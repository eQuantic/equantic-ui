using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// C#'s lifted operators over <c>Nullable&lt;T&gt;</c>, for the rules this compiler writes itself: null
/// in, null out, and on a value the rule of <c>T</c>, a width that wraps, a single that rounds, a
/// Decimal's methods, a division's check, a struct's own operator. JavaScript's own operators read null
/// as 0, so a lifted operator reaches them only through the runtime's lift, which hands the rule the
/// values and never a null. The operands are the lift's arguments, evaluated once each and in order, as
/// C# evaluates a lifted operator's before testing them.
/// </summary>
internal static class NullableLift
{
    /// <summary>
    /// C#'s lift of a binary operator by its kind (#948): an equality as <see cref="Equality"/>, a
    /// relation as <see cref="Relation"/>, and every other operator as <see cref="Binary"/>, null for an
    /// absent operand.
    /// </summary>
    public static JsExpr Operator(string token, JsExpr left, JsExpr right, Func<JsExpr, JsExpr, JsExpr> rule,
        ConversionContext context) => token switch
    {
        "==" or "!=" => Equality(token == "==", left, right, rule),
        "<" or ">" or "<=" or ">=" => Relation(left, right, rule, context),
        _ => Binary(left, right, rule, context),
    };

    /// <summary>
    /// A lifted equality, <c>==</c> where <paramref name="equal"/> and <c>!=</c> otherwise (#948):
    /// <paramref name="rule"/> compares two values, and where an operand is absent C# answers without it,
    /// two absent values equal and an absent one equal to no value, which is what JavaScript's loose
    /// equality answers between them. An operand that IS the null literal makes it the other's null check,
    /// for which C# never calls the operator either.
    /// </summary>
    public static JsExpr Equality(bool equal, JsExpr left, JsExpr right, Func<JsExpr, JsExpr, JsExpr> rule)
    {
        var loose = equal ? "==" : "!=";
        if (JsExprWriter.Write(left) == "null" || JsExprWriter.Write(right) == "null")
            return JsExpr.Binary(left, loose, right);
        JsExpr a = JsExpr.Identifier("$a"), b = JsExpr.Identifier("$b"), absent = JsExpr.Literal("null");
        var eitherAbsent = JsExpr.Binary(JsExpr.Binary(a, "==", absent), "||", JsExpr.Binary(b, "==", absent));
        return JsExpr.Call(
            JsExpr.Arrow("$a, $b", JsExpr.Conditional(eitherAbsent, JsExpr.Binary(a, loose, b), rule(a, b))),
            left, right);
    }

    /// <summary>A lifted relation, <c>&lt;</c>, <c>&gt;</c>, <c>&lt;=</c> or <c>&gt;=</c>:
    /// <paramref name="rule"/> over the two values, or false when either operand is null.</summary>
    public static JsExpr Relation(JsExpr left, JsExpr right, Func<JsExpr, JsExpr, JsExpr> rule,
        ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(Eq.LiftCmp), left, right,
            JsExpr.Arrow("$a, $b", rule(JsExpr.Identifier("$a"), JsExpr.Identifier("$b"))));
    }

    /// <summary>Whether <paramref name="type"/> is a <c>Nullable&lt;T&gt;</c> over a number whose
    /// operators are lifted here: an integral width, <c>float</c>, <c>double</c>, <c>decimal</c> or
    /// <c>char</c>. <paramref name="value"/> is its T.</summary>
    public static bool IsNullableNumber(ITypeSymbol? type, [NotNullWhen(true)] out ITypeSymbol? value)
    {
        value = type.IsNullableValue() ? type.UnwrapNullable() : null;
        return IsNumber(value);
    }

    /// <summary>Whether <paramref name="type"/> is one of those numbers itself: an integral width,
    /// <c>float</c>, <c>double</c>, <c>decimal</c> or <c>char</c>.</summary>
    public static bool IsNumber([NotNullWhen(true)] ITypeSymbol? type) =>
        type is not null
        && (IntegerWidth.Of(type) is not null
            || type.SpecialType is SpecialType.System_Single or SpecialType.System_Double
                or SpecialType.System_Decimal or SpecialType.System_Char);

    /// <summary>A lifted binary operator: <paramref name="rule"/> over the two values, or null when
    /// either operand is.</summary>
    public static JsExpr Binary(JsExpr left, JsExpr right, Func<JsExpr, JsExpr, JsExpr> rule,
        ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var body = JsExprWriter.Write(rule(JsExpr.Identifier("$a"), JsExpr.Identifier("$b")));
        return JsExpr.Callish(
            $"{Eq.LiftArith}({JsExprWriter.WriteIn(left, JsPrecedence.Assignment)}, {JsExprWriter.WriteIn(right, JsPrecedence.Assignment)}, ($a, $b) => {body})");
    }

    /// <summary>A lifted unary operator (an increment, a negation): <paramref name="rule"/> over the
    /// value, or null when the operand is.</summary>
    public static JsExpr Unary(JsExpr operand, Func<JsExpr, JsExpr> rule, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var body = JsExprWriter.Write(rule(JsExpr.Identifier("$a")));
        return JsExpr.Callish($"{Eq.LiftUnary}({JsExprWriter.WriteIn(operand, JsPrecedence.Assignment)}, ($a) => {body})");
    }
}
