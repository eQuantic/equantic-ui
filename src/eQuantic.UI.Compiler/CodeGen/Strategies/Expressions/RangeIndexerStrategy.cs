using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// A range used to INDEX — <c>line[start..end]</c>, <c>text[..n]</c>, <c>name[^3..]</c>. In C# that
/// is a slice; in JavaScript it is <c>.slice(from, to)</c>, whose negative arguments already carry
/// the meaning <c>^</c> has. So the ordinary shapes cost nothing at runtime.
/// <para>
/// What used to happen is why this exists: the range converted to a <c>{ start, end }</c> object and
/// the element access wrapped it in brackets, producing <c>text[{ start: 0, end: 4 }]</c> — which is
/// <c>undefined</c> at runtime, in every case, with no diagnostic. A tokenizer written in C# and
/// realized on the web drew nothing and said nothing about why.
/// </para>
/// <para>
/// One shape JS cannot say directly: a from-end endpoint that may be ZERO. <c>slice(0, -0)</c> is
/// <c>slice(0, 0)</c> — empty — where C#'s <c>[..^0]</c> means the whole value. Anything but a
/// positive literal after <c>^</c> therefore goes through <c>$eq.slice</c>, which resolves both
/// endpoints against the length exactly as <c>Index.GetOffset</c> does.
/// </para>
/// </summary>
public class RangeIndexerStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is ElementAccessExpressionSyntax { ArgumentList.Arguments: [{ Expression: RangeExpressionSyntax }] };

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var access = (ElementAccessExpressionSyntax)node;
        var range = (RangeExpressionSyntax)access.ArgumentList.Arguments[0].Expression;
        var receiver = context.Converter.ConvertExpression(access.Expression);

        var start = Endpoint(range.LeftOperand, context, isStart: true);
        var end = Endpoint(range.RightOperand, context, isStart: false);

        if (start.Direct is { } from && end.Direct is { } to)
            return $"{receiver}.slice({from}, {to})";
        if (start.Direct is { } only && end.Omitted)
            return $"{receiver}.slice({only})";

        // The endpoint could be zero-from-the-end, where a negative index would mean the opposite.
        return $"$eq.slice({receiver}, {start.Value}, {start.FromEnd.ToString().ToLowerInvariant()}, "
            + $"{end.Value}, {end.FromEnd.ToString().ToLowerInvariant()})";
    }

    /// <summary>One end of the range: what it is, and whether `.slice` can take it as it stands.</summary>
    private readonly record struct RangeEnd(string Value, bool FromEnd, string? Direct, bool Omitted);

    private static RangeEnd Endpoint(ExpressionSyntax? operand, ConversionContext context, bool isStart)
    {
        // Omitted: `..end` starts at 0, `start..` runs to the end.
        if (operand is null)
            return isStart ? new RangeEnd("0", false, "0", false) : new RangeEnd("null", false, null, true);

        if (operand is not PrefixUnaryExpressionSyntax hat || !hat.IsKind(SyntaxKind.IndexExpression))
        {
            var value = context.Converter.ConvertExpression(operand);
            return new RangeEnd(value, false, value, false);
        }

        var offset = context.Converter.ConvertExpression(hat.Operand);
        // `^3` is `-3` to slice — but only once we know it is not `^0`, which slice reads as 0.
        var direct = hat.Operand is LiteralExpressionSyntax { Token.Value: int and > 0 } ? $"-{offset}" : null;
        return new RangeEnd(offset, true, direct, false);
    }

    /// <summary>Above ElementAccessStrategy (1) and IndexFromEndStrategy (20), which claim the same
    /// syntax node for the non-range shapes.</summary>
    public int Priority => 25;
}
