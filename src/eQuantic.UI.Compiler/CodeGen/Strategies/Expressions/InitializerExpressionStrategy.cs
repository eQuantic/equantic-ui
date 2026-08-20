using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for object and collection initializers.
/// Handles:
/// - { new A(), new B() } → [ new A(), new B() ]
/// - { Prop = val } → { prop: val }
/// - { {k, v} } → { k: v }
/// </summary>
public class InitializerExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is InitializerExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        return ConvertInitializer((InitializerExpressionSyntax)node, context);
    }

    public string ConvertInitializer(InitializerExpressionSyntax initializer, ConversionContext context)
    {
        if (initializer == null) return "{}";
        
        // Collection Initializer: { new A(), new B() } -> [ new A(), new B() ]
        if (initializer.Kind() == SyntaxKind.CollectionInitializerExpression)
        {
            // Check if it's a Dictionary initializer: { {k, v}, {k, v} }
            if (initializer.Expressions.Count > 0 && initializer.Expressions.All(e => e is InitializerExpressionSyntax ie && ie.Expressions.Count == 2))
            {
                var pairs = initializer.Expressions.Cast<InitializerExpressionSyntax>()
                    .Select(ie => $"{context.Converter.ConvertExpression(ie.Expressions[0])}: {context.Converter.ConvertExpression(ie.Expressions[1])}");
                return $"{{ {string.Join(", ", pairs)} }}";
            }
            
            var elements = initializer.Expressions.Select(e => context.Converter.ConvertExpression(e));
            return $"[{string.Join(", ", elements)}]";
        }
        
        // Object Initializer: { Prop = Value } -> { prop: value }
        if (initializer.Kind() == SyntaxKind.ObjectInitializerExpression)
        {
            var props = new List<string>();
            foreach (var expr in initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax assignment)
                {
                    // Indexer keys in an initializer (`["cs"] = CSharp`, `[TokenKind.X] = v`): a JS
                    // COMPUTED key, with the key expression properly converted (an enum key lowers
                    // to its member string, where the old raw text named a class that doesn't
                    // exist). C# 13's from-the-END form (`[^1] = v`) means "mutate the EXISTING
                    // member's tail", which an object literal cannot say — that one is fenced.
                    if (assignment.Left is ImplicitElementAccessSyntax elementKey)
                    {
                        if (elementKey.ArgumentList.Arguments.Count != 1
                            || elementKey.ArgumentList.Arguments[0].Expression
                                is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.IndexExpression })
                        {
                            context.Report(assignment, ConversionSeverity.Error, "EQ2008",
                                "from-the-end indexer assignments inside initializers "
                                + "(`X = { [^i] = v }`) are not lowered yet — assign after construction.");
                            continue;
                        }

                        var key = context.Converter.ConvertExpression(elementKey.ArgumentList.Arguments[0].Expression);
                        props.Add($"[{key}]: {context.Converter.ConvertExpression(assignment.Right)}");
                        continue;
                    }

                    var propName = assignment.Left.ToString();
                    var value = context.Converter.ConvertExpression(assignment.Right);
                    
                    // Special handling for Children in initialization
                    if (propName == "Children")
                    {
                        if (assignment.Right is InitializerExpressionSyntax childInit)
                        {
                            // Avoid recursive infinite loop by explicitly calling conversion
                            // We need to handle this carefully.
                            // The easiest way is to use a helper or detect it.
                            // Actually, childInit is InitializerExpressionSyntax, so ConvertExpression will dispatch back to us.
                            // But we are inside ConvertInitializer, so calling ConvertExpression(childInit) matches this strategy.
                            value = Convert(childInit, context);
                            
                            var trimmedValue = value?.Trim();
                            if (string.IsNullOrEmpty(trimmedValue) || (trimmedValue.StartsWith("{") && trimmedValue.EndsWith("}") && string.IsNullOrWhiteSpace(trimmedValue.Substring(1, trimmedValue.Length - 2))))
                                value = "[]";
                        }
                        else 
                        {
                             var trimmedValue = value?.Trim();
                             if (string.IsNullOrEmpty(trimmedValue) || (trimmedValue.StartsWith("{") && trimmedValue.EndsWith("}") && string.IsNullOrWhiteSpace(trimmedValue.Substring(1, trimmedValue.Length - 2))))
                                value = "[]";
                        }
                    }
                    
                    // Event handler binding: Use semantic model to detect delegate/action assignments
                    var isEventHandler = false;
                    var leftType = context.SemanticHelper.GetType(assignment.Left);
                    if (leftType != null)
                    {
                        if (leftType.TypeKind == TypeKind.Delegate) isEventHandler = true;
                        else if ((leftType.Name == "Action" || leftType.Name == "Func") && context.SemanticHelper.IsSystemType(leftType))
                            isEventHandler = true;
                    }

                    if (isEventHandler && value != null)
                    {
                        var rightSymbol = context.SemanticHelper.GetSymbol(assignment.Right);
                        if (rightSymbol is IMethodSymbol methodSymbol && !methodSymbol.IsStatic)
                        {
                            // If it's an instance method reference and not already bound or a lambda
                            if (!value.Contains("=>") && !value.Contains("function") && !value.Contains(".bind("))
                            {
                                value = $"{value}.bind(this)";
                            }
                        }
                    }
                    
                    props.Add($"{propName.ToCamelCase()}: {value}");
                }
            }
            return $"{{ {string.Join(", ", props)} }}";
        }

        // Bare array initializer: `string[] N = { "a", "b" }` (no `new[]`) — an ArrayInitializerExpression,
        // not a collection/object initializer. Map its elements to a JS array, same as `new[] { … }`.
        if (initializer.Kind() == SyntaxKind.ArrayInitializerExpression)
        {
            var elements = initializer.Expressions.Select(e => context.Converter.ConvertExpression(e));
            return $"[{string.Join(", ", elements)}]";
        }

        return "{}";
    }

    public int Priority => 10;
}
