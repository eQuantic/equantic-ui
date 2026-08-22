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
        "TrimStart", "TrimEnd", "Trim", "ToCharArray", "Insert", "Remove",
        "ToUpper", "ToLower", "ToUpperInvariant", "ToLowerInvariant", "Equals"
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

        // Name decides ONLY where guessing is honest — see ConversionContext.CanGuess. Under an
        // AUTHORITATIVE model, in-tree-but-unbindable is reported (EQ2006), never guessed.
        if (symbol == null && context.CanGuess(node))
            return true;

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;

        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var rawArgs = invocation.ArgumentList.Arguments;

        // A trailing StringComparison argument selects the comparison mode. JS string ops have no such
        // parameter, so we strip it: case-sensitive (Ordinal/InvariantCulture) maps to the plain op;
        // an IgnoreCase variant lower-cases both operands first.
        var hasComparison = rawArgs.Count > 0 && IsStringComparison(rawArgs[^1], context);
        var ignoreCase = hasComparison && rawArgs[^1].Expression.ToString().Contains("IgnoreCase");
        var argNodes = hasComparison ? rawArgs.Take(rawArgs.Count - 1) : rawArgs;
        var args = argNodes.Select(a => context.Converter.ConvertExpression(a.Expression)).ToList();

        // Receiver / operand with optional case folding for IgnoreCase comparisons.
        var lhs = ignoreCase ? $"{caller}.toLowerCase()" : caller;
        string Fold(string a) => ignoreCase ? $"{a}.toLowerCase()" : a;
        // IndexOf/LastIndexOf may carry a trailing startIndex after the search value.
        var extraArgs = args.Count > 1 ? ", " + string.Join(", ", args.Skip(1)) : "";

        return methodName switch
        {
            "Split" => ConvertSplit(caller, args),
            "Replace" => ConvertReplace(caller, args),
            "StartsWith" => $"{lhs}.startsWith({Fold(args[0])})",
            "EndsWith" => $"{lhs}.endsWith({Fold(args[0])})",
            "Contains" => $"{lhs}.includes({Fold(args[0])})",
            "Equals" => $"({Fold(caller)} === {Fold(args[0])})",
            "Substring" => ConvertSubstring(caller, args, context),
            "IndexOf" => $"{lhs}.indexOf({Fold(args[0])}{extraArgs})",
            "LastIndexOf" => $"{lhs}.lastIndexOf({Fold(args[0])}{extraArgs})",
            "PadLeft" => ConvertPadLeft(caller, args),
            "PadRight" => ConvertPadRight(caller, args),
            "TrimStart" => ConvertTrim(caller, args, "start"),
            "TrimEnd" => ConvertTrim(caller, args, "end"),
            "Trim" => ConvertTrim(caller, args, "both"),
            "ToUpper" => $"{caller}.toUpperCase()",
            "ToLower" => $"{caller}.toLowerCase()",
            "ToUpperInvariant" => $"{caller}.toUpperCase()",
            "ToLowerInvariant" => $"{caller}.toLowerCase()",
            "ToCharArray" => $"[...{caller}]",
            "Insert" => ConvertInsert(caller, args),
            "Remove" => ConvertRemove(caller, args),
            _ => $"{caller}.{methodName.ToCamelCase()}({JoinArgs(args)})"
        };
    }

    /// <summary>True when the argument is a <c>System.StringComparison</c> value.</summary>
    private static bool IsStringComparison(ArgumentSyntax arg, ConversionContext context) =>
        context.SemanticHelper.GetType(arg.Expression).IsNamed("System.StringComparison")
        || arg.Expression.ToString().Contains("StringComparison"); // syntax fallback (no semantic model)

    private string ConvertTrim(string caller, List<string> args, string mode)
    {
        // No argument → native whitespace trim.
        if (args.Count == 0)
        {
            return mode switch
            {
                "start" => $"{caller}.trimStart()",
                "end" => $"{caller}.trimEnd()",
                _ => $"{caller}.trim()"
            };
        }

        // Trim specific characters. Char args are JS strings; concatenated they form the trim set
        // (.includes works for the resulting string or an array arg). No regex escaping needed.
        var chars = string.Join(" + ", args);
        return mode switch
        {
            "start" => $"(_s => {{ const _c = {chars}; let _i = 0; while (_i < _s.length && _c.includes(_s[_i])) _i++; return _s.slice(_i); }})({caller})",
            "end" => $"(_s => {{ const _c = {chars}; let _e = _s.length; while (_e > 0 && _c.includes(_s[_e - 1])) _e--; return _s.slice(0, _e); }})({caller})",
            _ => $"(_s => {{ const _c = {chars}; let _i = 0, _e = _s.length; while (_i < _e && _c.includes(_s[_i])) _i++; while (_e > _i && _c.includes(_s[_e - 1])) _e--; return _s.slice(_i, _e); }})({caller})"
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

    /// <summary>
    /// Substring through the runtime, which REFUSES an out-of-range index the way .NET does.
    /// JavaScript's own clamps — `"ab".slice(9)` is "" and `substring` swaps its arguments rather
    /// than complain — so a call that stops a server request kept going in the browser with an
    /// empty string spreading through it. Found by the differential generator, which reached the
    /// shape once it learned to write a try/catch.
    /// </summary>
    private string ConvertSubstring(string caller, List<string> args, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return args.Count switch
        {
            0 => $"{Eq.Substring}({caller}, 0)",
            1 => $"{Eq.Substring}({caller}, {args[0]})",
            _ => $"{Eq.Substring}({caller}, {args[0]}, {args[1]})",
        };
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

    public int Priority => 15; // Higher than InvocationStrategy (1)
}
