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

            // A DECIMAL is a runtime Decimal object: JavaScript's `-` coerces it through its text
            // into a plain NUMBER, silently shedding the type (`-3.99m` computed on as a double).
            // A constant folds to the negated literal; anything else negates on the type.
            if (prefix.OperatorToken.Text is "-" or "+"
                && context.SemanticHelper.GetType(prefix.Operand).IsDecimal())
            {
                if (prefix.OperatorToken.Text == "+") return context.Converter.ConvertIr(prefix.Operand);
                if (context.SemanticHelper.GetOperation(prefix) is Microsoft.CodeAnalysis.Operations.IUnaryOperation
                    { ConstantValue: { HasValue: true, Value: decimal negated } })
                {
                    context.UsedHelpers.Add(Eq.Import);
                    return JsExpr.Callish($"{Eq.Dec}(\"{negated.ToString(System.Globalization.CultureInfo.InvariantCulture)}\")");
                }
                var negatable = context.Converter.ConvertIr(prefix.Operand);
                return JsExpr.Callish($"{JsExprWriter.WriteIn(negatable, JsPrecedence.Call)}.neg()");
            }

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

        // A DECIMAL steps on the type: JavaScript's own ++ coerces the Decimal through its text
        // into a plain number, shedding the type mid-loop. Postfix IN VALUE POSITION answers the
        // OLD value — recovered exactly (base-10 add/sub of one is exact) instead of binding a temp.
        if (type.IsDecimal())
        {
            context.UsedHelpers.Add(Eq.Import);
            var decimalTarget = context.Converter.ConvertIr(operandSyntax);
            var decimalText = JsExprWriter.WriteIn(decimalTarget, JsPrecedence.Call);
            var method = op == "++" ? "add" : "sub";
            var assigned = JsExpr.Binary(decimalTarget, "=", JsExpr.Callish($"{decimalText}.{method}({Eq.Dec}(1))"));
            if (node is PostfixUnaryExpressionSyntax && ValueUsed(node))
            {
                var inverse = op == "++" ? "sub" : "add";
                return JsExpr.Callish($"({JsExprWriter.Write(assigned)}, {decimalText}.{inverse}({Eq.Dec}(1)))");
            }
            return assigned;
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

    /// <summary>Whether the step's RESULT is read — false in the two places an increment is pure
    /// effect: its own statement, and a for-loop's incrementor slot.</summary>
    private static bool ValueUsed(SyntaxNode node) => node.Parent switch
    {
        ExpressionStatementSyntax => false,
        ForStatementSyntax loop => !loop.Incrementors.Contains(node),
        _ => true,
    };

    public int Priority => 10;
}
