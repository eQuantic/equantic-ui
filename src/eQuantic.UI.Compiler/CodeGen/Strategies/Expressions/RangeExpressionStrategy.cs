using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// A range as a VALUE — `var r = 1..5;` — rather than as an index. Indexing with one is a slice and
/// belongs to <see cref="RangeIndexerStrategy"/>; a range that is stored, passed, or returned has
/// nothing on the other side to receive it, and emitting a `{ start, end }` object for it only made
/// the failure silent. It is reported instead: slice at the point of use. A range handed to an
/// indexer over System.Range is the same value, and is refused here too (<see cref="RefuseAsAKey"/>).
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

    /// <summary>
    /// Reports a range handed to an indexer that takes the <c>Range</c> itself (<c>this[Range r]</c>): it
    /// would cross as a System.Range value, which has no translation here, into a twin whose indexer
    /// reads members of a value this side never builds (#585). It was written as a call of a
    /// <c>slice</c> the twin does not have, or of a <c>Slice</c> that takes a length beside it.
    /// </summary>
    internal static void RefuseAsAKey(RangeExpressionSyntax range, IPropertySymbol indexer, ConversionContext context) =>
        context.Report(range, ConversionSeverity.Error, "EQ2004",
            $"`{range}` is handed to {indexer.ContainingType.Name}'s indexer over System.Range as a Range value, which has "
            + "no JavaScript translation. Give the type a Length (or a Count) and a Slice(int start, int length) instead: "
            + "a range over those becomes a call of that Slice.");

    public int Priority => 10;
}
