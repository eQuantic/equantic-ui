using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        var sb = new StringBuilder();
        sb.Append('`');
        
        foreach (var content in interpolated.Contents)
        {
            switch (content)
            {
                case InterpolatedStringTextSyntax text:
                    // The DECODED value, not the raw source: escapes processed and verbatim "" read as
                    // ", matching .NET's string value, then spelled as a template's text.
                    sb.Append(JsStringLiteral.TemplateText(Unbraced(text.TextToken.ValueText, interpolated)));
                    break;
                case InterpolationSyntax interpolation:
                    sb.Append("${");
                    // A PLAIN hole arrives converted the way C# converts it — null → "", bool →
                    // "True", an enum as its member name — because ValueFlow settles a value on
                    // its way into text; only a hole with a format or an alignment receives the
                    // raw value, for the formatter.
                    var expr = context.Converter.ConvertExpression(interpolation.Expression);
                    var format = interpolation.FormatClause?.FormatStringToken.ValueText;
                    var alignment = interpolation.AlignmentClause?.Value.ToString();
                    
                    if (format != null || alignment != null)
                    {
                        // A FORMATTED enum still prints its member name, not the lowercase wire
                        // value (`$"{Kind.B,5}"` is "    B" on the server): the lookup is here
                        // because the hole handed the formatter the raw value.
                        if (context.SemanticHelper.GetType(interpolation.Expression)
                            is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
                        {
                            expr = Invocation.ToStringStrategy.EnumNameLookup(
                                enumType, interpolation.Expression, expr, context);
                        }
                        context.UsedHelpers.Add(Eq.Import);
                        // The format is a string like any other: `dd 'de' MMMM` closed the quotes it
                        // was written between, and Bun refused the module (#520).
                        var fmtArg = format != null ? JsStringLiteral.Quote(format) : "null";
                        // A float says it is one: its own digits are not the double's (#378). An
                        // integer says so where a specifier is written, since it rounds a half away
                        // from zero (#393); with none, its text is its digits whatever it is.
                        var kind = FormatKind.Of(context.SemanticHelper.GetType(interpolation.Expression));
                        if (FormatKind.IsInteger(kind) && format == null) kind = null;
                        var alignArg = alignment != null ? $", {alignment}" : kind != null ? ", undefined" : "";
                        var kindArg = kind != null ? $", undefined, '{kind}'" : "";
                        sb.Append($"{Eq.Format}({expr}, {fmtArg}{alignArg}{kindArg})");
                    }
                    else
                    {
                        sb.Append(expr);
                    }

                    sb.Append('}');
                    break;
            }
        }
        
        sb.Append('`');
        return sb.ToString();
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
