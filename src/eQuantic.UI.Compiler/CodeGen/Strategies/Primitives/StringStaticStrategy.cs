using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Converts C# static string methods to JavaScript equivalents.
/// Handles:
/// - string.IsNullOrEmpty(s) -> (!s || s === '')
/// - string.IsNullOrWhiteSpace(s) -> (!s || s.trim() === '')
/// - string.Join(sep, arr) -> arr.join(sep)
/// - string.Concat(a, b, ...) -> a + b + ...
/// - string.Format(fmt, args) -> template literal or sprintf-like
/// - string.Empty -> ''
/// </summary>
public class StringStaticStrategy : IConversionStrategy
{
    private static readonly HashSet<string> SupportedMethods = new()
    {
        "IsNullOrEmpty", "IsNullOrWhiteSpace", "Join", "Concat", "Format", "Compare", "Equals"
    };

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var methodName = memberAccess.Name.Identifier.Text;
        if (!SupportedMethods.Contains(methodName))
            return false;

        // Check if it's String.Method or string.Method
        var expressionText = memberAccess.Expression.ToString();
        if (expressionText == "string" || expressionText == "String")
            return true;

        // Also check via semantic model
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && ms.IsStatic)
        {
            var containingType = ms.ContainingType.ToDisplayString();
            if (containingType == "string" || containingType == "System.String")
                return true;
        }

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;

        var args = invocation.ArgumentList.Arguments
            .Select(a => context.Converter.ConvertExpression(a.Expression))
            .ToList();

        return methodName switch
        {
            "IsNullOrEmpty" => ConvertIsNullOrEmpty(args),
            "IsNullOrWhiteSpace" => ConvertIsNullOrWhiteSpace(args),
            "Join" => ConvertJoin(args),
            "Concat" => ConvertConcat(args),
            "Format" => ConvertFormat(args),
            "Compare" => ConvertCompare(args),
            "Equals" => ConvertEquals(args),
            _ => $"String.{ToCamelCase(methodName)}({string.Join(", ", args)})"
        };
    }

    private string ConvertIsNullOrEmpty(List<string> args)
    {
        if (args.Count == 0) return "false";
        var arg = args[0];
        return $"(!{arg} || {arg} === '')";
    }

    private string ConvertIsNullOrWhiteSpace(List<string> args)
    {
        if (args.Count == 0) return "false";
        var arg = args[0];
        return $"(!{arg} || {arg}.trim() === '')";
    }

    private string ConvertJoin(List<string> args)
    {
        if (args.Count < 2) return "''";
        // string.Join(separator, array) -> array.join(separator)
        return $"{args[1]}.join({args[0]})";
    }

    private string ConvertConcat(List<string> args)
    {
        if (args.Count == 0) return "''";
        // string.Concat(a, b, c) -> a + b + c
        // But if it's an array, use join
        if (args.Count == 1)
            return $"[...{args[0]}].join('')";
        return $"({string.Join(" + ", args)})";
    }

    private string ConvertFormat(List<string> args)
    {
        if (args.Count == 0) return "''";
        if (args.Count == 1) return args[0];

        // Simple replacement: string.Format("{0} {1}", a, b) -> `${a} ${b}`
        // For now, use a runtime helper approach
        var format = args[0];
        var formatArgs = args.Skip(1).ToList();

        // Generate a replace chain
        var result = format;
        for (int i = 0; i < formatArgs.Count; i++)
        {
            result = $"{result}.replace('{{{i}}}', {formatArgs[i]})";
        }
        return result;
    }

    private string ConvertCompare(List<string> args)
    {
        if (args.Count < 2) return "0";
        // string.Compare(a, b) -> a.localeCompare(b)
        return $"{args[0]}.localeCompare({args[1]})";
    }

    private string ConvertEquals(List<string> args)
    {
        if (args.Count < 2) return "false";
        // string.Equals(a, b) -> a === b
        // string.Equals(a, b, StringComparison.OrdinalIgnoreCase) -> a.toLowerCase() === b.toLowerCase()
        if (args.Count >= 3 && args[2].Contains("IgnoreCase"))
            return $"({args[0]}.toLowerCase() === {args[1]}.toLowerCase())";
        return $"({args[0]} === {args[1]})";
    }

    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 15; // Higher than InvocationStrategy (1)
}
