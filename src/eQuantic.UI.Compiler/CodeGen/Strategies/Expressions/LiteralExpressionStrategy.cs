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
            // For numbers, just use valid text representation
            _ => literal.Token.Text 
        };
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
