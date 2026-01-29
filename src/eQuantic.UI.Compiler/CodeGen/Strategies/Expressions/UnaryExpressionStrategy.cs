using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for unary expressions (prefix and postfix).
/// Handles:
/// - Prefix: ++x, --x, !x, +x, -x
/// - Postfix: x++, x--
/// </summary>
public class UnaryExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is PrefixUnaryExpressionSyntax || node is PostfixUnaryExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        if (node is PrefixUnaryExpressionSyntax prefix)
        {
            var operand = context.Converter.ConvertExpression(prefix.Operand);
            var op = prefix.OperatorToken.Text;
            return $"{op}{operand}";
        }
        
        if (node is PostfixUnaryExpressionSyntax postfix)
        {
            var operand = context.Converter.ConvertExpression(postfix.Operand);
            var op = postfix.OperatorToken.Text;
            return $"{operand}{op}";
        }

        return node.ToString();
    }

    public int Priority => 10;
}
