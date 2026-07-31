using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for static String methods.
/// Handles:
/// - String.IsNullOrEmpty(s) -> !s
/// - String.Join(sep, val) -> val.join(sep)
/// - String.Format(fmt, args) -> fmt.replace... (Simplified)
/// </summary>
public class StringStaticStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        // `string.Empty` — the static PROPERTY (no invocation): the empty string literal.
        if (node is MemberAccessExpressionSyntax { Name.Identifier.Text: "Empty" } property
            && property.Expression.ToString() is "string" or "String" or "System.String")
            return true;

        if (node is not InvocationExpressionSyntax invocation) return false;
        
        var methodAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (methodAccess == null) return false;

        var typeExpression = methodAccess.Expression.ToString();
        var methodName = methodAccess.Name.Identifier.Text;
        
        // Check for String.Method or System.String.Method
        // Heuristic: "String" or "string"
        if (typeExpression != "String" && typeExpression != "string" && typeExpression != "System.String")
            return false;
            
        return methodName switch
        {
            "IsNullOrEmpty" => true,
            "IsNullOrWhiteSpace" => true,
            "Join" => true,
            "Concat" => true,
            "Format" => true,
            "Compare" => true,
            "Equals" => true,
            _ => false
        };
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        if (node is MemberAccessExpressionSyntax { Name.Identifier.Text: "Empty" })
            return "''";

        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;
        var args = invocation.ArgumentList.Arguments;

        if (methodName == "IsNullOrEmpty")
        {
            var target = context.Converter.ConvertExpression(args[0].Expression);
            return $"!{target}";
        }
        
        if (methodName == "IsNullOrWhiteSpace")
        {
            var target = context.Converter.ConvertExpression(args[0].Expression);
            // !x || !x.trim()
            return $"(!{target} || !{target}.trim())";
        }
        
        if (methodName == "Join")
        {
            // Join(separator, values)
            var separator = context.Converter.ConvertExpression(args[0].Expression);
            var values = context.Converter.ConvertExpression(args[1].Expression);
            return $"{values}.join({separator})";
        }
        
        if (methodName == "Concat")
        {
            if (args.Count == 0) return "''";
            // string.Concat(a, b, c) -> a + b + c
            // But if it's an array, use join
            if (args.Count == 1)
            {
                var arg = context.Converter.ConvertExpression(args[0].Expression);
                return $"[...{arg}].join('')";
            }
            var concatenated = string.Join(" + ", args.Select(a => context.Converter.ConvertExpression(a.Expression)));
            return $"({concatenated})";
        }

        if (methodName == "Format")
        {
             // Route to the runtime helper, which substitutes {i}/{i:spec} (the latter via the same
             // formatter the interpolation path uses, so `{0:F2}` works) and unescapes {{/}}.
             context.UsedHelpers.Add(Eq.Import);
             var fmt = context.Converter.ConvertExpression(args[0].Expression);
             var restArgs = string.Join(", ", args.Skip(1).Select(a => context.Converter.ConvertExpression(a.Expression)));
             return restArgs.Length > 0 ? $"{Eq.StringFormat}({fmt}, {restArgs})" : $"{Eq.StringFormat}({fmt})";
        }

        if (methodName == "Compare")
        {
            if (args.Count < 2) return "0";
            // string.Compare(a, b) -> a.localeCompare(b)
            var first = context.Converter.ConvertExpression(args[0].Expression);
            var second = context.Converter.ConvertExpression(args[1].Expression);
            return $"{first}.localeCompare({second})";
        }

        if (methodName == "Equals")
        {
            if (args.Count < 2) return "false";
            // string.Equals(a, b) -> a === b
            // string.Equals(a, b, StringComparison.OrdinalIgnoreCase) -> a.toLowerCase() === b.toLowerCase()
            var first = context.Converter.ConvertExpression(args[0].Expression);
            var second = context.Converter.ConvertExpression(args[1].Expression);

            if (args.Count >= 3)
            {
                var comparison = args[2].Expression.ToString();
                if (comparison.Contains("IgnoreCase"))
                    return $"({first}.toLowerCase() === {second}.toLowerCase())";
            }
            return $"({first} === {second})";
        }

        return node.ToString();
    }

    public int Priority => 20;
}
