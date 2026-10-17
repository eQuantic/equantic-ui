using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for interpolated strings.
/// Handles: $"Hello {name}" → `Hello ${name}`
/// Supports format specifiers: {val:F2} → format(val, 'F2')
/// </summary>
public class InterpolatedStringStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is InterpolatedStringExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var interpolated = (InterpolatedStringExpressionSyntax)node;
        // A FormattableString holds its format and its arguments, which a culture handed to it later writes
        // again; the browser holds the text the interpolation writes, in the page's culture. Only a call that
        // names the culture where the text is made writes one (InterpolationInACultureStrategy, #839).
        var converted = context.SemanticHelper.GetConvertedType(interpolated);
        if ((converted.IsNamed("System.FormattableString") || converted.IsNamed("System.IFormattable"))
            && !context.InterpolationCultures.ContainsKey(context.SemanticHelper.Original(interpolated)))
        {
            context.Report(interpolated, ConversionSeverity.Error, "EQ2004",
                $"'{converted!.ToDisplayString()}' has no JavaScript translation: the browser holds the text an "
                + "interpolation writes, in the page's culture, not its format and its arguments, so no culture handed "
                + "to it later can write them again. Name the culture where the text is made, with "
                + "FormattableString.Invariant($\"…\") or string.Create(culture, $\"…\"), or write the interpolation "
                + "as a string.");
            return "''";
        }
        var sb = new StringBuilder();
        sb.Append('`');
        
        foreach (var content in interpolated.Contents)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax text:
                    sb.Append(JsStringLiteral.TemplateText(Text(text, interpolated)));
                    break;
                case InterpolationSyntax interpolation:
                    sb.Append("${").Append(Hole(interpolation, context, padded: true)).Append('}');
                    break;
            }
        }
        
        sb.Append('`');
        return sb.ToString();
    }

    /// <summary>
    /// A text part's value: the DECODED value, not the raw source, escapes processed and verbatim
    /// <c>""</c> read as <c>"</c>, matching .NET's string value.
    /// </summary>
    internal static string Text(InterpolatedStringTextSyntax text, InterpolatedStringExpressionSyntax interpolated) =>
        Unbraced(text.TextToken.ValueText, interpolated);

    /// <summary>
    /// A hole's alignment as the number it is, or null for a hole with none. C# takes a constant
    /// expression there and nothing else, so the model has its value whatever the source spells it
    /// with. The source's TEXT was written: <c>{name,Widths.Name}</c> named a member the twin does not
    /// have (its static is <c>name</c>), or a class the module never imported, and the text was never
    /// padded, with no diagnostic (#834). The constant arrives in its OWN type, so a <c>short</c>, a
    /// <c>byte</c> or a <c>char</c>, which C# converts to the int it pads to, is that int too. The text
    /// is what there is only where the model cannot be asked.
    /// </summary>
    internal static string? Alignment(InterpolationSyntax interpolation, ConversionContext context)
    {
        if (interpolation.AlignmentClause?.Value is not { } written) return null;
        return context.SemanticHelper.TryGetConstantValue(written, out var value)
            && value is int or short or ushort or sbyte or byte or char
            ? System.Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture)
                .ToString(System.Globalization.CultureInfo.InvariantCulture)
            : written.ToString();
    }

    /// <summary>
    /// The JavaScript of a hole's text: its value converted as C# converts it, formatted where a format
    /// is written, and padded to its alignment, or, not <paramref name="padded"/>, the text the
    /// alignment pads, for a writer that appends the padding itself (<c>StringBuilder.Append($"…")</c>).
    /// Given a <paramref name="captured"/> name, the hole's value was evaluated already and is held by
    /// it, and only its text is written here (a <c>FormattableString</c>'s holes, InterpolationInACultureStrategy).
    /// </summary>
    internal static string Hole(InterpolationSyntax interpolation, ConversionContext context, bool padded, string? captured = null)
    {
        // A PLAIN hole arrives converted the way C# converts it — null → "", bool →
        // "True", an enum as its member name — because ValueFlow settles a value on
        // its way into text; only a hole with a format or an alignment receives the
        // raw value, for the formatter. A captured one is written as that text here.
        var expr = captured ?? context.Converter.ConvertExpression(interpolation.Expression);
        var format = interpolation.FormatClause?.FormatStringToken.ValueText;
        var alignment = Alignment(interpolation, context);
        
        // An ENUM formats itself, as its ToString(format) does, and the alignment pads
        // that text: it prints its member name, not the camelCase key the browser holds
        // (`$"{Kind.B,5}"` is "    B" on the server), a nullable one included, which is
        // nothing for null, and `D`, `X` or `F` what they ask for, not the name (#452).
        var holeType = context.SemanticHelper.GetType(interpolation.Expression);
        if ((format != null || alignment != null)
            && holeType.UnwrapNullable() is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
        {
            var general = format is null or "" or "G" or "g";
            expr = general && holeType is INamedTypeSymbol { TypeKind: TypeKind.Enum }
                ? Invocation.ToStringStrategy.EnumNameLookup(enumType, interpolation.Expression, expr, context)
                : Types.EnumShape.Text(enumType, expr, context, general ? null : JsStringLiteral.Quote(format!));
            format = null;
            if (alignment == null || !padded)
                return expr;
        }

        // A TUPLE and an ANONYMOUS object are not IFormattable: .NET writes their ToString, ignoring a
        // format, and pads that text. The text is written from the static type (StringConversion), and
        // only the alignment is left for the formatter (#762).
        if ((format != null || alignment != null) && StringConversion.ReachesTheFormatterAsText(holeType))
        {
            expr = JsExprWriter.Write(StringConversion.Of(holeType!, JsExpr.Opaque(expr), context));
            format = null;
            if (alignment == null || !padded)
                return expr;
        }

        if (format == null && alignment == null)
            return captured is null ? expr : JsExprWriter.Write(context.WritesInvariant(interpolation.Parent)
                ? StringConversion.InvariantText(interpolation.Expression, JsExpr.Identifier(captured), context)
                : StringConversion.ToDotNetString(interpolation.Expression, JsExpr.Identifier(captured), context));

        context.UsedHelpers.Add(Eq.Import);
        // The format is a string like any other: `dd 'de' MMMM` closed the quotes it
        // was written between, and Bun refused the module (#520).
        var fmtArg = format != null ? JsStringLiteral.Quote(format) : "null";
        // A number says what it is where a specifier is written: a double takes no
        // `D` (#455), a float writes its own digits (#378), an integer rounds a half
        // away from zero (#393). With none, only a float's text is its own; every
        // other number's is its digits, in the culture in force (#454).
        var kind = format == null ? FormatKind.OfText(holeType) : FormatKind.Of(holeType);
        // The culture in force is the invariant one where a call named it for this interpolation (#839).
        var invariant = context.WritesInvariant(interpolation.Parent);
        var alignArg = alignment != null && padded ? $", {alignment}" : kind != null || invariant ? ", undefined" : "";
        var kindArg = (kind, invariant) switch
        {
            (not null, true) => $", true, '{kind}'",
            (not null, false) => $", undefined, '{kind}'",
            (null, true) => ", true",
            _ => "",
        };
        return $"{Eq.Format}({expr}, {fmtArg}{alignArg}{kindArg})";
    }

    /// <summary>
    /// The text's value, from <c>ValueText</c>, which keeps a doubled brace doubled. In a regular or a
    /// verbatim interpolated string a doubled brace IS one brace (<c>{{</c> is <c>{</c>, <c>}}</c> is
    /// <c>}</c>). In a raw one it is not: a brace is text unless as many of them as the string has
    /// dollars open a hole, so <c>$$$"""a{{b}}"""</c> is <c>a{{b}}</c>, and collapsing it there
    /// wrote <c>a{b}</c> (#520). A <c>${</c> the collapse produces (<c>$"${{x}}"</c>) is text, and
    /// the template's writer escapes it.
    /// </summary>
    private static string Unbraced(string text, InterpolatedStringExpressionSyntax interpolated) =>
        interpolated.StringStartToken.Kind() is SyntaxKind.InterpolatedSingleLineRawStringStartToken
            or SyntaxKind.InterpolatedMultiLineRawStringStartToken
            ? text
            : text.Replace("{{", "{").Replace("}}", "}");

    public int Priority => 10;
}
