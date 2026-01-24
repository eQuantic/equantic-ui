using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Models;

namespace eQuantic.UI.Compiler.Parser;

/// <summary>
/// Parser for component files using Roslyn
/// </summary>
public class ComponentParser
{
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
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
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
        
        // Find component class (extends StatefulComponent, StatelessComponent or HtmlElement)
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
        
        foreach (var classDecl in classes)
        {
            var baseType = classDecl.BaseList?.Types.FirstOrDefault()?.Type.ToString();
            
            bool isComp = baseType == "StatefulComponent" || 
                         baseType == "StatelessComponent" || 
                         baseType == "HtmlElement" ||
                         baseType == "Flex" || 
                         baseType == "Container" ||
                         baseType == "Stack" ||
                         classDecl.Members.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.Text == "Render" || m.Identifier.Text == "Build");
                         
            if (!isComp) continue;

            var definition = new ComponentDefinition
            {
                Name = classDecl.Identifier.Text,
                SourcePath = sourcePath,
                SyntaxTree = tree,
                Namespace = ns ?? ""
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
            }
            else if (baseType == "HtmlElement" || isComp)
            {
                definition.IsPrimitive = true;
                definition.IsStateful = false;
                definition.BaseClassName = baseType;
                ParsePrimitiveClass(classDecl, definition);
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
                    IsAsync = method.Modifiers.Any(m => m.ValueText == "async")
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
    
    private void ParsePrimitiveClass(ClassDeclarationSyntax classDecl, ComponentDefinition definition)
    {
        // Extract properties
        var properties = classDecl.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => p.Modifiers.Any(SyntaxKind.PublicKeyword));
        
        foreach (var prop in properties)
        {
            definition.Methods.Add(new MethodDefinition
            {
                Name = prop.Identifier.Text,
                ReturnType = prop.Type.ToString(),
                Body = "", // Properties don't have bodies in this context
                SyntaxNode = null // Marker for property
            });
        }
        
        // Extract methods (including Render)
        var methods = classDecl.DescendantNodes()
            .OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            if (method.Identifier.Text == "Render")
            {
                definition.BuildMethodNode = method;
                continue;
            }

            var methodDef = new MethodDefinition
            {
                Name = method.Identifier.Text,
                ReturnType = method.ReturnType.ToString(),
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

        // Extract constructors
        var constructors = classDecl.DescendantNodes()
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
            tree.Properties["Content"] = ParsePropertyValue(firstArg.Expression);
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
                var propValue = ParsePropertyValue(assignment.Right);
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
    
    private PropertyValue ParsePropertyValue(ExpressionSyntax expression)
    {
        return expression switch
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
    }
}
