using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// A range as a VALUE — `var r = 1..5;` — rather than as an index. Indexing with one is a slice and
/// belongs to <see cref="RangeIndexerStrategy"/>; a range that is stored, passed, or returned has
/// nothing on the other side to receive it, and emitting a `{ start, end }` object for it only made
/// the failure silent. It is reported instead: slice at the point of use.
/// </summary>
public class RangeExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) => node is RangeExpressionSyntax;

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var range = (RangeExpressionSyntax)node;
        context.Report(node, ConversionSeverity.Error, "EQ2004",
            $"`{range}` is a System.Range value, which has no JavaScript translation. "
            + "Index with the range directly (`text[a..b]`) — that becomes a slice — rather than "
            + "storing it and indexing later.");

        var left = range.LeftOperand is null ? "0" : context.Converter.ConvertExpression(range.LeftOperand);
        var right = range.RightOperand is null ? "null" : context.Converter.ConvertExpression(range.RightOperand);
        return $"{{ start: {left}, end: {right} }}";
    }

    public int Priority => 10;
}
