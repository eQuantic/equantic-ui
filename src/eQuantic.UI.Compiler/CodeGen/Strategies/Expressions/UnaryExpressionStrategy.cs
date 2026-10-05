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
                    return JsExpr.Callish(ConstantLiteral.Write(negated, null, context)!);
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
    /// (IntegerWidth), an ENUM steps its value, which the browser holds by its member's name, and a
    /// type with a user-defined <c>operator ++</c> steps through the static its twin carries. A
    /// NULLABLE number steps its value by the same rule inside the lift, so null stays null (#372).
    /// Null leaves the native `++`, which is what every loop counter wants.
    /// <para>
    /// A PLACE (a dictionary's entry, an entry of an indexer a twin carries, #427) has no target
    /// JavaScript's own step can write, so every step of one is a read-modify-write, the step being
    /// the one a local of its type gets: an indexer's enum went to the native `++`, which wrote
    /// <c>m.item(0)++</c>, and the module did not parse. A dictionary's entry is read first, and .NET
    /// throws for a key that is not there, so it steps through the guard whatever its type computes.
    /// A step with no form here is refused rather than written as JavaScript's own.
    /// </para>
    /// The target is evaluated once and a postfix step in value position answers the value BEFORE
    /// it, as C# does (ReadModifyWrite): `values[i++]++` steps `i` once, and `byte b = 255;
    /// var old = b++;` is 255, not the wrapped 0.
    /// </summary>
    private static JsExpr? Step(ExpressionSyntax operandSyntax, string op, SyntaxNode node, ConversionContext context)
    {
        var type = context.SemanticHelper.GetType(operandSyntax);
        var delta = op == "++" ? "+" : "-";
        var answerOld = node is PostfixUnaryExpressionSyntax && ValueUsed(node);
        var place = Place.Of(operandSyntax, context);
        JsExpr Stepped(Func<JsExpr, JsExpr> next) => place is not null
            ? place.Modify([], (current, _) => next(current), answerOld)
            : ReadModifyWrite.Assign(context.Converter.ConvertIr(operandSyntax), [], (current, _) => next(current),
                answerOld, context);
        JsExpr Plain(ITypeSymbol number, JsExpr current) =>
            JsExpr.Binary(current, delta, JsExpr.Literal(number.IsLong() ? "1n" : "1"));

        // A user-defined step goes through the static its twin carries, for a type the source
        // declares; a framework type's (Int128, Half) keeps the rules below, which have none for it,
        // and a host-only one stops here.
        if (context.SemanticHelper.GetOperation(node) is Microsoft.CodeAnalysis.Operations.IIncrementOrDecrementOperation
            { OperatorMethod: { } method } increment)
        {
            if (method.ReportIfHostOnly(node, context)) return JsExpr.Callish("undefined");
            if (UserDefinedOperators.IsInSource(method))
            {
                // C# 14's instance `void operator ++()` steps the value in place, which no twin carries yet.
                if (!method.IsStatic || UserDefinedOperators.Unary(method, op, "") is null)
                    return JsExpr.Callish(context.Unhandled(node, "user-defined step"));
                method.ContainingType.RegisterIntroduced(context);
                JsExpr Operator(JsExpr current) => UserDefinedOperators.Unary(method, op, JsExprWriter.Write(current))!;
                return Stepped(current => increment.IsLifted ? NullableLift.Unary(current, Operator, context) : Operator(current));
            }
        }
        if (NullableLift.IsNullableNumber(type, out var value))
        {
            var rule = StepRule(value, delta, node, context) ?? (current => Plain(value, current));
            return Stepped(current => NullableLift.Unary(current, rule, context));
        }
        // A nullable enum steps its value inside the lift, as a nullable number does.
        if (type.IsNullableValue() && type.UnwrapNullable() is INamedTypeSymbol { TypeKind: TypeKind.Enum } nullableEnum
            && StepRule(nullableEnum, delta, node, context) is { } enumRule)
            return Stepped(current => NullableLift.Unary(current, enumRule, context));
        if (StepRule(type, delta, node, context) is { } typed) return Stepped(typed);
        if (place is null) return null;
        if (NullableLift.IsNumber(type)) return Stepped(current => Plain(type, current));
        return JsExpr.Callish(context.Unhandled(node, "step"));
    }

    /// <summary>The value one step computes from the current one on <paramref name="type"/>, where
    /// JavaScript's own step would compute another; null where it computes C#'s.</summary>
    private static Func<JsExpr, JsExpr>? StepRule(ITypeSymbol? type, string delta, SyntaxNode node, ConversionContext context)
    {
        // An enum steps its value, as its arithmetic computes it: `mode + 1`, the name behind the value
        // where one has it (Types.EnumShape). JavaScript's step read the name as a number, NaN.
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
            return current => Types.EnumShape.Held(enumType,
                JsExpr.Binary(Types.EnumShape.ValueOf(enumType, current, context), delta, JsExpr.Literal("1")), context);

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
