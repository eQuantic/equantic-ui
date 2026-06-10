using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Models;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.Parser;

/// <summary>
/// Parser for component files using Roslyn
/// </summary>
public class ComponentParser
{
    private SemanticModelProvider? _semanticModelProvider;

    /// <summary>
    /// Supplies a semantic model provider so component detection can walk the base-type chain (via the
    /// project compilation, which resolves library bases) instead of matching base-type name strings.
    /// </summary>
    public void SetSemanticModelProvider(SemanticModelProvider provider) => _semanticModelProvider = provider;

    /// <summary>Semantic model for the tree (via the project compilation), or null to fall back to the
    /// syntactic heuristic — never throws.</summary>
    private SemanticModel? TryGetSemanticModel(SyntaxTree tree)
    {
        if (_semanticModelProvider == null) return null;
        try { return _semanticModelProvider.GetSemanticModel(tree); }
        catch { return null; }
    }
    /// <summary>
    /// Parse a .eqx file and extract component definitions
    /// </summary>
    public IEnumerable<ComponentDefinition> Parse(string filePath)
    {
        var sourceCode = File.ReadAllText(filePath);
        return ParseSource(sourceCode, filePath);
    }
    
    /// <summary>
    /// Parse source code and extract component definitions
    /// </summary>
    public IEnumerable<ComponentDefinition> ParseSource(string sourceCode, string sourcePath = "")
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode, path: sourcePath);
        var root = tree.GetCompilationUnitRoot();
        var results = new List<ComponentDefinition>();
        
        // Extract namespace
        string? ns = null;
        var namespaceDecl = root.DescendantNodes()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        
        if (namespaceDecl != null)
        {
            ns = namespaceDecl.Name.ToString();
        }
        else
        {
            var blockNamespace = root.DescendantNodes()
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault();
            if (blockNamespace != null)
            {
                ns = blockNamespace.Name.ToString();
            }
        }
        
        // Discover user value types: records (positional or body) and structs are emitted as named JS
        // classes. Reactive — driven by the declarations actually present, not a fixed list.
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (RecordTypeEmitter.CanEmit(typeDecl))
            {
                results.Add(new ComponentDefinition
                {
                    Name = typeDecl.Identifier.Text,
                    SourcePath = sourcePath,
                    SyntaxTree = tree,
                    Namespace = ns ?? "",
                    IsRecordType = true,
                    ValueTypeSyntax = typeDecl,
                });
            }
        }

        // Discover static utility classes (`static class Format { … }`) used from components — emitted as
        // their own module of static members so `Format.Foo()` resolves at runtime.
        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (classDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                results.Add(new ComponentDefinition
                {
                    Name = classDecl.Identifier.Text,
                    SourcePath = sourcePath,
                    SyntaxTree = tree,
                    Namespace = ns ?? "",
                    IsStaticHelper = true,
                    ValueTypeSyntax = classDecl,
                });
            }
        }

        // Find component classes. Prefer a semantic base-type walk (resolves library bases like Flex /
        // Container and any user-defined intermediate component) over matching base-type name strings;
        // fall back to the syntactic heuristic when no semantic model is available.
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
        var model = TryGetSemanticModel(tree);

        // Pre-pass: which classes are components. A class is one if its base resolves to a known component
        // base (semantic walk) / matches a base-name heuristic / it declares Build|Render — AND, transitively,
        // any class extending another in-file component (so a subclass of a user component is recognized even
        // without its own Build and without relying on the semantic model resolving the full base chain).
        string? BaseName(ClassDeclarationSyntax c)
        {
            var b = c.BaseList?.Types.FirstOrDefault()?.Type.ToString();
            if (b == null) return null;
            if (b.Contains('<')) b = b.Substring(0, b.IndexOf('<'));
            return b.Contains('.') ? b.Substring(b.LastIndexOf('.') + 1) : b;
        }
        var componentNames = new HashSet<string>();
        foreach (var c in classes)
        {
            var bn = BaseName(c);
            var hasBR = c.Members.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.Text is "Render" or "Build");
            bool direct = model?.GetDeclaredSymbol(c) is INamedTypeSymbol s
                ? (s.IsUiComponent() || hasBR)
                : (bn is "StatefulComponent" or "StatelessComponent" or "HtmlElement" or "Flex" or "Container" or "Stack" || hasBR);
            if (direct) componentNames.Add(c.Identifier.Text);
        }
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var c in classes)
            {
                if (componentNames.Contains(c.Identifier.Text)) continue;
                var bn = BaseName(c);
                if (bn != null && componentNames.Contains(bn)) { componentNames.Add(c.Identifier.Text); changed = true; }
            }
        }

        foreach (var classDecl in classes)
        {
            var baseType = classDecl.BaseList?.Types.FirstOrDefault()?.Type.ToString();
            var hasBuildOrRender = classDecl.Members.OfType<MethodDeclarationSyntax>()
                .Any(m => m.Identifier.Text is "Render" or "Build");

            if (!componentNames.Contains(classDecl.Identifier.Text)) continue;

            var definition = new ComponentDefinition
            {
                Name = classDecl.Identifier.Text,
                SourcePath = sourcePath,
                SyntaxTree = tree,
                Namespace = ns ?? "",
                TypeParameters = classDecl.TypeParameterList?.Parameters.Select(p => p.Identifier.Text).ToList() ?? new List<string>(),
                IsAbstract = classDecl.Modifiers.Any(SyntaxKind.AbstractKeyword)
            };

            if (baseType == "StatefulComponent")
            {
                definition.IsStateful = true;
                
                // Parse Page attributes and ServerActions
                ParsePageAttributes(classDecl, definition);
                ParseServerActions(classDecl, definition);
                
                // Find state class name from CreateState method
                var createStateMethod = classDecl.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == "CreateState");
                
                if (createStateMethod != null)
                {
                    var newExpr = createStateMethod.DescendantNodes()
                        .OfType<ObjectCreationExpressionSyntax>()
                        .FirstOrDefault();
                    
                    if (newExpr != null)
                    {
                        definition.StateClassName = newExpr.Type.ToString();
                    }
                }

                definition.BaseClassName = baseType;
                
                // If we found a state class name, find it in the same file
                if (!string.IsNullOrEmpty(definition.BaseClassName) && !string.IsNullOrEmpty(definition.StateClassName))
                {
                    var stateClass = classes.FirstOrDefault(c => c.Identifier.Text == definition.StateClassName);
                    if (stateClass != null)
                    {
                        ParseStateClass(stateClass, definition);
                    }
                }

                // Static/instance data fields declared on the stateful component class itself.
                ParseComponentFields(classDecl, definition);
            }
            else if (baseType == "StatelessComponent")
            {
                definition.IsStateful = false;
                definition.BaseClassName = baseType;
                ParsePageAttributes(classDecl, definition);

                // Parse Build method for stateless component
                var buildMethod = classDecl.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == "Build");

                if (buildMethod != null)
                {
                    definition.BuildMethodNode = buildMethod;

                    var returnStatement = buildMethod.DescendantNodes()
                        .OfType<ReturnStatementSyntax>()
                        .FirstOrDefault();

                    if (returnStatement?.Expression != null)
                    {
                        definition.BuildTree = ParseComponentExpression(returnStatement.Expression);
                    }
                }

                // Parse constructors (for components with positional args like Text, Heading)
                ParseConstructors(classDecl, definition);

                // Parse other helper methods
                ParseMethods(classDecl, definition);

                // Capture static/instance data fields declared on the component
                ParseComponentFields(classDecl, definition);
            }
            else if (baseType == "HtmlElement")
            {
                definition.IsPrimitive = true;
                definition.IsStateful = false;
                definition.BaseClassName = baseType;
                ParsePrimitiveClass(classDecl, definition);
            }
            else
            {
                // Component that extends another component (not directly StatelessComponent/HtmlElement)
                // Check if it has its own Build method
                definition.IsStateful = false;
                definition.BaseClassName = baseType;

                var buildMethod = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == "Build");

                if (buildMethod != null)
                {
                    definition.BuildMethodNode = buildMethod;

                    var returnStatement = buildMethod.DescendantNodes()
                        .OfType<ReturnStatementSyntax>()
                        .FirstOrDefault();

                    if (returnStatement?.Expression != null)
                    {
                        definition.BuildTree = ParseComponentExpression(returnStatement.Expression);
                    }
                }
                else
                {
                    // No Build method, treat as primitive (extends another component without overriding)
                    definition.IsPrimitive = true;
                    ParseMethods(classDecl, definition);
                }
            }

            results.Add(definition);
        }
        
        return results;
    }
    
    private void ParsePageAttributes(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var attrName = attr.Name.ToString();
                if (attrName == "Page" || attrName == "PageAttribute")
                {
                    var routeInfo = new PageRouteInfo();
                    
                    if (attr.ArgumentList?.Arguments.Count > 0)
                    {
                        var routeArg = attr.ArgumentList.Arguments[0];
                        routeInfo.Route = routeArg.Expression.ToString().Trim('"');
                        
                        // Check for named Title argument
                        foreach (var arg in attr.ArgumentList.Arguments.Skip(1))
                        {
                            if (arg.NameEquals?.Name.ToString() == "Title")
                            {
                                routeInfo.Title = arg.Expression.ToString().Trim('"');
                            }
                        }
                    }
                    
                    definition.PageRoutes.Add(routeInfo);
                }
            }
        }
    }
    
    private void ParseServerActions(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        var methods = classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var serverActionAttr = method.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(a => a.Name.ToString() == "ServerAction" || a.Name.ToString() == "ServerActionAttribute");
            
            if (serverActionAttr != null)
            {
                string actionName = method.Identifier.Text;
                
                // Check for Name parameter
                if (serverActionAttr.ArgumentList != null)
                {
                    foreach (var arg in serverActionAttr.ArgumentList.Arguments)
                    {
                        if (arg.NameEquals?.Name.ToString() == "Name")
                        {
                            actionName = arg.Expression.ToString().Trim('"');
                            break;
                        }
                    }
                }

                var actionInfo = new ServerActionInfo
                {
                    MethodName = method.Identifier.Text,
                    ActionId = $"{definition.Name}/{actionName}",
                    ReturnType = method.ReturnType.ToString(),
                    TypeParameters = method.TypeParameterList?.Parameters.Select(p => p.Identifier.Text).ToList() ?? new List<string>(),
                    IsAsync = method.Modifiers.Any(m => m.ValueText == "async"),
                    SyntaxNode = method
                };
                
                foreach (var param in method.ParameterList.Parameters)
                {
                    actionInfo.Parameters.Add(new ParameterDefinition
                    {
                        Name = param.Identifier.Text,
                        Type = param.Type?.ToString() ?? "object"
                    });
                }
                
                definition.ServerActions.Add(actionInfo);
            }
        }
    }
    
    private void ParseMethods(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        // Extract properties
        var properties = classDecl.Members
            .OfType<PropertyDeclarationSyntax>();
        
        foreach (var prop in properties)
        {
            var isPublic = prop.Modifiers.Any(SyntaxKind.PublicKeyword);
            
            if (definition.Properties.Any(p => p.Name == prop.Identifier.Text)) continue;

            definition.Properties.Add(new PropertyDefinition
            {
                Name = prop.Identifier.Text,
                Type = prop.Type.ToString(),
                DefaultValue = prop.Initializer?.Value.ToString(),
                DefaultValueNode = prop.Initializer?.Value,
                IsPublic = isPublic,
                IsStatic = prop.Modifiers.Any(SyntaxKind.StaticKeyword),
                Node = prop
            });
        }
        
        // Extract methods (excluding Render/Build which are handled separately)
        var methods = classDecl.Members
            .OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var methodName = method.Identifier.Text;
            if (methodName == "Render" || methodName == "Build" || methodName == "CreateState")
            {
                if (methodName == "Render" || methodName == "Build")
                {
                    definition.BuildMethodNode = method;
                }
                continue;
            }

            if (definition.Methods.Any(m => m.Name == methodName)) continue;

            var methodDef = new MethodDefinition
            {
                Name = methodName,
                ReturnType = method.ReturnType.ToString(),
                TypeParameters = method.TypeParameterList?.Parameters.Select(p => p.Identifier.Text).ToList() ?? new List<string>(),
                Body = method.Body?.ToString() ?? method.ExpressionBody?.Expression.ToString() ?? "",
                SyntaxNode = method
            };
            
            foreach (var param in method.ParameterList.Parameters)
            {
                methodDef.Parameters.Add(new ParameterDefinition
                {
                    Name = param.Identifier.Text,
                    Type = param.Type?.ToString() ?? "object"
                });
            }
            
            definition.Methods.Add(methodDef);
        }
    }

    private void ParsePrimitiveClass(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        ParseMethods(classDecl, definition);

        // Extract constructors
        var constructors = classDecl.Members
            .OfType<ConstructorDeclarationSyntax>();
        
        foreach (var ctor in constructors)
        {
            var ctorDef = new MethodDefinition
            {
                Name = ctor.Identifier.Text,
                ReturnType = "void",
                Body = ctor.Body?.ToString() ?? ctor.ExpressionBody?.Expression.ToString() ?? "",
                SyntaxNode = null // Marker for constructor helper
            };

            foreach (var param in ctor.ParameterList.Parameters)
            {
                ctorDef.Parameters.Add(new ParameterDefinition
                {
                    Name = param.Identifier.Text,
                    Type = param.Type?.ToString() ?? "object"
                });
            }

            definition.Constructors.Add(ctorDef);
        }
    }

    private void ParseConstructors(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        var constructors = classDecl.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => c.ParameterList.Parameters.Count > 0); // Only non-default constructors

        foreach (var ctor in constructors)
        {
            var ctorDef = new MethodDefinition
            {
                Name = ctor.Identifier.Text,
                ReturnType = "void",
                Body = ctor.Body?.ToString() ?? ctor.ExpressionBody?.Expression.ToString() ?? "",
                SyntaxNode = null,
                BodyNode = ctor.Body
            };

            foreach (var param in ctor.ParameterList.Parameters)
            {
                ctorDef.Parameters.Add(new ParameterDefinition
                {
                    Name = param.Identifier.Text,
                    Type = param.Type?.ToString() ?? "object"
                });
            }

            definition.Constructors.Add(ctorDef);
        }
    }

    /// <summary>
    /// Captures fields declared directly on a component class (static data, consts, instance fields) into
    /// <see cref="ComponentDefinition.ComponentFields"/>. Uses direct members (not descendants) so fields
    /// of any nested type aren't pulled in. A field is treated as static when declared <c>static</c> or
    /// <c>const</c> — both become a <c>static</c> class member referenced as <c>ClassName.field</c>.
    /// </summary>
    private void ParseComponentFields(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
        {
            var isStatic = field.Modifiers.Any(SyntaxKind.StaticKeyword)
                           || field.Modifiers.Any(SyntaxKind.ConstKeyword);
            foreach (var variable in field.Declaration.Variables)
            {
                definition.ComponentFields.Add(new StateField
                {
                    Name = variable.Identifier.Text,
                    Type = field.Declaration.Type.ToString(),
                    DefaultValue = variable.Initializer?.Value.ToString(),
                    DefaultValueNode = variable.Initializer?.Value,
                    IsStatic = isStatic
                });
            }
        }
    }

    private void ParseStateClass(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        // Extract fields
        var fields = classDecl.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Where(f => f.Modifiers.Any(SyntaxKind.PrivateKeyword));
        
        foreach (var field in fields)
        {
            foreach (var variable in field.Declaration.Variables)
            {
                definition.StateFields.Add(new StateField
                {
                    Name = variable.Identifier.Text,
                    Type = field.Declaration.Type.ToString(),
                    DefaultValue = variable.Initializer?.Value.ToString(),
                    DefaultValueNode = variable.Initializer?.Value
                });
            }
        }
        
        // Extract methods (excluding Build)
        var methods = classDecl.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text != "Build");
        
        foreach (var method in methods)
        {
            var methodDef = new MethodDefinition
            {
                Name = method.Identifier.Text,
                ReturnType = method.ReturnType.ToString(),
                TypeParameters = method.TypeParameterList?.Parameters.Select(p => p.Identifier.Text).ToList() ?? new List<string>(),
                Body = method.Body?.ToString() ?? method.ExpressionBody?.Expression.ToString() ?? "",
                SyntaxNode = method
            };
            
            foreach (var param in method.ParameterList.Parameters)
            {
                methodDef.Parameters.Add(new ParameterDefinition
                {
                    Name = param.Identifier.Text,
                    Type = param.Type?.ToString() ?? "object"
                });
            }
            
            definition.Methods.Add(methodDef);
        }
        
        // Parse Build method for component tree
        var buildMethod = classDecl.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "Build");
        
        if (buildMethod != null)
        {
            // Capture full method node for robust conversion (Phase 2)
            definition.BuildMethodNode = buildMethod;

            var returnStatement = buildMethod.DescendantNodes()
                .OfType<ReturnStatementSyntax>()
                .FirstOrDefault();
            
            if (returnStatement?.Expression != null)
            {
                definition.BuildTree = ParseComponentExpression(returnStatement.Expression);
            }
        }
    }
    
    private ComponentTree? ParseComponentExpression(ExpressionSyntax expression)
    {
        // Handle object initializer: new Container { ... }
        if (expression is ObjectCreationExpressionSyntax objectCreation)
        {
            return ParseObjectCreation(objectCreation);
        }
        
        // Handle implicit object creation: new() { ... } - treat as Component
        if (expression is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            return ParseImplicitObjectCreation(implicitCreation);
        }
        
        // Handle constructor with args: new Text("content")
        if (expression is InvocationExpressionSyntax invocation)
        {
            // Could be a factory method
            return new ComponentTree
            {
                ComponentType = invocation.Expression.ToString()
            };
        }
        
        return null;
    }
    
    private ComponentTree ParseObjectCreation(ObjectCreationExpressionSyntax objectCreation)
    {
        var tree = new ComponentTree
        {
            ComponentType = objectCreation.Type.ToString()
        };
        
        // Parse constructor arguments
        if (objectCreation.ArgumentList?.Arguments.Count > 0)
        {
            var firstArg = objectCreation.ArgumentList.Arguments[0];
            // For Text("content"), store as Content property
            tree.Properties["Content"] = ParsePropertyValue(firstArg.Expression, "Content");
        }
        
        // Parse initializer properties
        if (objectCreation.Initializer != null)
        {
            ParseInitializer(objectCreation.Initializer, tree);
        }
        
        return tree;
    }
    
    private ComponentTree ParseImplicitObjectCreation(ImplicitObjectCreationExpressionSyntax implicitCreation)
    {
        var tree = new ComponentTree
        {
            ComponentType = "Unknown" // Will be inferred from context
        };
        
        if (implicitCreation.Initializer != null)
        {
            ParseInitializer(implicitCreation.Initializer, tree);
        }
        
        return tree;
    }
    
    private void ParseInitializer(InitializerExpressionSyntax initializer, ComponentTree tree)
    {
        foreach (var expr in initializer.Expressions)
        {
            if (expr is AssignmentExpressionSyntax assignment)
            {
                var propName = assignment.Left.ToString();
                var propValue = ParsePropertyValue(assignment.Right, propName);
                tree.Properties[propName] = propValue;
                
                // Handle Children specially
                if (propName == "Children" && assignment.Right is InitializerExpressionSyntax childInit)
                {
                    foreach (var childExpr in childInit.Expressions)
                    {
                        var childTree = ParseComponentExpression(childExpr);
                        if (childTree != null)
                        {
                            tree.Children.Add(childTree);
                        }
                    }
                }
            }
        }
    }
    
    private PropertyValue ParsePropertyValue(ExpressionSyntax expression, string? propName = null)
    {
        var value = expression switch
        {
            LiteralExpressionSyntax literal => new PropertyValue
            {
                Type = literal.Kind() switch
                {
                    SyntaxKind.StringLiteralExpression => PropertyValueType.String,
                    SyntaxKind.NumericLiteralExpression => PropertyValueType.Number,
                    SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression => PropertyValueType.Boolean,
                    _ => PropertyValueType.String
                },
                StringValue = literal.Token.ValueText
            },
            
            InterpolatedStringExpressionSyntax interpolated => new PropertyValue
            {
                Type = PropertyValueType.Expression,
                Expression = interpolated.ToString(),
                ExpressionNode = interpolated
            },
            
            // Lambda expression: (v) => SetState(() => _message = v)
            ParenthesizedLambdaExpressionSyntax or SimpleLambdaExpressionSyntax => new PropertyValue
            {
                Type = PropertyValueType.EventHandler,
                Expression = expression.ToString(),
                ExpressionNode = expression
            },
            
            // Member access: AppStyles.Button
            MemberAccessExpressionSyntax memberAccess => new PropertyValue
            {
                Type = memberAccess.Name.ToString() switch
                {
                    _ when memberAccess.Expression.ToString().Contains("Styles") => PropertyValueType.StyleClass,
                    _ => PropertyValueType.Expression
                },
                Expression = memberAccess.ToString(),
                ExpressionNode = memberAccess
            },
            
            // Object creation: new Container { ... }
            ObjectCreationExpressionSyntax objCreation => new PropertyValue
            {
                Type = PropertyValueType.Component,
                ComponentValue = ParseObjectCreation(objCreation)
            },
            
            // Initializer expression { ... }
            InitializerExpressionSyntax initExpr => new PropertyValue
            {
                Type = PropertyValueType.ComponentList,
                ListValue = initExpr.Expressions
                    .Select(e => ParseComponentExpression(e))
                    .Where(c => c != null)
                    .Cast<ComponentTree>()
                    .ToList()
            },
            
            // Default: treat as expression
            _ => new PropertyValue
            {
                Type = PropertyValueType.Expression,
                Expression = expression.ToString(),
                ExpressionNode = expression
            }
        };

        // Heuristic: automatically treat "On*" properties as handlers if they ended up as expressions
        if (propName != null && propName.StartsWith("On") && value.Type == PropertyValueType.Expression)
        {
            value.Type = PropertyValueType.EventHandler;
        }

        return value;
    }
}
