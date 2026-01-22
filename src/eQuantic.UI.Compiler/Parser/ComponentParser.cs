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
    public ComponentDefinition Parse(string filePath)
    {
        var sourceCode = File.ReadAllText(filePath);
        return ParseSource(sourceCode, filePath);
    }
    
    /// <summary>
    /// Parse source code and extract component definitions
    /// </summary>
    public ComponentDefinition ParseSource(string sourceCode, string sourcePath = "")
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetCompilationUnitRoot();
        
        var definition = new ComponentDefinition
        {
            SourcePath = sourcePath
        };
        
        // Extract namespace
        var namespaceDecl = root.DescendantNodes()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        
        if (namespaceDecl != null)
        {
            definition.Namespace = namespaceDecl.Name.ToString();
        }
        else
        {
            var blockNamespace = root.DescendantNodes()
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault();
            if (blockNamespace != null)
            {
                definition.Namespace = blockNamespace.Name.ToString();
            }
        }
        
        // Find component class (extends StatefulComponent or StatelessComponent)
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
        
        foreach (var classDecl in classes)
        {
            var baseType = classDecl.BaseList?.Types.FirstOrDefault()?.Type.ToString();
            
            if (baseType == "StatefulComponent")
            {
                definition.Name = classDecl.Identifier.Text;
                definition.IsStateful = true;
                
                // Find the CreateState method to get state class name
                var createStateMethod = classDecl.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == "CreateState");
                
                if (createStateMethod != null)
                {
                    // Extract state class name from "new StateClassName()"
                    var newExpr = createStateMethod.DescendantNodes()
                        .OfType<ObjectCreationExpressionSyntax>()
                        .FirstOrDefault();
                    
                    if (newExpr != null)
                    {
                        definition.StateClassName = newExpr.Type.ToString();
                    }
                }
            }
            else if (baseType == "StatelessComponent")
            {
                definition.Name = classDecl.Identifier.Text;
                definition.IsStateful = false;
            }
            else if (baseType?.StartsWith("ComponentState") == true && definition.IsStateful)
            {
                // This is the state class - extract fields and methods
                ParseStateClass(classDecl, definition);
            }
        }
        
        return definition;
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
                    DefaultValue = variable.Initializer?.Value.ToString()
                });
            }
        }
        
        // Extract methods (excluding Build)
        var methods = classDecl.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text != "Build" && 
                       !m.Modifiers.Any(SyntaxKind.OverrideKeyword));
        
        foreach (var method in methods)
        {
            var methodDef = new MethodDefinition
            {
                Name = method.Identifier.Text,
                ReturnType = method.ReturnType.ToString(),
                Body = method.Body?.ToString() ?? method.ExpressionBody?.Expression.ToString() ?? ""
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
                Expression = interpolated.ToString()
            },
            
            // Lambda expression: (v) => SetState(() => _message = v)
            ParenthesizedLambdaExpressionSyntax or SimpleLambdaExpressionSyntax => new PropertyValue
            {
                Type = PropertyValueType.EventHandler,
                Expression = expression.ToString()
            },
            
            // Member access: AppStyles.Button
            MemberAccessExpressionSyntax memberAccess => new PropertyValue
            {
                Type = memberAccess.Name.ToString() switch
                {
                    _ when memberAccess.Expression.ToString().Contains("Styles") => PropertyValueType.StyleClass,
                    _ => PropertyValueType.Expression
                },
                Expression = memberAccess.ToString()
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
                Expression = expression.ToString()
            }
        };
    }
}
