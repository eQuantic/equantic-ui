using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// The operators C# defines on an enum (§12.10.5, §12.10.6, §12.13.3), each computed as C# computes it,
/// on the VALUES at the underlying type's width: <c>e + n</c> and <c>e - n</c> are the enum again,
/// <c>a - b</c> its underlying type, <c>&amp;</c>, <c>|</c>, <c>^</c> and <c>~</c> the enum, a step the
/// next value, and a relation compares the values. A result settles in the width as an integer of the
/// underlying type does (<see cref="IntegerWidth"/>): a byte's wraps, a uint's is never negative, a
/// 64-bit one's is a BigInt's, and a checked context throws.
/// <para>
/// JavaScript's own operators answered for a flags enum's, in signed 32 bits, so a uint flag's high bit
/// read negative and a long's flags above bit 31 vanished (#555). A non-flags operand is read through
/// its shape (<see cref="EnumShape.ValueOf"/>), and an enum result is held again
/// (<see cref="EnumShape.Held"/>). Equality is not here: it compares what is held, a key with a key and
/// a value with a value, which <c>===</c> already does, a BigInt's included.
/// </para>
/// </summary>
internal static class EnumOperators
{
    /// <summary>The enum a type is, a nullable one's included, or null.</summary>
    internal static INamedTypeSymbol? EnumOf(ITypeSymbol? type) =>
        type.UnwrapNullable() is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType ? enumType : null;

    /// <summary>An operand as the value an operator computes with: an enum's value, and anything else
    /// (the underlying type's operand of <c>e + n</c>) as it is.</summary>
    internal static JsExpr Value(JsExpr operand, ITypeSymbol? type, ConversionContext context) =>
        EnumOf(type) is { } enumType ? EnumShape.ValueOf(enumType, operand, context) : operand;

    /// <summary>
    /// A binary expression whose operator is an enum's, as the bound tree names it: computed on the
    /// values, and through the runtime's lift when it is lifted, which answers null for an absent operand
    /// and false for a relation with one. Null for any other operator, equality and a string's
    /// concatenation included.
    /// </summary>
    internal static JsExpr? Binary(BinaryExpressionSyntax binary, string op, JsExpr left, JsExpr right,
        ConversionContext context)
    {
        if (op is not ("+" or "-" or "&" or "|" or "^" or "<" or ">" or "<=" or ">=")) return null;
        if (context.SemanticHelper.GetOperation(binary) is not IBinaryOperation { OperatorMethod: null } bound) return null;
        var leftType = bound.LeftOperand.Type;
        var rightType = bound.RightOperand.Type;
        var enumType = EnumOf(leftType) ?? EnumOf(rightType);
        if (enumType is null || bound.Type.UnwrapNullable()?.SpecialType == SpecialType.System_String) return null;

        var arithmetic = ArithmeticContext.Of(binary, context);
        if (!bound.IsLifted) return Compute(op, left, leftType, right, rightType, bound.Type, arithmetic, context);

        var (valueLeft, valueRight, result) = (leftType.UnwrapNullable(), rightType.UnwrapNullable(), bound.Type.UnwrapNullable());
        JsExpr Rule(JsExpr a, JsExpr b) => Compute(op, a, valueLeft, b, valueRight, result, arithmetic, context);
        return op is "<" or ">" or "<=" or ">="
            ? NullableLift.Relation(left, right, Rule, context)
            : NullableLift.Binary(left, right, Rule, context);
    }

    /// <summary>
    /// The value <paramref name="op"/> computes from two operands that are not null, as the result type
    /// holds it: the enum's key or value, the underlying type's value, or a bool for a relation.
    /// </summary>
    internal static JsExpr Compute(string op, JsExpr left, ITypeSymbol? leftType, JsExpr right, ITypeSymbol? rightType,
        ITypeSymbol? resultType, ArithmeticContext arithmetic, ConversionContext context)
    {
        var enumType = (EnumOf(leftType) ?? EnumOf(rightType))!;
        var l = Value(left, leftType, context);
        var r = Value(right, rightType, context);
        var computed = op switch
        {
            "+" or "-" => IntegerWidth.Settle(JsExpr.Binary(l, op, r), enumType.EnumUnderlyingType,
                arithmetic.IsChecked, arithmetic.ExplicitUnchecked, context),
            "&" or "|" or "^" => IntegerWidth.Bitwise(op, l, r, EnumShape.Width(enumType)) ?? JsExpr.Binary(l, op, r),
            _ => JsExpr.Binary(l, op, r),
        };
        return EnumOf(resultType) is { } held ? EnumShape.Held(held, computed, context) : computed;
    }

