using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Converts C# string instance methods to JavaScript equivalents.
/// Handles:
/// - Split(separator) -> split(separator); Split() -> $eq.text.splitOnWhiteSpace, .NET's white space
/// - Replace(old, new) -> replaceAll(old, new)
/// - StartsWith(prefix) -> startsWith(prefix)
/// - EndsWith(suffix) -> endsWith(suffix)
/// - Contains(substring) -> includes(substring)
/// - Substring(start, length?) -> substring(start, start + length) or slice(start)
/// - IndexOf(value) -> indexOf(value)
/// - LastIndexOf(value) -> lastIndexOf(value)
/// - PadLeft(width, char?) -> padStart(width, char)
/// - PadRight(width, char?) -> padEnd(width, char)
/// - Trim(), TrimStart(), TrimEnd() -> $eq.text.trim/trimStart/trimEnd, .NET's white space
/// - an overload that compares (a StringComparison, a CultureInfo) and Replace(string, string) ->
///   the runtime's searches, chosen by the bound method
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

        // An overload that compares goes to the runtime, chosen by the BOUND method and handed the
        // comparison as the value it is, its member's name, so one held in a variable is the one it
        // holds. It was read from its SPELLING and both sides were lower-cased: the Kelvin sign
        // matched a k, a comparison in a variable was dropped, and so was Replace's (#528).
        if (ComparingShape(invocation, methodName, context) is { } shape)
            return ComparingCall(invocation, methodName, shape, context);

        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments.Select(a => context.Converter.ConvertExpression(a.Expression)).ToList();
        // IndexOf/LastIndexOf may carry a trailing startIndex after the search value.
        var extraArgs = args.Count > 1 ? ", " + string.Join(", ", args.Skip(1)) : "";

        return methodName switch
        {
            "Split" => ConvertSplit(caller, args, context),
            "Replace" => ConvertReplace(caller, args),
            "StartsWith" => $"{caller}.startsWith({args[0]})",
            "EndsWith" => $"{caller}.endsWith({args[0]})",
            "Contains" => $"{caller}.includes({args[0]})",
            "Equals" => $"({caller} === {args[0]})",
            "Substring" => ConvertSubstring(caller, args, context),
            "IndexOf" => $"{caller}.indexOf({args[0]}{extraArgs})",
            "LastIndexOf" => $"{caller}.lastIndexOf({args[0]}{extraArgs})",
            "PadLeft" => ConvertPadLeft(caller, args),
            "PadRight" => ConvertPadRight(caller, args),
            "TrimStart" => ConvertTrim(caller, args, "start", context),
            "TrimEnd" => ConvertTrim(caller, args, "end", context),
            "Trim" => ConvertTrim(caller, args, "both", context),
            "ToUpper" => $"{caller}.toUpperCase()",
            "ToLower" => $"{caller}.toLowerCase()",
            "ToUpperInvariant" => $"{caller}.toUpperCase()",
            "ToLowerInvariant" => $"{caller}.toLowerCase()",
            // Its chars, the UTF-16 code units: a spread gives code points, one where .NET has two (#524).
            // The (startIndex, length) overload takes its range, refused where it leaves the string,
            // as .NET refuses it: `slice` clamped a negative start and a long length in silence.
            "ToCharArray" => args.Count == 2
                ? CharRange(caller, args, context)
                : $"{caller}.split('')",
            "Insert" => ConvertInsert(caller, args),
            "Remove" => ConvertRemove(caller, args),
            _ => $"{caller}.{methodName.ToCamelCase()}({JoinArgs(args)})"
        };
    }

    /// <summary>
    /// The shape of an overload that compares, one letter per parameter (s a string, c a char, i an
    /// int, b a bool, k a <c>StringComparison</c>, u a <c>CultureInfo</c>), or null for one that does
    /// not. <c>Replace(string, string)</c> is one: it is ordinal, and <c>replaceAll</c> read
    /// <c>$&amp;</c> in its replacement as a pattern and wrote a null one as "null". The bound method
    /// says which overload it is; with no model to ask, a comparison spelled as the last argument and
    /// the count of the arguments are the only evidence there is, and a Replace of two arguments is
    /// the ordinal one whether they are strings or chars, which it answers alike.
    /// </summary>
    private static string? ComparingShape(InvocationExpressionSyntax invocation, string methodName, ConversionContext context)
    {
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method)
        {
            var shape = string.Join(",", method.Parameters.Select(parameter => parameter.Type switch
            {
                { SpecialType: SpecialType.System_String } => "s",
                { SpecialType: SpecialType.System_Char } => "c",
                { SpecialType: SpecialType.System_Int32 } => "i",
                { SpecialType: SpecialType.System_Boolean } => "b",
                var type when type.IsNamed("System.StringComparison") => "k",
                var type when type.IsNamed("System.Globalization.CultureInfo") => "u",
                _ => "?",
            }));
            // A CultureInfo makes a COMPARING overload of these three, and nothing else's: ToUpper's
            // and ToLower's culture is their casing, which is theirs to read.
            var comparing = shape.Contains('k')
                || (shape.Contains('u') && methodName is "StartsWith" or "EndsWith" or "Replace")
                || (methodName == "Replace" && shape == "s,s");
            return comparing ? shape : null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        switch (methodName, arguments.Count)
        {
            case ("Replace", 2):
                return "s,s";
            // The overloads that take a CultureInfo are the only ones of these arities, refused below
            // as they are with a model.
            case ("StartsWith" or "EndsWith", 3):
                return "s,b,u";
            case ("Replace", 4):
                return "s,s,b,u";
        }
        if (arguments.Count == 0 || !arguments[^1].Expression.ToString().Contains("StringComparison")) return null;
        return (methodName, arguments.Count) switch
        {
            ("Equals" or "StartsWith" or "EndsWith" or "Contains" or "IndexOf" or "LastIndexOf", 2) => "s,k",
            ("IndexOf" or "LastIndexOf", 3) => "s,i,k",
            ("IndexOf" or "LastIndexOf", 4) => "s,i,i,k",
            ("Replace", 3) => "s,s,k",
            _ => null,
        };
    }

    /// <summary>
    /// The runtime's search for an overload that compares. It takes the overload's arguments as C#
    /// lists them, the receiver first, so a call binds nothing to keep them in the order they run;
    /// <c>Replace(string, string)</c> is the ordinal one. Equals compares two whole strings, as the
    /// static does by the same comparison; a SEARCH by a culture comparison has no JavaScript form
    /// (.NET searches with ICU's collation, and the platform's collator searches nothing), so a
    /// constant one is refused here and one in a variable throws in the runtime. A <c>CultureInfo</c>
    /// has no form this side reads.
    /// </summary>
    private static string ComparingCall(InvocationExpressionSyntax invocation, string methodName, string shape, ConversionContext context)
    {
        var helper = (methodName, shape) switch
        {
            ("Equals", "s,k") => Eq.StringInstanceEquals,
            ("StartsWith", "s,k") => Eq.StringStartsWith,
            ("EndsWith", "s,k") => Eq.StringEndsWith,
            ("Contains", "s,k" or "c,k") => Eq.StringContains,
            ("IndexOf", "s,k" or "c,k" or "s,i,k" or "s,i,i,k") => Eq.StringIndexOf,
            ("LastIndexOf", "s,k" or "s,i,k" or "s,i,i,k") => Eq.StringLastIndexOf,
            ("Replace", "s,s,k" or "s,s") => Eq.StringReplace,
            _ => null,
        };
        // The label names the fix, since the refusal is where a developer meets it.
        if (helper is null)
            return context.Unhandled(invocation,
                $"string.{methodName} (a CultureInfo has no search in the browser: pass StringComparison.Ordinal or OrdinalIgnoreCase)");
        var bound = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        if (methodName != "Equals" && shape.EndsWith('k')
            && ComparisonArgument(invocation, bound) is { } comparison && IsCultureConstant(comparison, context))
            return context.Unhandled(invocation,
                $"string.{methodName} (a culture comparison has no search in the browser: search by Ordinal or OrdinalIgnoreCase)");

        context.UsedHelpers.Add(Eq.Import);
        var parameters = Enumerable.Range(0, shape.Split(',').Length).Select(slot => $"{{{slot}}}");
        var template = $"{helper}({{R}}, {string.Join(", ", parameters)}{(shape == "s,s" ? ", 'ordinal'" : "")})";
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        var parts = new List<JsExpr> { context.Converter.ConvertIr(access.Expression) };
        parts.AddRange(invocation.ArgumentList.Arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)));
        if (bound is not null)
            template = PrimitiveStaticStrategy.BindNamedArguments(template, invocation, bound);
        // The holes now name WRITTEN arguments; the receiver is the first part, so each moves by one.
        template = Regex.Replace(template, @"\{(\d)\}", hole => "{" + (int.Parse(hole.Groups[1].Value) + 1) + "}")
            .Replace("{R}", "{0}");
        return JsExprWriter.Write(JsExpr.Template(template, parts, context.TypeAnnotations));
    }

    /// <summary>The written argument bound to the comparison parameter. A named one may be written
    /// anywhere, so the last argument is not it: <c>IndexOf(value: b, comparisonType: c, startIndex: 1)</c>
    /// read the start as the comparison. With no model, the comparison is the one spelled last.</summary>
    private static ArgumentSyntax? ComparisonArgument(InvocationExpressionSyntax invocation, IMethodSymbol? method)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (method is null) return arguments.Count > 0 ? arguments[^1] : null;
        var parameter = method.Parameters.FirstOrDefault(p => p.Type.IsNamed("System.StringComparison"));
        if (parameter is null) return null;
        for (var i = 0; i < arguments.Count; i++)
        {
            var name = arguments[i].NameColon?.Name.Identifier.ValueText;
            if (name is null ? i == parameter.Ordinal : name == parameter.Name) return arguments[i];
        }
        return null;
    }

    /// <summary>Whether the comparison is a constant one of the four culture members, the only ones
    /// below <c>Ordinal</c> (4). Where the model can be asked its answer stands: a comparison that is
    /// not a constant, a variable or a conditional between two, reaches the runtime, which throws for
    /// a culture one when it arrives. With no model, only a culture member written as the argument
    /// itself is one; a conditional that names one in an arm is not.</summary>
    private static bool IsCultureConstant(ArgumentSyntax argument, ConversionContext context)
    {
        if (context.SemanticHelper.KnowsOrMapped(argument.Expression))
            return context.SemanticHelper.TryGetConstantValue(argument.Expression, out var value)
                && value is int and >= 0 and < 4;
        var expression = argument.Expression;
        while (expression is ParenthesizedExpressionSyntax parenthesized) expression = parenthesized.Expression;
        return expression is MemberAccessExpressionSyntax
        {
            Name.Identifier.ValueText: "CurrentCulture" or "CurrentCultureIgnoreCase" or "InvariantCulture" or "InvariantCultureIgnoreCase",
            Expression: IdentifierNameSyntax { Identifier.ValueText: "StringComparison" }
                or MemberAccessExpressionSyntax { Name.Identifier.ValueText: "StringComparison" },
        };
    }

    /// <summary>
    /// Trim and its two halves, through the runtime: with no argument, .NET's white space, which
    /// JavaScript's <c>trim</c> is not (it leaves U+0085 NEXT LINE and takes U+FEFF); with characters,
    /// those, one char or an array of them, where a null or an empty array is white space again. The
    /// string and the characters go in as ARGUMENTS, evaluated once where C# evaluates them, the string
    /// first. The characters were written inside an arrow around the string, which an <c>await</c>
    /// among them could not parse in (#539).
    /// </summary>
    private string ConvertTrim(string caller, List<string> args, string mode, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var helper = mode switch
        {
            "start" => Eq.TrimStart,
            "end" => Eq.TrimEnd,
            _ => Eq.Trim,
        };
        // Several chars are C#'s params array, written out: `Trim('a', 'b')` is `Trim(new[] { 'a', 'b' })`.
        var chars = args.Count switch
        {
            0 => "",
            1 => ", " + args[0],
            _ => ", [" + string.Join(", ", args) + "]",
        };
        return $"{helper}({caller}{chars})";
    }

    private string ConvertSplit(string caller, List<string> args, ConversionContext context)
    {
        // No separator splits on .NET's white space, keeping the empty entries between two of it.
        // It was `split('')`, which cut the text into its characters.
        if (args.Count == 0)
        {
            context.UsedHelpers.Add(Eq.Import);
            return $"{Eq.SplitOnWhiteSpace}({caller})";
        }

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

    /// <summary><c>ToCharArray(startIndex, length)</c>: the range through the runtime, which refuses
    /// one that leaves the string.</summary>
    private static string CharRange(string caller, IReadOnlyList<string> args, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return $"{Eq.TextChars}({caller}, {args[0]}, {args[1]})";
    }

    public int Priority => 15; // Higher than InvocationStrategy (1)
}
