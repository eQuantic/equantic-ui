using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;

/// <summary>
/// Strategy for ToString method conversion.
/// Handles: x.ToString() → String(x)
/// This is safer than x.toString() in JS because String(x) handles null/undefined gracefully.
/// </summary>
public class ToStringStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        return memberAccess.Name.Identifier.Text == "ToString";
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var caller = context.Converter.ConvertExpression(memberAccess.Expression);

        // WHICH argument is which: C# has ToString(), ToString(format), ToString(provider) and
        // ToString(format, provider), and `ToString(x)` reads the same in source either way. Taking
        // args[0] as the format is how `ToString(CultureInfo.InvariantCulture)` emitted
        // `$eq.text.format(value, CultureInfo.InvariantCulture)` — a name that exists in .NET and
        // in no browser, so the page died with "CultureInfo is not defined" while the server, which
        // runs the C#, was perfectly happy.
        var args = invocation.ArgumentList.Arguments;
        var receiverType = context.SemanticHelper.GetType(memberAccess.Expression);

        // A value the browser holds as DATA reads as its record text, which is what a concatenation
        // already writes it as (StringConversion).
        if (args.Count == 0 && receiverType.UnwrapNullable() is INamedTypeSymbol data && data.TwinIsData())
            return JsExprWriter.Write(StringConversion.ToDotNetString(memberAccess.Expression,
                context.Converter.ConvertIr(memberAccess.Expression), context));

        // A BOOL writes True or False, what a concatenation already writes it as (StringConversion):
        // `String(b)` lowercased it (#381). Its provider changes nothing, and a null bool? is empty.
        // C# still evaluates the provider, after the receiver: one that could have an effect runs,
        // in that order, and one that could not is left out — a named culture, a null, a literal,
        // or a name bound to a local, a parameter or a field. A bare name can be a PROPERTY, whose
        // getter may have one.
        if (receiverType.UnwrapNullable() is { SpecialType: SpecialType.System_Boolean })
        {
            var ignored = args.FirstOrDefault(argument => IsFormatProvider(argument.Expression, context))?.Expression;
            if (ignored is null || IsInert(ignored, context))
                return JsExprWriter.Write(StringConversion.ToDotNetString(memberAccess.Expression,
                    context.Converter.ConvertIr(memberAccess.Expression), context));
            // `$value`: no C# name can take it, so nothing the provider names is shadowed.
            var text = StringConversion.ToDotNetString(memberAccess.Expression, JsExpr.Identifier("$value"), context);
            return JsExprWriter.Write(JsExpr.Template($"(($value) => ({{1}}, {JsExprWriter.Write(text)}))({{0}})",
                [context.Converter.ConvertIr(memberAccess.Expression), context.Converter.ConvertIr(ignored)],
                context.TypeAnnotations));
        }

        var provider = args.FirstOrDefault(argument => IsFormatProvider(argument.Expression, context));
        var formatArg = args.FirstOrDefault(argument => argument != provider);

        // An ENUM crosses as its camelCase key (`Kind.B` → 'b'), or its number for a flags enum, so
        // String() handed back what the BROWSER holds where the server writes the member's name, a
        // word that changed by itself at hydration. It writes its name, or what its format asks for
        // (`D` its number, `X` its hex, `F` its set flags), and a nullable one nothing for null. Its
        // provider is unused, as in .NET, where both overloads that take one are obsolete for that,
        // but C# still evaluates it, after the receiver, in the order the arguments are written: one
        // that could have an effect runs there, and one that could not is left out, as a bool's is.
        if (receiverType.UnwrapNullable() is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            var special = formatArg is not null && !IsGeneralFormat(formatArg.Expression, context);
            string Text(string held, string? format) =>
                format is not null ? Types.EnumShape.Text(enumType, held, context, format)
                : receiverType is INamedTypeSymbol { TypeKind: TypeKind.Enum }
                    ? EnumNameLookup(enumType, memberAccess.Expression, held, context)
                    : Types.EnumShape.Text(enumType, held, context);
            if (provider is null || IsInert(provider.Expression, context))
                return Text(caller, special ? context.Converter.ConvertExpression(formatArg!.Expression) : null);

            var parts = new List<JsExpr> { context.Converter.ConvertIr(memberAccess.Expression) };
            string? formatHole = null;
            var providerHole = "";
            foreach (var argument in args.OrderBy(argument => argument.SpanStart))
            {
                if (argument == provider) providerHole = $"{{{parts.Count}}}";
                else if (special) formatHole = $"{{{parts.Count}}}";
                else continue;
                parts.Add(context.Converter.ConvertIr(argument.Expression));
            }
            return JsExprWriter.Write(JsExpr.Template($"({providerHole}, {Text("{0}", formatHole)})", parts,
                context.TypeAnnotations));
        }

        var invariant = false;
        if (provider is not null)
        {
            if (NamedCulture.IsInvariant(provider.Expression, context))
            {
                invariant = true;
            }
            else if (!NamedCulture.IsCurrent(provider.Expression, context))
            {
                // Never approximate a provider nobody tested: a custom IFormatProvider, or a culture
                // read from a variable, has no counterpart in the Intl subset this framework pins.
                context.Report(node, ConversionSeverity.Error, "EQ2108",
                    "Only CultureInfo.InvariantCulture and CultureInfo.CurrentCulture cross to "
                    + "JavaScript. Format with an explicit specifier — ToString(\"N2\") follows the "
                    + "app's culture on both targets — or convert with the invariant culture.");
                return $"String({caller})";
            }
        }

        // A DATE goes through the formatter whatever it is given, as .NET formats it: a standard
        // specifier from the culture's patterns, a custom picture drawn token by token, and with
        // none, which is `G`, the current culture's general pattern, the current culture and a null
        // being the call with none; the invariant culture writes the invariant patterns. With no
        // specifier it wrote the twin's invariant text, where .NET writes the culture's (found in
        // review, #472). A null DateTime? writes nothing (#388).
        if (IsDateTime(receiverType))
        {
            context.UsedHelpers.Add(Eq.Import);
            var specifier = formatArg is null ? "'G'" : DateSpecifier(formatArg.Expression, context);
            return invariant
                ? $"{Eq.Format}({caller}, {specifier}, undefined, true)"
                : $"{Eq.Format}({caller}, {specifier})";
        }

        if (formatArg is not null)
        {
            var fmt = context.Converter.ConvertExpression(formatArg.Expression);
            context.UsedHelpers.Add(Eq.Import);
            // The alignment slot stays empty: this shape has none, and the invariant flag is what
            // makes the helper stop reading the culture the reader happens to be in. A float and an
            // integer say what they are (FormatKind).
            var kind = FormatKind.Of(receiverType) is { } named ? $", '{named}'" : "";
            return invariant || kind.Length > 0
                ? $"{Eq.Format}({caller}, {fmt}, undefined, {(invariant ? "true" : "undefined")}{kind})"
                : $"{Eq.Format}({caller}, {fmt})";
        }

        if (provider is not null)
        {
            // A provider with NO specifier. JavaScript's `String(x)` is already the invariant
            // rendering of a number, so the invariant ask is answered exactly; the CURRENT culture's
            // general format is not in the tested subset, and asking for it by name is how a page
            // gets digits nobody pinned.
            if (invariant) return RealText(memberAccess.Expression, context) ?? $"String({caller})";

            context.Report(node, ConversionSeverity.Error, "EQ2109",
                "ToString(CultureInfo.CurrentCulture) has no specifier to pin, and the general "
                + "format is outside the tested Intl subset. Name the format — ToString(\"N2\"), "
                + "ToString(\"F1\") — which reads the same on the server and in the browser.");
            return $"String({caller})";
        }

        // A FRACTIONAL number with no culture at all is the quiet one. C# renders it in whatever
        // culture the thread is in — a pt request renders "0,55" from the server — and JavaScript's
        // `String(x)` is always invariant, so the browser re-renders "0.55" over it. Two targets,
        // two answers, from source that looks obviously correct. A warning rather than an error:
        // this compiles in apps today, and the fix is one argument away.
        if (context.SemanticHelper.GetType(memberAccess.Expression).UnwrapNullable() is
            { SpecialType: SpecialType.System_Single or SpecialType.System_Double
                or SpecialType.System_Decimal })
        {
            context.Report(node, ConversionSeverity.Warning, "EQ2110",
                "A fractional number converted with no culture reads differently on each target: "
                + "C# follows the request's culture (a comma, in pt) and JavaScript is always "
                + "invariant. Say which you mean — ToString(CultureInfo.InvariantCulture) for a "
                + "value a machine reads, or ToString(\"N2\") for one a person reads.");
        }

        // A FLOAT prints as the shortest decimal that reads back as the same single — `0.1f + 0.2f`
        // is "0.3", where String() of the same bits would spell the double underneath — and a
        // DOUBLE in .NET's notation, which turns scientific at 1e17 where String() waits for 1e21.
        if (invocation.ArgumentList.Arguments.Count == 0 && RealText(memberAccess.Expression, context) is { } real)
            return real;

        return $"String({caller})";
    }

    /// <summary>
    /// A DateTime's format as .NET reads it: a null or an empty one is <c>G</c>, the general pattern.
    /// A constant says which at build time and a variable at run time, where the formatter took
    /// either for no format at all and wrote the twin's invariant text (found in Copilot's second
    /// round, #472).
    /// </summary>
    private static string DateSpecifier(ExpressionSyntax format, ConversionContext context)
    {
        if (context.SemanticHelper.IsNullConstant(format)) return "'G'";
        if (context.SemanticHelper.TryGetConstantValue(format, out var constant) && constant is string text)
            return text.Length == 0 ? "'G'" : context.Converter.ConvertExpression(format);
        return JsExprWriter.Write(JsExpr.Binary(JsExpr.Group(context.Converter.ConvertIr(format)), "||", JsExpr.Literal("'G'")));
    }

    /// <summary>
    /// Whether an enum's format is the general one, which writes what no format writes: a null, an
    /// empty string, <c>G</c> or <c>g</c>, known at build time. Any other, a variable's included, is
    /// read by the runtime, which throws for one .NET refuses.
    /// </summary>
    internal static bool IsGeneralFormat(ExpressionSyntax format, ConversionContext context) =>
        context.SemanticHelper.IsNullConstant(format)
        || context.SemanticHelper.TryGetConstantValue(format, out var constant) && constant is "" or "G" or "g";

    /// <summary>
    /// Whether a provider an overload ignores can be left out: nothing in it can have an effect. A
    /// literal, a null, a named culture, or a name bound to a local, a parameter or a field; a bare
    /// name can be a PROPERTY, whose getter may have one.
    /// </summary>
    private static bool IsInert(ExpressionSyntax provider, ConversionContext context) =>
        provider is LiteralExpressionSyntax
        || provider is IdentifierNameSyntax && context.SemanticHelper.GetSymbol(provider) is ILocalSymbol or IParameterSymbol or IFieldSymbol
        || NamedCulture.IsInvariant(provider, context) || NamedCulture.IsCurrent(provider, context);

    /// <summary>Whether the receiver is a DateTime, a nullable one's included.</summary>
    private static bool IsDateTime(ITypeSymbol? type) =>
        type.UnwrapNullable()?.ToDisplayString() == "System.DateTime";

    /// <summary>
    /// A float's or a double's text as .NET writes it, a nullable one's included, which is nothing
    /// for a null (<c>Nullable&lt;T&gt;.ToString()</c> is "", where String() spelled "null"): the
    /// same conversion a concatenation takes. Null for any other receiver.
    /// </summary>
    private static string? RealText(ExpressionSyntax receiver, ConversionContext context)
    {
        if (context.SemanticHelper.GetType(receiver).UnwrapNullable()?.SpecialType
            is not (SpecialType.System_Single or SpecialType.System_Double)) return null;
        return Ir.JsExprWriter.Write(
            StringConversion.ToDotNetString(receiver, context.Converter.ConvertIr(receiver), context));
    }

    /// <summary>
    /// The C# member NAME for an enum value. A member named in the source folds to its name
    /// (`Kind.B.ToString()`: the value is known here, so say it), the member the model binds, never a
    /// property that shares a member's name (`settings.Default`); anything else is the runtime's text,
    /// read from the enum's shape: a member's name, a flags combination's set flags, and a value no
    /// member names as its number. A key→name table answered undefined for the last two (#452).
    /// </summary>
    internal static string EnumNameLookup(INamedTypeSymbol enumType, ExpressionSyntax expression,
        string caller, ConversionContext context) =>
        context.SemanticHelper.GetSymbol(expression) is IFieldSymbol { HasConstantValue: true } member
            && SymbolEqualityComparer.Default.Equals(member.ContainingType, enumType)
            ? $"'{member.Name}'"
            : Types.EnumShape.Text(enumType, caller, context);

    /// <summary>
    /// Is this argument the PROVIDER rather than the format? Asked of the model, because the two
    /// overloads are one syntax. Without a model — the playground compiles a buffer alone — the
    /// source's own spelling is the only evidence there is, and `CultureInfo.X` is unambiguous.
    /// </summary>
    private static bool IsFormatProvider(ExpressionSyntax expression, ConversionContext context)
    {
        if (context.SemanticHelper.GetType(expression) is { } type)
        {
            return type.Name == "IFormatProvider"
                || type.AllInterfaces.Any(i => i.ToDisplayString() == "System.IFormatProvider");
        }

        return expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "CultureInfo" } };
    }

    public int Priority => 10;
}
