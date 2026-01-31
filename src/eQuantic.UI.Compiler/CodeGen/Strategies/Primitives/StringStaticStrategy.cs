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
             // Simple fallback: if 1st arg is string literal, we might do replacement, but for now
             // we return a simplified template literal approach if just 1 arg?
             // Or rely on a helper `stringFormat(fmt, ...args)` which we assume exists or emit inline?

             // Implementing simple replacement: format(fmt, ...args)
             var fmt = context.Converter.ConvertExpression(args[0].Expression);
             var restArgs = string.Join(", ", args.Skip(1).Select(a => context.Converter.ConvertExpression(a.Expression)));

             // Emitting a runtime helper call since replace matches need regex
             // "fmt".replace(/{(\d+)}/g, (match, number) => typeof args[number] != 'undefined' ? args[number] : match)
             return $"(function(f, ...a) {{ return f.replace(/{{(\\d+)}}/g, (m, n) => typeof a[n] != 'undefined' ? a[n] : m); }})({fmt}, {restArgs})";
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
