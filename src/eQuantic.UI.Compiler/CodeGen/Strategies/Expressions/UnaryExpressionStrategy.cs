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
            if (prefix.OperatorToken.Text is "++" or "--" && Step(prefix.Operand, prefix.OperatorToken.Text, node, context) is { } stepped)
                return stepped;
            // A USER-DEFINED unary operator on an in-source type calls the twin's static method.
            if (context.SemanticHelper.GetOperation(prefix) is Microsoft.CodeAnalysis.Operations.IUnaryOperation
                { OperatorMethod: { } unaryMethod }
                && UserDefinedOperators.Unary(unaryMethod, prefix.OperatorToken.Text,
                    context.Converter.ConvertExpression(prefix.Operand)) is { } unaryCall)
                return unaryCall;
            return JsExpr.Prefix(prefix.OperatorToken.Text,
                context.Converter.ConvertIr(prefix.Operand));
        }

        if (node is PostfixUnaryExpressionSyntax postfix)
        {
            if (postfix.OperatorToken.Text is "++" or "--" && Step(postfix.Operand, postfix.OperatorToken.Text, node, context) is { } stepped)
                return stepped;
            var operand = context.Converter.ConvertIr(postfix.Operand);

            // `x!` asserts non-null to the C# compiler and means nothing at runtime; it survives
            // only where the output is still TypeScript being type-checked.
            if (postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                return context.TypeAnnotations ? JsExpr.Postfix(operand, "!") : operand;

            return JsExpr.Postfix(operand, postfix.OperatorToken.Text);
        }

        return JsExpr.Opaque(context.Unhandled(node, "unary operator"));
    }

    /// <summary>
    /// An increment that JavaScript's own would get wrong, lowered to an assignment: a CHAR steps
    /// by code unit (`'a'++` is NaN here), a narrow width wraps, and a checked context throws —
    /// the result type decides (IntegerWidth). Null leaves the native `++`, which is what every
    /// loop counter wants. Prefix semantics: the expression's value is the stepped one.
    /// </summary>
    private static JsExpr? Step(ExpressionSyntax operandSyntax, string op, SyntaxNode node, ConversionContext context)
    {
        var type = context.SemanticHelper.GetType(operandSyntax);
        var delta = op == "++" ? "+" : "-";
        if (type is { SpecialType: SpecialType.System_Char })
        {
            var target = context.Converter.ConvertIr(operandSyntax);
            var text = JsExprWriter.WriteIn(target, JsPrecedence.Call);
            return JsExpr.Binary(target, "=", JsExpr.Callish($"String.fromCharCode({text}.charCodeAt(0) {delta} 1)"));
        }
        if (IntegerWidth.Of(type) is not { } width) return null;
        var arithmetic = ArithmeticContext.Of(node, context);
        if (!(arithmetic.IsChecked || arithmetic.ExplicitUnchecked || IntegerWidth.WrapsByDefault(width))) return null;
        var operand = context.Converter.ConvertIr(operandSyntax);
        var one = width.Bits == 64 ? JsExpr.Literal("1n") : JsExpr.Literal("1");
        var stepped = IntegerWidth.Settle(JsExpr.Binary(operand, delta, one), type,
            arithmetic.IsChecked, arithmetic.ExplicitUnchecked, context);
        return JsExpr.Binary(operand, "=", stepped);
    }

    public int Priority => 10;
}
