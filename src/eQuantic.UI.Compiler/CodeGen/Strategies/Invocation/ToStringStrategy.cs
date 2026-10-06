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
        // none, or a null or an empty one, its type's own text, which the formatter knows (`G` of a
        // DateTime, `d` of a DateOnly, `t` of a TimeOnly, and a DateTimeOffset's `G` with its offset),
        // the current culture and a null being the call with none; the invariant culture writes the
        // invariant patterns. A DateTime with no specifier wrote the twin's invariant text where .NET
        // writes the culture's (found in review, #472), and the other three types never reached the
        // formatter at all (#469). A null date writes nothing (#388).
        if (IsDate(receiverType))
        {
            context.UsedHelpers.Add(Eq.Import);
            var specifier = formatArg is null ? "null" : context.Converter.ConvertExpression(formatArg.Expression);
            return invariant
                ? $"{Eq.Format}({caller}, {specifier}, undefined, true)"
                : $"{Eq.Format}({caller}, {specifier})";
        }

        if (formatArg is not null)
        {
            var fmt = context.Converter.ConvertExpression(formatArg.Expression);
            context.UsedHelpers.Add(Eq.Import);
            // The alignment slot stays empty: this shape has none, and the invariant flag is what
            // makes the helper stop reading the culture the reader happens to be in. A number says
            // which it is (FormatKind): a double takes no `D`, a float writes its own digits, an
            // integer rounds a half away from zero.
            var kind = FormatKind.Of(receiverType) is { } named ? $", '{named}'" : "";
            return invariant || kind.Length > 0
                ? $"{Eq.Format}({caller}, {fmt}, undefined, {(invariant ? "true" : "undefined")}{kind})"
                : $"{Eq.Format}({caller}, {fmt})";
        }

        // With no specifier, the INVARIANT culture's text: JavaScript's `String(x)` is already the
        // invariant rendering of an integer and a decimal, and a float's and a double's are .NET's
        // notation (`1E+17`, `-0`, a float's own digits).
        if (invariant) return RealText(memberAccess.Expression, context) ?? $"String({caller})";

        // With no specifier and no culture named, or the current one, or a null: the value's text in
        // the culture in force, which is what a concatenation writes (StringConversion) and what the
        // server writes for the same call (#454). It was the invariant text, under a warning (EQ2110)
        // that the two targets disagreed, and the current culture named with no specifier was refused
        // (EQ2109) because the general format was not pinned; it is now, on both sides.
        return JsExprWriter.Write(StringConversion.ToText(memberAccess.Expression,
            context.Converter.ConvertIr(memberAccess.Expression), context));
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

    /// <summary>Whether the receiver is a date: a DateTime, a DateOnly, a TimeOnly or a DateTimeOffset,
    /// a nullable one's included.</summary>
    private static bool IsDate(ITypeSymbol? type) =>
        type.UnwrapNullable()?.ToDisplayString() is "System.DateTime" or "System.DateOnly"
            or "System.TimeOnly" or "System.DateTimeOffset";

    /// <summary>
    /// A float's or a double's text in the INVARIANT culture, as .NET writes it: the shortest digits
    /// that read back, in .NET's notation (<c>1E+17</c>, <c>-0</c>), a float in its own digits, and
    /// nothing for a null one (<c>Nullable&lt;T&gt;.ToString()</c> is "", where String() spelled
    /// "null"). Null for any other receiver, whose invariant text is <c>String()</c>'s.
    /// </summary>
    private static string? RealText(ExpressionSyntax receiver, ConversionContext context)
    {
        var type = context.SemanticHelper.GetType(receiver);
        var real = type.UnwrapNullable();
        if (real?.SpecialType is not (SpecialType.System_Single or SpecialType.System_Double)) return null;
        context.UsedHelpers.Add(Eq.Import);
        var printer = real.SpecialType == SpecialType.System_Single ? Eq.Single : Eq.Double;
        var converted = context.Converter.ConvertIr(receiver);
        return ReferenceEquals(real, type)
            ? $"{printer}({JsExprWriter.Write(converted)})"
            : JsExprWriter.Write(JsExpr.Template($"({{0}} == null ? '' : {printer}({{0}}))", [converted],
                context.TypeAnnotations));
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
