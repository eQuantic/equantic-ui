using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Literals in their JavaScript spelling: single-quoted strings, <c>true</c>/<c>false</c>/<c>null</c>,
/// numbers with the C# type suffixes stripped (<c>L</c> becomes a BigInt literal, <c>m</c> an exact
/// Decimal through the runtime helper).
/// </summary>
public class LiteralExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is LiteralExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var literal = (LiteralExpressionSyntax)node;
        return literal.Kind() switch
        {
            SyntaxKind.StringLiteralExpression => JsExpr.Literal($"'{EscapeString(literal.Token.ValueText)}'"),
            SyntaxKind.TrueLiteralExpression => JsExpr.Literal("true"),
            SyntaxKind.FalseLiteralExpression => JsExpr.Literal("false"),
            SyntaxKind.NullLiteralExpression => JsExpr.Literal("null"),
            SyntaxKind.NumericLiteralExpression => ConvertNumericLiteral(literal.Token, context),
            _ => JsExpr.Literal(literal.Token.Text)
        };
    }

    private static JsExpr ConvertNumericLiteral(SyntaxToken token, ConversionContext context)
    {
        var text = token.Text;
        var isHexOrBinary = text.StartsWith("0x") || text.StartsWith("0X")
            || text.StartsWith("0b") || text.StartsWith("0B");

        // decimal literal (1.1m / 1.1M) -> exact Decimal via the $eq.num.dec compat helper.
        if (!isHexOrBinary && text.Length > 0 && (text[^1] == 'm' || text[^1] == 'M'))
        {
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Callish($"{Eq.Dec}(\"{text[..^1]}\")");
        }

        // Strip C# numeric type suffixes that aren't valid JS. Hex/binary keep their digits
        // (only L/U are suffixes there); for decimals, F/D are also suffixes, not digits.
        var noSuffix = isHexOrBinary
            ? text.TrimEnd('L', 'l', 'U', 'u')
            : text.TrimEnd('L', 'l', 'U', 'u', 'F', 'f', 'D', 'd');

        // A long/ulong literal becomes a BigInt literal for exact 64-bit values. The TYPE decides,
        // which the parser has already given the token's value: a literal written with no suffix is
        // a long when int and uint cannot hold it, so `long ticks = 637000000000000000;` is a long
        // with no `L` in sight, and as a plain number it lost its low digits and threw a TypeError
        // at the first arithmetic with another long.
        var suffix = text[noSuffix.Length..];
        if (token.Value is long or ulong)
        {
            return JsExpr.Literal($"{noSuffix}n");
        }

        // A float literal is SINGLE precision: `0.1f` is not 0.1 but the nearest float, and a
        // double that receives it keeps that value — `(double)0.1f > 0.1` is true in C#. Only a
        // literal single precision actually CHANGES is wrapped: `10f` and `0.5f` are exact.
        if (suffix.IndexOfAny(new[] { 'F', 'f' }) >= 0
            && double.TryParse(noSuffix, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var asDouble)
            && (double)(float)asDouble != asDouble)
        {
            return JsExpr.Callish($"Math.fround({noSuffix})");
        }

        return JsExpr.Literal(noSuffix);
    }

    private static string EscapeString(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
    }

    public int Priority => 10;
}
