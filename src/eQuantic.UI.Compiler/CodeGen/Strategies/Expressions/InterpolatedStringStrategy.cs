using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
                    // Use the DECODED value, not the raw source: this collapses doubled braces ({{ -> {,
                    // }} -> }), unescapes verbatim "" -> ", and processes regular escapes — matching .NET's
                    // string value. Then re-escape only what a JS template literal treats specially.
                    sb.Append(EscapeForTemplate(text.TextToken.ValueText));
                    break;
                case InterpolationSyntax interpolation:
                    sb.Append("${");
                    var expr = context.Converter.ConvertExpression(interpolation.Expression);
                    
                    var format = interpolation.FormatClause?.FormatStringToken.ValueText;
                    var alignment = interpolation.AlignmentClause?.Value.ToString();
                    
                    if (format != null || alignment != null)
                    {
                        context.UsedHelpers.Add(Eq.Import);
                        var fmtArg = format != null ? $"'{format}'" : "null";
                        var alignArg = alignment != null ? $", {alignment}" : "";
                        sb.Append($"{Eq.Format}({expr}, {fmtArg}{alignArg})");
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
    /// Prepare decoded interpolated-string text for a JS template literal. First collapse the doubled
    /// braces that escape a literal brace in C# interpolation (<c>{{</c> -> <c>{</c>, <c>}}</c> -> <c>}</c>) —
    /// <c>ValueText</c> leaves these doubled. Then escape what a template literal treats specially: backslash
    /// (first, so we don't double-escape), backtick, and the <c>${</c> opener (done last so a <c>${</c>
    /// produced by the brace collapse, e.g. from <c>$"${{x}}"</c>, is also neutralised).
    /// </summary>
    private static string EscapeForTemplate(string s) =>
        s.Replace("{{", "{").Replace("}}", "}")
         .Replace("\\", "\\\\").Replace("`", "\\`").Replace("${", "\\${");

    public int Priority => 10;
}
