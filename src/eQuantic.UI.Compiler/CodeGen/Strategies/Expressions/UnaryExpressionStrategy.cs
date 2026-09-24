using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Extensions;
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
            // A USER-DEFINED unary operator on an in-source type calls the twin's static method —
            // and a host-only one stops here rather than emitting JavaScript's own operator.
            if (context.SemanticHelper.GetOperation(prefix) is Microsoft.CodeAnalysis.Operations.IUnaryOperation
                { OperatorMethod: { } unaryMethod })
            {
                if (unaryMethod.ReportIfHostOnly(prefix, context)) return JsExpr.Callish("undefined");
                if (UserDefinedOperators.Unary(unaryMethod, prefix.OperatorToken.Text,
                        context.Converter.ConvertExpression(prefix.Operand)) is { } unaryCall)
                    return unaryCall;
            }

            // A NULLABLE number negates its value inside the lift (#372): JavaScript's `-null` is -0,
            // `~null` is -1 and `+null` is 0, where C#'s lifted operator answers null. `+` is the
            // value itself, which a BigInt needs: JavaScript's `+5n` throws.
            if (prefix.OperatorToken.Text is "-" or "~" or "+"
                && NullableLift.IsNullableNumber(context.SemanticHelper.GetType(prefix.Operand), out var liftedValue))
            {
                var text = prefix.OperatorToken.Text;
                return NullableLift.Unary(context.Converter.ConvertIr(prefix.Operand), value => text switch
                {
                    "+" => value,
                    "-" when liftedValue.IsDecimal() => JsExpr.Callish($"{JsExprWriter.WriteIn(value, JsPrecedence.Call)}.neg()"),
                    "-" => Negated(value, prefix, context),
                    "~" => Complemented(value, prefix, context),
                    _ => JsExpr.Prefix(text, value),
                }, context);
            }

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

            if (prefix.OperatorToken.Text == "-")
                return Negated(context.Converter.ConvertIr(prefix.Operand), prefix, context);
            if (prefix.OperatorToken.Text == "~")
                return Complemented(context.Converter.ConvertIr(prefix.Operand), prefix, context);
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
    /// by code unit (`'a'++` is NaN here), a DECIMAL steps on the type (JavaScript's `++` coerces it
    /// through its text into a plain number), a FLOAT rounds to single precision (`0.1f + 1` is not
    /// exact), a narrow width wraps and a checked context throws — the result type decides
    /// (IntegerWidth). A NULLABLE number steps its value by the same rule inside the lift, so null
    /// stays null (#372). A DICTIONARY ENTRY is read first, and .NET throws for a key that is not
    /// there, so it steps through the guard whatever its type computes. Null leaves the native
    /// `++`, which is what every loop counter wants.
    /// The target is evaluated once and a postfix step in value position answers the value BEFORE
    /// it, as C# does (ReadModifyWrite): `values[i++]++` steps `i` once, and `byte b = 255;
    /// var old = b++;` is 255, not the wrapped 0.
    /// </summary>
    private static JsExpr? Step(ExpressionSyntax operandSyntax, string op, SyntaxNode node, ConversionContext context)
    {
        var type = context.SemanticHelper.GetType(operandSyntax);
        var delta = op == "++" ? "+" : "-";
        var answerOld = node is PostfixUnaryExpressionSyntax && ValueUsed(node);
        var entry = ReadModifyWrite.EntryOf(operandSyntax, context);
        JsExpr Stepped(Func<JsExpr, JsExpr> next) => entry is not null
            ? ReadModifyWrite.AssignEntry(context.Converter.ConvertIr(entry.Expression),
                context.Converter.ConvertIr(entry.ArgumentList.Arguments[0].Expression), [], (current, _) => next(current),
                answerOld, context)
            : ReadModifyWrite.Assign(context.Converter.ConvertIr(operandSyntax), [], (current, _) => next(current),
                answerOld, context);
        JsExpr Plain(ITypeSymbol number, JsExpr current) =>
            JsExpr.Binary(current, delta, JsExpr.Literal(number.IsLong() ? "1n" : "1"));

        if (NullableLift.IsNullableNumber(type, out var value))
        {
            var rule = StepRule(value, delta, node, context) ?? (current => Plain(value, current));
            return Stepped(current => NullableLift.Unary(current, rule, context));
        }
        if (StepRule(type, delta, node, context) is { } typed) return Stepped(typed);
        return entry is not null && NullableLift.IsNumber(type) ? Stepped(current => Plain(type, current)) : null;
    }

    /// <summary>The value one step computes from the current one on <paramref name="type"/>, where
    /// JavaScript's own step would compute another; null where it computes C#'s.</summary>
    private static Func<JsExpr, JsExpr>? StepRule(ITypeSymbol? type, string delta, SyntaxNode node, ConversionContext context)
    {
        if (type is { SpecialType: SpecialType.System_Char })
            return current => JsExpr.Callish(
                $"String.fromCharCode({JsExprWriter.WriteIn(current, JsPrecedence.Call)}.charCodeAt(0) {delta} 1)");

        if (type.IsDecimal())
        {
            context.UsedHelpers.Add(Eq.Import);
            var method = delta == "+" ? "add" : "sub";
            return current => JsExpr.Callish(
                $"{JsExprWriter.WriteIn(current, JsPrecedence.Call)}.{method}({Eq.Dec}(1))");
        }

        if (SinglePrecision.Is(type))
            return current => SinglePrecision.Round(JsExpr.Binary(current, delta, JsExpr.Literal("1")));

        if (IntegerWidth.Of(type) is not { } width) return null;
        var arithmetic = ArithmeticContext.Of(node, context);
        if (!(arithmetic.IsChecked || arithmetic.ExplicitUnchecked || IntegerWidth.WrapsByDefault(width))) return null;
        var one = width.Bits == 64 ? JsExpr.Literal("1n") : JsExpr.Literal("1");
        return current => IntegerWidth.Settle(JsExpr.Binary(current, delta, one), type,
            arithmetic.IsChecked, arithmetic.ExplicitUnchecked, context);
    }

    /// <summary>
    /// A negation settled by its result's width in the context it sits in (IntegerWidth): C#'s
    /// <c>checked(-x)</c> throws for int.MinValue and an explicit <c>unchecked</c> negation of
    /// long.MinValue wraps back to it, where JavaScript's <c>-</c> answers one more than the type
    /// holds. The result is an int or a long (a narrower operand promotes, a uint's is a long), and
    /// neither wraps by default, so a plain negation stays the plain operator.
    /// </summary>
    private static JsExpr Negated(JsExpr operand, PrefixUnaryExpressionSyntax prefix, ConversionContext context)
    {
        var negated = JsExpr.Prefix("-", operand);
        var result = context.SemanticHelper.GetType(prefix).UnwrapNullable();
        if (IntegerWidth.Of(result) is null) return negated;
        var arithmetic = ArithmeticContext.Of(prefix, context);
        return arithmetic.IsChecked || arithmetic.ExplicitUnchecked
            ? IntegerWidth.Settle(negated, result, arithmetic.IsChecked, arithmetic.ExplicitUnchecked, context)
            : negated;
    }

    /// <summary>
    /// A complement in its result's width: JavaScript's <c>~</c> answers a signed 32-bit number, or a
    /// negative BigInt, so a uint's <c>~0</c> was -1 where C# answers uint.MaxValue, and a ulong's
    /// the same in 64 bits. An unsigned result goes back to its width; a signed one, int or long
    /// (a narrower operand promotes to int), is already what C# answers.
    /// </summary>
    private static JsExpr Complemented(JsExpr operand, PrefixUnaryExpressionSyntax prefix, ConversionContext context)
    {
        var complemented = JsExpr.Prefix("~", operand);
        return IntegerWidth.Of(context.SemanticHelper.GetType(prefix).UnwrapNullable()) is { Unsigned: true } width
            ? IntegerWidth.Wrap(complemented, width)
            : complemented;
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
