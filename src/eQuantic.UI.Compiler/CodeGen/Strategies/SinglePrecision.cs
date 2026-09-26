using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Where a C# <c>float</c> becomes single precision on this side: at the operation that PRODUCES
/// it. JavaScript computes in doubles, and RyuJIT does not — it emits <c>addss</c>/<c>mulss</c> and
/// rounds EVERY float operation to single, on every platform .NET ships. So the compiler rounds
/// wherever a float value is born: an arithmetic result (<c>+ - * /</c>, a compound assignment, an
/// increment), a conversion that can lose bits (from an <c>int</c>, a <c>long</c>, a
/// <c>double</c>, a <c>decimal</c>), a BCL call whose float answer is not exact (<c>float.Sqrt</c>,
/// <c>MathF.Sin</c>, <c>Sum</c>), a float constant, and a value arriving from the server. Every
/// float VALUE is then a single already, so a store, a comparison, an argument and a
/// <c>return</c> need nothing — which is what closed the seam a store-only rule had left open
/// (#146: a float-returning method handed its caller an unrounded double).
/// <para>
/// Rounding the DOUBLE result of one operation on two singles is exact, not an approximation: a
/// double carries 53 bits, at least 2×24+2, and at that width rounding twice gives the correctly
/// rounded single for <c>+ - * /</c> and the square root. What is NOT exact is skipping a step:
/// <c>fround(a*x - b*x)</c> is one rounding where RyuJIT makes three. The previous rule argued
/// from ECMA-335 I.12.1.3, which LETS a runtime carry a float intermediate at higher precision;
/// that is a permission, and the cross-pin promises what the subject computes, not what the
/// specification allows.
/// </para>
/// <para>
/// What needs no rounding is exact by arithmetic, not by assumption: a negation, a remainder of
/// two singles (<c>fmod</c> is exact), the min, max, absolute value, floor, ceiling, truncation
/// and whole-number rounding of a single, and an integer narrower than 25 bits.
/// </para>
/// </summary>
public static class SinglePrecision
{
    /// <summary>Whether the type is <c>float</c> (a <c>float?</c> is not: its value may be null,
    /// and a lifted operation answers null where <c>Math.fround</c> would answer 0).</summary>
    public static bool Is(ITypeSymbol? type) => type is { SpecialType: SpecialType.System_Single };

    /// <summary>The value rounded to single precision.</summary>
    public static JsExpr Round(JsExpr value) => JsExpr.Callish($"Math.fround({JsExprWriter.Write(value)})");

    /// <summary>Whether every value of an integer type is exactly a single: 24 bits of significand
    /// hold every integer below 2^24, so the sub-int widths and char convert exactly and the rest
    /// round like any other value.</summary>
    public static bool HoldsExactly(SpecialType integer) => integer is SpecialType.System_SByte
        or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16
        or SpecialType.System_Char;

    /// <summary>Whether an integer VALUE is provably a single already, so converting it rounds
    /// nothing: a constant the single represents, a choice between two such (<c>ring ? 12 : 8</c>,
    /// whatever <c>ring</c> is), or a value of a width that always fits.</summary>
    public static bool HoldsExactly(IOperation value) => value switch
    {
        { ConstantValue: { HasValue: true, Value: { } constant } } => IsExactInteger(constant),
        IConditionalOperation { WhenFalse: { } whenFalse } choice =>
            HoldsExactly(choice.WhenTrue) && HoldsExactly(whenFalse),
        _ => value.Type is { } type && HoldsExactly(type.SpecialType),
    };

    private static bool IsExactInteger(object constant) => constant switch
    {
        int i => RoundTrips(i),
        uint u => RoundTrips(u),
        short or ushort or byte or sbyte or char => true,
        _ => false,
    };

    // Compared as DOUBLES: `(float)i == i` would convert i to float first and always hold.
    private static bool RoundTrips(long value)
    {
        var single = (float)value;
        return (double)single == value;
    }
}
