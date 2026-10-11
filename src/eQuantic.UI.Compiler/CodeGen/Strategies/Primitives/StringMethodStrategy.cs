using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Converts C# string instance methods to JavaScript equivalents.
/// Handles:
/// - Split -> $eq.text.split (one separator) or splitAny (a set, white space where there is none),
///   chosen by the bound overload, with its count and options
/// - Replace(old, new) -> replaceAll(old, new)
/// - StartsWith(prefix) -> startsWith(prefix)
/// - EndsWith(suffix) -> endsWith(suffix)
/// - Contains(substring) -> includes(substring)
/// - Substring(start, length?) -> substring(start, start + length) or slice(start)
/// - IndexOf(value) -> indexOf(value); with a start or a count -> $eq.text.indexOf, ordinal
/// - LastIndexOf(value) -> lastIndexOf(value); with a start or a count -> $eq.text.lastIndexOf, ordinal
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

        if (methodName == "Split")
            return SplitCall(invocation, context);
        if (methodName is "IndexOf" or "LastIndexOf" && invocation.ArgumentList.Arguments.Count > 1
            && context.SemanticHelper.GetSymbol(invocation) is null && SpelledSearch(invocation.ArgumentList.Arguments) is null)
            return context.Unhandled(invocation, $"string.{methodName} whose overload the spelling cannot settle");
        // An overload that compares goes to the runtime, chosen by the BOUND method and handed the
        // comparison as the value it is, its member's name, so one held in a variable is the one it
        // holds. It was read from its SPELLING and both sides were lower-cased: the Kelvin sign
        // matched a k, a comparison in a variable was dropped, and so was Replace's (#528).
        if (ComparingShape(invocation, methodName, context) is { } shape)
            return ComparingCall(invocation, methodName, shape, context);
        if (CharSearch(invocation, methodName, context) is { } charSearch)
            return charSearch;

        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments.Select(a => context.Converter.ConvertExpression(a.Expression)).ToList();
        // With no model to ask, IndexOf/LastIndexOf may carry a trailing start and count no spelling tells.
        var extraArgs = args.Count > 1 ? ", " + string.Join(", ", args.Skip(1)) : "";

        return methodName switch
        {
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
    /// <c>$&amp;</c> in its replacement as a pattern and wrote a null one as "null". So are
    /// <c>IndexOf</c> and <c>LastIndexOf</c> of a string from a start, and for a count: JavaScript's
    /// searches take no count, read a start past the end as the end, and read <c>lastIndexOf</c>'s as
    /// the place a match may START, where .NET's is the place it must END by (#724). They search by
    /// ordinal, as the overload without a start does here, where .NET searches by the current culture
    /// (#532). The bound method says which overload it is; with no model to ask, a comparison spelled
    /// as the last argument and the count of the arguments are the only evidence there is, a Replace
    /// of two arguments is the ordinal one whether they are strings or chars, which it answers alike,
    /// and a search is what its spelling settles (<see cref="SpelledSearch"/>), a char's from a start
    /// left to <see cref="CharSearch"/>.
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
                || (methodName == "Replace" && shape == "s,s")
                || (methodName is "IndexOf" or "LastIndexOf" && shape is "s,i" or "s,i,i");
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
            case ("IndexOf" or "LastIndexOf", > 1):
                return SpelledSearch(arguments) is ['s', ..] search ? search : null;
        }
        if (arguments.Count == 0 || !SpellsAComparison(arguments[^1].Expression)) return null;
        return (methodName, arguments.Count) switch
        {
            ("Equals" or "StartsWith" or "EndsWith" or "Contains", 2) => "s,k",
            ("Replace", 3) => "s,s,k",
            _ => null,
        };
    }

    /// <summary>
    /// The shape of an <c>IndexOf</c> or <c>LastIndexOf</c> of two arguments or more, in
    /// <see cref="ComparingShape"/>'s letters, with no model to say which overload it is, or null where
    /// the spelling cannot settle it. After the value, an argument is a start or a count where it is a
    /// number (<see cref="IsANumber"/>), is named as one, or stands where only an int can (the second of
    /// three, the second and third of four); it is a comparison where it is spelled or named as one, or
    /// is the fourth. A name in the second place of two, or the third of three, could hold either, so
    /// the shape is null, and so it is for a named argument out of its parameter's place, which nothing
    /// here moves. A search from a start is a char's where a char literal says so and a string's where
    /// the spelling says so (<see cref="SpellsAString"/>); a value a name holds could be either, and
    /// the two answer differently at the ends of the string (<c>"".LastIndexOf(c, 5)</c> is -1 for a
    /// char and throws for a string), so the shape is null. A search that compares has no char
    /// overload to tell apart: <c>IndexOf(char, StringComparison)</c> answers as the string of one.
    /// </summary>
    private static string? SpelledSearch(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        var count = arguments.Count;
        var tail = new List<char>();
        for (var place = 1; place < count; place++)
        {
            var argument = arguments[place];
            tail.Add(argument.NameColon?.Name.Identifier.ValueText switch
            {
                "startIndex" or "count" => 'i',
                "comparisonType" => 'k',
                not null => '?',
                _ when IsANumber(argument.Expression) => 'i',
                _ when SpellsAComparison(argument.Expression) => 'k',
                _ when count == 4 => place < 3 ? 'i' : 'k',
                _ when count == 3 && place == 1 => 'i',
                _ => '?',
            });
        }
        string[]? names = new string(tail.ToArray()) switch
        {
            "i" => ["value", "startIndex"],
            "ii" => ["value", "startIndex", "count"],
            "k" => ["value", "comparisonType"],
            "ik" => ["value", "startIndex", "comparisonType"],
            "iik" => ["value", "startIndex", "count", "comparisonType"],
            _ => null,
        };
        if (names is null) return null;
        for (var place = 0; place < count; place++)
            if (arguments[place].NameColon?.Name.Identifier.ValueText is { } name && name != names[place]) return null;
        var value = tail.Contains('k') ? 's'
            : arguments[0].Expression.IsKind(SyntaxKind.CharacterLiteralExpression) ? 'c'
            : SpellsAString(arguments[0].Expression) ? 's'
            : '?';
        return value == '?' ? null : string.Join(",", tail.Prepend(value));
    }

    /// <summary>Whether an expression is spelled as a string: a string literal, an interpolated string,
    /// or a concatenation one side of which is. Evidence for the paths with no model only.</summary>
    private static bool SpellsAString(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax parenthesized => SpellsAString(parenthesized.Expression),
        LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.StringLiteralExpression),
        InterpolatedStringExpressionSyntax => true,
        BinaryExpressionSyntax binary => binary.IsKind(SyntaxKind.AddExpression)
            && (SpellsAString(binary.Left) || SpellsAString(binary.Right)),
        _ => false,
    };

    /// <summary>Whether an argument spells a <c>StringComparison</c>, the only evidence of one with no
    /// model to ask.</summary>
    private static bool SpellsAComparison(ExpressionSyntax expression) => expression.ToString().Contains("StringComparison");

    /// <summary>
    /// The runtime's search for an overload that compares. It takes the overload's arguments as C#
    /// lists them, the receiver first, so a call binds nothing to keep them in the order they run;
    /// <c>Replace(string, string)</c> and the searches from a start with no comparison are the ordinal
    /// ones. Equals compares two whole strings, as the static does by the same comparison; a SEARCH
    /// by a culture comparison has no JavaScript form (.NET searches with ICU's collation, and the
    /// platform's collator searches nothing), so a constant one is refused here and one in a variable
    /// throws in the runtime. A <c>CultureInfo</c> has no form this side reads.
    /// </summary>
    private static string ComparingCall(InvocationExpressionSyntax invocation, string methodName, string shape, ConversionContext context)
    {
        var helper = (methodName, shape) switch
        {
            ("Equals", "s,k") => Eq.StringInstanceEquals,
            ("StartsWith", "s,k") => Eq.StringStartsWith,
            ("EndsWith", "s,k") => Eq.StringEndsWith,
            ("Contains", "s,k" or "c,k") => Eq.StringContains,
            ("IndexOf", "s,k" or "c,k" or "s,i,k" or "s,i,i,k" or "s,i" or "s,i,i") => Eq.StringIndexOf,
            ("LastIndexOf", "s,k" or "s,i,k" or "s,i,i,k" or "s,i" or "s,i,i") => Eq.StringLastIndexOf,
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

        // The overloads with no comparison search by ordinal.
        var ordinal = shape is "s,s" or "s,i" or "s,i,i" ? ", 'ordinal'" : "";
        return RuntimeCall(invocation, helper, shape.Split(',').Length, ordinal, bound, context);
    }

    /// <summary>
    /// A char's search with a start, and with a count: ordinal, as every char search is, through the
    /// runtime, which checks the range as .NET 10 checks it. JavaScript's <c>indexOf</c> and
    /// <c>lastIndexOf</c> clamped a start outside the string and take no count, so the count was
    /// dropped (#534). The bound method says it is the char overload; with no model to ask, a char
    /// literal written as the value is the only evidence there is, its start and count placed by
    /// <see cref="SpelledSearch"/>.
    /// </summary>
    private static string? CharSearch(InvocationExpressionSyntax invocation, string methodName, ConversionContext context)
    {
        if (methodName is not ("IndexOf" or "LastIndexOf")) return null;
        var arguments = invocation.ArgumentList.Arguments;
        var bound = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        var isCharSearch = bound is not null
            ? bound.Parameters.Length is 2 or 3
                && bound.Parameters[0].Type.SpecialType == SpecialType.System_Char
                && bound.Parameters.Skip(1).All(parameter => parameter.Type.SpecialType == SpecialType.System_Int32)
            : arguments.Count > 1 && SpelledSearch(arguments) is "c,i" or "c,i,i";
        if (!isCharSearch) return null;
        var helper = methodName == "IndexOf" ? Eq.StringIndexOfChar : Eq.StringLastIndexOfChar;
        return RuntimeCall(invocation, helper, bound?.Parameters.Length ?? arguments.Count, "", bound, context);
    }

    /// <summary>
    /// A call of the runtime's <paramref name="helper"/> with the receiver first and the overload's
    /// <paramref name="arity"/> arguments after it, as C# lists them, then <paramref name="extra"/>.
    /// A named argument goes to its parameter's place and still runs where it is written, so a call
    /// binds nothing to keep the order its arguments run in.
    /// </summary>
    private static string RuntimeCall(InvocationExpressionSyntax invocation, string helper, int arity, string extra,
        IMethodSymbol? bound, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var parameters = Enumerable.Range(0, arity).Select(slot => $"{{{slot}}}");
        var template = $"{helper}({{R}}, {string.Join(", ", parameters)}{extra})";
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        var parts = new List<JsExpr> { context.Converter.ConvertIr(access.Expression) };
        parts.AddRange(invocation.ArgumentList.Arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)));
        if (bound is not null)
            template = PrimitiveStaticStrategy.BindNamedArguments(template, invocation, bound);
        // The holes now name WRITTEN arguments; the receiver is the first part, so each moves by one.
        template = Regex.Replace(template, @"\{(\d)\}", hole => "{" + (int.Parse(hole.Groups[1].Value) + 1) + "}")
            .Replace("{R}", "{0}");
        return JsExprWriter.Write(JsExpr.Template(template, parts));
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

    /// <summary>
    /// <c>string.Split</c> by the overload C# bound, through the runtime's twin of .NET's own: one
    /// separator (a char or a string) goes to <see cref="Eq.StringSplit"/>, a set of them (a char
    /// array, a span of chars or a string array, white space where it is null or empty) to
    /// <see cref="Eq.StringSplitAny"/>, each with its count and its options in their places, a named
    /// one included, as the values they are. JavaScript's <c>split</c> was handed the first argument
    /// whole: an array was one text joined by commas, a null was the text "null", the count was
    /// dropped, and the options were read from their spelling, so one in a variable was lost (#753).
    /// The chars a params parameter takes one by one, <c>Split(',', ';')</c>, are an array of them.
    /// </summary>
    private static string SplitCall(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        var receiver = context.Converter.ConvertIr(((MemberAccessExpressionSyntax)invocation.Expression).Expression);
        var arguments = invocation.ArgumentList.Arguments;
        context.UsedHelpers.Add(Eq.Import);
        if (context.SemanticHelper.GetSymbol(invocation) is not IMethodSymbol { Parameters.Length: > 0 } method)
            return GuessedSplit(invocation, receiver, context);

        var separator = method.Parameters[0];
        var helper = separator.Type.SpecialType is SpecialType.System_Char or SpecialType.System_String
            ? Eq.StringSplit
            : Eq.StringSplitAny;
        if (separator.IsParams && PassesOneByOne(invocation, method, context))
        {
            // The params overloads take nothing else, so every argument is one of the separators.
            var parts = new List<JsExpr> { receiver };
            parts.AddRange(arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)));
            var holes = string.Join(", ", Enumerable.Range(1, arguments.Count).Select(index => "{" + index + "}"));
            var template = arguments.Count == 0 ? $"{helper}({{0}})" : $"{helper}({{0}}, [{holes}])";
            return JsExprWriter.Write(JsExpr.Template(template, parts));
        }
        if (separator.Type is not (IArrayTypeSymbol or { SpecialType: SpecialType.System_Char or SpecialType.System_String })
            && ParameterTemplate.Filling(invocation, method, 0)?.Expression is not CollectionExpressionSyntax)
            return context.Unhandled(invocation, "string.Split over a span, which JavaScript does not have");

        // The count and the options where the overload has them and the call passes them; a count
        // left out before the options is the twin's own default.
        var passed = ParameterTemplate.WrittenFor(arguments, method);
        string Hole(Func<IParameterSymbol, bool> takes) =>
            method.Parameters.FirstOrDefault(takes) is { } parameter && passed[parameter.Ordinal] >= 0
                ? "{" + (parameter.Ordinal + 1) + "}"
                : "undefined";
        var tail = new List<string>
        {
            Hole(parameter => parameter.Type.SpecialType == SpecialType.System_Int32),
            Hole(parameter => parameter.Type.IsNamed("System.StringSplitOptions")),
        };
        while (tail.Count > 0 && tail[^1] == "undefined") tail.RemoveAt(tail.Count - 1);
        var call = $"{helper}({string.Join(", ", new[] { "{0}", "{1}" }.Concat(tail))})";
        return JsExprWriter.Write(ParameterTemplate.Call(call, receiver, invocation, method, context));
    }

    /// <summary>Whether the call hands a params parameter its separators one by one (its expanded
    /// form), which the bound tree says; with no operation to ask, any count of arguments but one, or
    /// one that is a char, is that form.</summary>
    private static bool PassesOneByOne(InvocationExpressionSyntax invocation, IMethodSymbol method, ConversionContext context)
    {
        if (context.SemanticHelper.GetOperation(invocation) is IInvocationOperation operation)
            return operation.Arguments.Any(argument => SymbolEqualityComparer.Default.Equals(argument.Parameter, method.Parameters[0])
                && argument.ArgumentKind is ArgumentKind.ParamArray or ArgumentKind.ParamCollection);
        var arguments = invocation.ArgumentList.Arguments;
        return arguments.Count != 1
            || context.SemanticHelper.GetType(arguments[0].Expression)?.SpecialType == SpecialType.System_Char;
    }

    /// <summary>
    /// <c>string.Split</c> with no model to say which overload it is. No argument is white space; chars
    /// written one by one are a set of them, and so are more than three arguments, which only the
    /// params overload takes. Otherwise the separator is a set where it is spelled as an array
    /// (<see cref="SpelledAsArray"/>) and the one separator where it is a char literal or spelled as a
    /// string (<see cref="SpellsAString"/>); after it, the options are spelled or named as such and a
    /// count is a number or named as one (<see cref="IsANumber"/>), and three arguments are a
    /// separator, a count and options where the separator is an array or either of the others says
    /// what it is. A name could hold a char, a string or a set of them, a count or options, and three
    /// names could be three chars, so what the spelling cannot place is a build error rather than a
    /// guess, as <c>string.Format</c>'s first argument is; so is a named argument out of its
    /// parameter's place, which nothing here moves.
    /// </summary>
    private static string GuessedSplit(InvocationExpressionSyntax invocation, JsExpr receiver, ConversionContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        string? Name(int place) => arguments[place].NameColon?.Name.Identifier.ValueText;
        var inPlace = Enumerable.Range(0, arguments.Count).All(place => Name(place) switch
        {
            null => true,
            "separator" => place == 0,
            "count" => place == 1,
            "options" => place > 0 && place == arguments.Count - 1,
            _ => false,
        });
        if (!inPlace)
            return context.Unhandled(invocation, "string.Split with a named argument out of its place, which no model moves");
        bool IsCount(int place) => Name(place) == "count" || IsANumber(arguments[place].Expression);
        bool IsOptions(int place) => Name(place) == "options" || SpelledOptions(arguments[place].Expression) is not null;

        // Options spelled as members are the number they make: with no model a member is its key.
        var parts = new List<JsExpr> { receiver };
        parts.AddRange(arguments.Select(argument => SpelledOptions(argument.Expression) is { } options
            ? JsExpr.Literal(options.ToString(System.Globalization.CultureInfo.InvariantCulture))
            : context.Converter.ConvertIr(argument.Expression)));
        var holes = Enumerable.Range(1, arguments.Count).Select(index => "{" + index + "}").ToList();
        var isSet = arguments.Count > 0 && SpelledAsArray(arguments[0].Expression);
        var isOne = arguments.Count > 0 && (arguments[0].Expression.IsKind(SyntaxKind.CharacterLiteralExpression)
            || SpellsAString(arguments[0].Expression));
        var helper = isSet ? Eq.StringSplitAny : Eq.StringSplit;
        var template = arguments.Count switch
        {
            0 => $"{Eq.StringSplitAny}({{0}})",
            > 3 => $"{Eq.StringSplitAny}({{0}}, [{string.Join(", ", holes)}])",
            > 1 when arguments.All(argument => argument.Expression.IsKind(SyntaxKind.CharacterLiteralExpression)) =>
                $"{Eq.StringSplitAny}({{0}}, [{string.Join(", ", holes)}])",
            _ when !isSet && !isOne => null,
            1 => $"{helper}({{0}}, {{1}})",
            2 when IsOptions(1) => $"{helper}({{0}}, {{1}}, undefined, {{2}})",
            2 when IsCount(1) => $"{helper}({{0}}, {{1}}, {{2}})",
            3 when isSet || IsCount(1) || IsOptions(2) => $"{helper}({{0}}, {{1}}, {{2}}, {{3}})",
            _ => null,
        };
        return template is null
            ? context.Unhandled(invocation, "string.Split whose separator, count and options the spelling cannot tell apart")
            : JsExprWriter.Write(JsExpr.Template(template, parts));
    }

    /// <summary>Whether an expression is spelled as an array: one created, a collection expression, or a
    /// value cast, defaulted or converted with <c>as</c> to an array type. <c>s.Split(null)</c> is
    /// ambiguous in C#, so <c>(char[])null</c> is how a set left to white space is written.</summary>
    private static bool SpelledAsArray(ExpressionSyntax expression)
    {
        static bool IsArray(TypeSyntax type) => (type is NullableTypeSyntax nullable ? nullable.ElementType : type) is ArrayTypeSyntax;
        return expression switch
        {
            ParenthesizedExpressionSyntax parenthesized => SpelledAsArray(parenthesized.Expression),
            ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax or CollectionExpressionSyntax => true,
            CastExpressionSyntax cast => IsArray(cast.Type),
            DefaultExpressionSyntax @default => IsArray(@default.Type),
            BinaryExpressionSyntax { Right: TypeSyntax type } binary => binary.IsKind(SyntaxKind.AsExpression) && IsArray(type),
            _ => false,
        };
    }

    /// <summary>The value of <c>StringSplitOptions</c> members spelled out and joined by <c>|</c>, or null
    /// where the spelling is anything else.</summary>
    private static int? SpelledOptions(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax parenthesized => SpelledOptions(parenthesized.Expression),
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.BitwiseOrExpression) =>
            SpelledOptions(binary.Left) | SpelledOptions(binary.Right),
        MemberAccessExpressionSyntax member when member.Expression.ToString() is "StringSplitOptions" or "System.StringSplitOptions" =>
            member.Name.Identifier.ValueText switch
            {
                "None" => 0,
                "RemoveEmptyEntries" => 1,
                "TrimEntries" => 2,
                _ => null,
            },
        _ => null,
    };

    /// <summary>Whether an argument is a number a name could not be: a numeric literal, its negation, or
    /// arithmetic, which a <c>StringComparison</c> or a <c>StringSplitOptions</c> held in a variable
    /// never is. Evidence for the paths with no model only.</summary>
    private static bool IsANumber(ExpressionSyntax expression) => expression switch
    {
        ParenthesizedExpressionSyntax parenthesized => IsANumber(parenthesized.Expression),
        LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.NumericLiteralExpression),
        PrefixUnaryExpressionSyntax unary => unary.IsKind(SyntaxKind.UnaryMinusExpression) && IsANumber(unary.Operand),
        BinaryExpressionSyntax binary => binary.Kind() is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression
            or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression,
        _ => false,
    };

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
