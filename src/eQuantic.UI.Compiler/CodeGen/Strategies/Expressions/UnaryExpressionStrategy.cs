using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Prefix and postfix operators. As IR the operand's own binding is visible, so the writer keeps
/// a negated negation from welding into the DECREMENT operator (<c>- -x</c> is not <c>--x</c>) and
/// parenthesizes an operand looser than the operator.
/// </summary>
public class UnaryExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is PrefixUnaryExpressionSyntax || node is PostfixUnaryExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        if (node is PrefixUnaryExpressionSyntax prefix)
        {
            return JsExpr.Prefix(prefix.OperatorToken.Text,
                context.Converter.ConvertIr(prefix.Operand));
        }

        if (node is PostfixUnaryExpressionSyntax postfix)
        {
            var operand = context.Converter.ConvertIr(postfix.Operand);

            // `x!` asserts non-null to the C# compiler and means nothing at runtime; it survives
            // only where the output is still TypeScript being type-checked.
            if (postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                return context.TypeAnnotations ? JsExpr.Postfix(operand, "!") : operand;

            return JsExpr.Postfix(operand, postfix.OperatorToken.Text);
        }

        return JsExpr.Opaque(context.Unhandled(node, "unary operator"));
    }

    public int Priority => 10;
}
