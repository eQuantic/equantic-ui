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
                    
                    props.Add($"{ToCamelCase(propName)}: {value}");
                }
            }
            return $"{{ {string.Join(", ", props)} }}";
        }
        
        return "{}";
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 10;
}
