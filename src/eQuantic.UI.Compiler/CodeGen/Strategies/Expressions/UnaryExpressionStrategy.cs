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

            // C#'s null-forgiving `x!` is an ASSERTION, not an operation — it produces no code and
            // means only "I know this is not null". TypeScript spells the same assertion the same
            // way, so it survives there; JavaScript has no such syntax, and `x!.value` is a parse
            // error that costs the whole module rather than the one expression.
            if (postfix.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SuppressNullableWarningExpression))
            {
                return context.TypeAnnotations ? $"{operand}!" : operand;
            }

            return $"{operand}{postfix.OperatorToken.Text}";
        }

        return node.ToString();
    }

    public int Priority => 10;
}
