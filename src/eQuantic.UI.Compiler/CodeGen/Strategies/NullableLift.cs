using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// C#'s lifted operators over <c>Nullable&lt;T&gt;</c>, for the rules this compiler writes itself: null
/// in, null out, and on a value the rule of <c>T</c>, a width that wraps, a single that rounds, a
/// Decimal's methods, a division's check. JavaScript's own operators read null as 0, so a lifted
/// operator reaches them only through the runtime's lift, which hands the rule the values and never
/// a null. The operands are the lift's arguments, evaluated once each and in order, as C# evaluates a
/// lifted operator's before testing them.
/// </summary>
internal static class NullableLift
{
    /// <summary>Whether <paramref name="type"/> is a <c>Nullable&lt;T&gt;</c> over a number whose
    /// operators are lifted here: an integral width, <c>float</c>, <c>double</c>, <c>decimal</c> or
    /// <c>char</c>. <paramref name="value"/> is its T.</summary>
    public static bool IsNullableNumber(ITypeSymbol? type, [NotNullWhen(true)] out ITypeSymbol? value)
    {
        value = type.IsNullableValue() ? type.UnwrapNullable() : null;
        return value is not null
            && (IntegerWidth.Of(value) is not null
                || value.SpecialType is SpecialType.System_Single or SpecialType.System_Double
                    or SpecialType.System_Decimal or SpecialType.System_Char);
    }

    /// <summary>A lifted binary operator: <paramref name="rule"/> over the two values, or null when
    /// either operand is.</summary>
    public static JsExpr Binary(JsExpr left, JsExpr right, Func<JsExpr, JsExpr, JsExpr> rule,
        ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var body = JsExprWriter.Write(rule(JsExpr.Identifier("a"), JsExpr.Identifier("b")));
        return JsExpr.Callish(
            $"{Eq.LiftArith}({JsExprWriter.WriteIn(left, JsPrecedence.Assignment)}, {JsExprWriter.WriteIn(right, JsPrecedence.Assignment)}, (a, b) => {body})");
    }

    /// <summary>A lifted unary operator (an increment, a negation): <paramref name="rule"/> over the
    /// value, or null when the operand is.</summary>
    public static JsExpr Unary(JsExpr operand, Func<JsExpr, JsExpr> rule, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var body = JsExprWriter.Write(rule(JsExpr.Identifier("a")));
        return JsExpr.Callish($"{Eq.LiftUnary}({JsExprWriter.WriteIn(operand, JsPrecedence.Assignment)}, (a) => {body})");
    }
}