    /// <summary>
    /// <c>~e</c>: the complement of the value in the underlying type's width, held as the enum. A byte's
    /// <c>~1</c> is 254, where JavaScript's <c>~</c> answers -2 in signed 32 bits, and a uint's and a
    /// ulong's are never negative. Null where the operand is no enum.
    /// </summary>
    internal static JsExpr? Complement(PrefixUnaryExpressionSyntax prefix, JsExpr operand, ConversionContext context)
    {
        var type = context.SemanticHelper.GetType(prefix.Operand);
        if (EnumOf(type) is not { } enumType) return null;
        JsExpr Complemented(JsExpr held)
        {
            var width = EnumShape.Width(enumType);
            var complemented = JsExpr.Prefix("~", EnumShape.ValueOf(enumType, held, context));
            // A signed type holds its complement already: JavaScript's `~` of a value in range stays in it.
            return EnumShape.Held(enumType, width.Unsigned ? IntegerWidth.Wrap(complemented, width) : complemented, context);
        }
        return type.IsNullableValue() ? NullableLift.Unary(operand, Complemented, context) : Complemented(operand);
    }

    /// <summary>
    /// The step <c>e++</c> and <c>e--</c> take, the next or the previous value in the underlying type's
    /// width, held as the enum. Null where the type is no enum.
    /// </summary>
    internal static Func<JsExpr, JsExpr>? Step(ITypeSymbol? type, string delta, SyntaxNode node, ConversionContext context)
    {
        if (EnumOf(type) is not { } enumType) return null;
        var arithmetic = ArithmeticContext.Of(node, context);
        var one = JsExpr.Literal(EnumShape.ValueLiteral(enumType, 1));
        return current => EnumShape.Held(enumType,
            IntegerWidth.Settle(JsExpr.Binary(EnumShape.ValueOf(enumType, current, context), delta, one),
                enumType.EnumUnderlyingType, arithmetic.IsChecked, arithmetic.ExplicitUnchecked, context),
            context);
    }

    /// <summary>
    /// <c>e op= v</c> on an enum target: the value its operator computes, as <see cref="Compute"/>
    /// computes it, from the target and the value of the type the bound tree converted it to (the enum for
    /// <c>|=</c>, the underlying type for <c>+=</c>). Null where the target is no enum.
    /// </summary>
    internal static Func<JsExpr, JsExpr, JsExpr>? Compound(string op, ITypeSymbol? target, AssignmentExpressionSyntax assignment,
        ConversionContext context)
    {
        if (op is not ("+" or "-" or "&" or "|" or "^") || EnumOf(target) is null) return null;
        var valueType = context.SemanticHelper.GetOperation(assignment) is ICompoundAssignmentOperation compound
            ? compound.Value.Type
            : context.SemanticHelper.GetType(assignment.Right);
        var arithmetic = ArithmeticContext.Of(assignment, context);
        var targetValue = target.UnwrapNullable();
        return (current, operand) => Compute(op, current, targetValue, operand, valueType.UnwrapNullable(), targetValue,
            arithmetic, context);
    }

    /// <summary>
    /// <c>e.HasFlag(flag)</c>: whether every bit of the flag's value is set in the receiver's, which .NET
    /// answers for any enum, a non-flags one's included, so a member with no bits is in every value. The
    /// flag is read once, and the test settles in the width: a uint's high bit was negative on one side of
    /// the comparison and not on the other. A constant flag is its value, written at the enum's width,
    /// which the test reads twice as it is: <c>((p &amp; 1) === 1)</c>.
    /// </summary>
    internal static JsExpr HasFlag(INamedTypeSymbol enumType, JsExpr receiver, JsExpr flag, decimal? flagConstant,
        ConversionContext context)
    {
        var value = EnumShape.ValueOf(enumType, receiver, context);
        var mask = flagConstant is { } constant
            ? JsExpr.Literal(EnumShape.ValueLiteral(enumType, constant))
            : EnumShape.ValueOf(enumType, flag, context);
        var masked = IntegerWidth.Bitwise("&", JsExpr.Opaque("{0}"), JsExpr.Opaque("{1}"), EnumShape.Width(enumType));
        var test = masked is null ? "({0} & {1})" : JsExprWriter.Write(masked);
        return JsExpr.Template($"({test} === {{1}})", [value, mask]);
    }
}
