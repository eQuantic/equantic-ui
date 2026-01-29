using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Converts C# string instance methods to JavaScript equivalents.
/// Handles:
/// - Split(separator) -> split(separator)
/// - Replace(old, new) -> replaceAll(old, new)
/// - StartsWith(prefix) -> startsWith(prefix)
/// - EndsWith(suffix) -> endsWith(suffix)
/// - Contains(substring) -> includes(substring)
/// - Substring(start, length?) -> substring(start, start + length) or slice(start)
/// - IndexOf(value) -> indexOf(value)
/// - LastIndexOf(value) -> lastIndexOf(value)
/// - PadLeft(width, char?) -> padStart(width, char)
/// - PadRight(width, char?) -> padEnd(width, char)
/// - TrimStart() -> trimStart()
/// - TrimEnd() -> trimEnd()
/// </summary>
public class StringMethodStrategy : IConversionStrategy
{
    private static readonly HashSet<string> SupportedMethods = new()
    {
        "Split", "Replace", "StartsWith", "EndsWith", "Contains",
        "Substring", "IndexOf", "LastIndexOf", "PadLeft", "PadRight",
        "TrimStart", "TrimEnd", "ToCharArray", "Insert", "Remove"
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

        // Check if it's a string method via semantic model
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms)
        {
            var containingType = ms.ContainingType.ToDisplayString();
            if (containingType == "string" || containingType == "System.String")
                return true;
        }

        // Fallback heuristic when no semantic info
        if (context.SemanticModel == null || symbol == null)
            return true;

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;

        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments
            .Select(a => context.Converter.ConvertExpression(a.Expression))
            .ToList();

        return methodName switch
        {
            "Split" => ConvertSplit(caller, args),
            "Replace" => ConvertReplace(caller, args),
            "StartsWith" => $"{caller}.startsWith({JoinArgs(args)})",
            "EndsWith" => $"{caller}.endsWith({JoinArgs(args)})",
            "Contains" => $"{caller}.includes({JoinArgs(args)})",
            "Substring" => ConvertSubstring(caller, args),
            "IndexOf" => $"{caller}.indexOf({JoinArgs(args)})",
            "LastIndexOf" => $"{caller}.lastIndexOf({JoinArgs(args)})",
            "PadLeft" => ConvertPadLeft(caller, args),
            "PadRight" => ConvertPadRight(caller, args),
            "TrimStart" => $"{caller}.trimStart()",
            "TrimEnd" => $"{caller}.trimEnd()",
            "ToCharArray" => $"[...{caller}]",
            "Insert" => ConvertInsert(caller, args),
            "Remove" => ConvertRemove(caller, args),
            _ => $"{caller}.{ToCamelCase(methodName)}({JoinArgs(args)})"
        };
    }

    private string ConvertSplit(string caller, List<string> args)
    {
        if (args.Count == 0)
            return $"{caller}.split('')";

        // Handle StringSplitOptions.RemoveEmptyEntries
        if (args.Count >= 2 && args[1].Contains("RemoveEmptyEntries"))
            return $"{caller}.split({args[0]}).filter(s => s !== '')";

        return $"{caller}.split({args[0]})";
    }

    private string ConvertReplace(string caller, List<string> args)
    {
        if (args.Count < 2)
            return $"{caller}.replace({JoinArgs(args)})";

        // Use replaceAll for replacing all occurrences (C# Replace behavior)
        return $"{caller}.replaceAll({args[0]}, {args[1]})";
    }

    private string ConvertSubstring(string caller, List<string> args)
    {
        if (args.Count == 1)
        {
            // Substring(startIndex) -> slice(startIndex)
            return $"{caller}.slice({args[0]})";
        }
        if (args.Count >= 2)
        {
            // Substring(startIndex, length) -> substring(startIndex, startIndex + length)
            return $"{caller}.substring({args[0]}, {args[0]} + {args[1]})";
        }
        return $"{caller}.substring()";
    }

    private string ConvertPadLeft(string caller, List<string> args)
    {
        if (args.Count == 1)
            return $"{caller}.padStart({args[0]})";
        if (args.Count >= 2)
            return $"{caller}.padStart({args[0]}, {args[1]})";
        return $"{caller}.padStart()";
    }

    private string ConvertPadRight(string caller, List<string> args)
    {
        if (args.Count == 1)
            return $"{caller}.padEnd({args[0]})";
        if (args.Count >= 2)
            return $"{caller}.padEnd({args[0]}, {args[1]})";
        return $"{caller}.padEnd()";
    }

    private string ConvertInsert(string caller, List<string> args)
    {
        if (args.Count >= 2)
        {
            // str.Insert(index, value) -> str.slice(0, index) + value + str.slice(index)
            return $"({caller}.slice(0, {args[0]}) + {args[1]} + {caller}.slice({args[0]}))";
        }
        return caller;
    }

    private string ConvertRemove(string caller, List<string> args)
    {
        if (args.Count == 1)
        {
            // str.Remove(startIndex) -> str.slice(0, startIndex)
            return $"{caller}.slice(0, {args[0]})";
        }
        if (args.Count >= 2)
        {
            // str.Remove(startIndex, count) -> str.slice(0, startIndex) + str.slice(startIndex + count)
            return $"({caller}.slice(0, {args[0]}) + {caller}.slice({args[0]} + {args[1]}))";
        }
        return caller;
    }

    private string JoinArgs(List<string> args) => string.Join(", ", args);

    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 15; // Higher than InvocationStrategy (1)
}
