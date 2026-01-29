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
                    // Escape backticks in template literal
                    sb.Append(text.TextToken.Text.Replace("`", "\\`"));
                    break;
                case InterpolationSyntax interpolation:
                    sb.Append("${");
                    var expr = context.Converter.ConvertExpression(interpolation.Expression);
                    
                    var format = interpolation.FormatClause?.FormatStringToken.ValueText;
                    var alignment = interpolation.AlignmentClause?.Value.ToString();
                    
                    if (format != null || alignment != null)
                    {
                        context.UsedHelpers.Add("format");
                        var fmtArg = format != null ? $"'{format}'" : "null";
                        var alignArg = alignment != null ? $", {alignment}" : "";
                        sb.Append($"format({expr}, {fmtArg}{alignArg})");
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

    public int Priority => 10;
}
