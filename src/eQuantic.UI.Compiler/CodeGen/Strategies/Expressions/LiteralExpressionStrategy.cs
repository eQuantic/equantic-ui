using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for literal expressions.
/// Handles: string, int, bool, null literals
/// </summary>
public class LiteralExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is LiteralExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var literal = (LiteralExpressionSyntax)node;
        return literal.Kind() switch
        {
            SyntaxKind.StringLiteralExpression => $"'{EscapeString(literal.Token.ValueText)}'",
            SyntaxKind.TrueLiteralExpression => "true",
            SyntaxKind.FalseLiteralExpression => "false",
            SyntaxKind.NullLiteralExpression => "null",
            SyntaxKind.NumericLiteralExpression => ConvertNumericLiteral(literal.Token.Text, context),
            _ => literal.Token.Text
        };
    }

    private static string ConvertNumericLiteral(string text, ConversionContext context)
    {
        var isHexOrBinary = text.StartsWith("0x") || text.StartsWith("0X")
            || text.StartsWith("0b") || text.StartsWith("0B");

        // decimal literal (1.1m / 1.1M) -> exact Decimal via the $eq.num.dec compat helper.
        if (!isHexOrBinary && text.Length > 0 && (text[^1] == 'm' || text[^1] == 'M'))
        {
            context.UsedHelpers.Add(Eq.Import);
            return $"{Eq.Dec}(\"{text[..^1]}\")";
        }

        // Strip C# numeric type suffixes that aren't valid JS. Hex/binary keep their digits
        // (only L/U are suffixes there); for decimals, F/D are also suffixes, not digits.
        var noSuffix = isHexOrBinary
            ? text.TrimEnd('L', 'l', 'U', 'u')
            : text.TrimEnd('L', 'l', 'U', 'u', 'F', 'f', 'D', 'd');

        // A long/ulong literal (suffix contains L) becomes a BigInt literal for exact 64-bit values.
        var suffix = text[noSuffix.Length..];
        if (suffix.IndexOfAny(new[] { 'L', 'l' }) >= 0)
        {
            return $"{noSuffix}n";
        }

        return noSuffix;
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
