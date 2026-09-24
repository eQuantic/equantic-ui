using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// C#'s integer <c>/</c> and <c>%</c> (#333): truncated toward zero, and the throws .NET throws. A
/// zero divisor is a DivideByZeroException, and <c>MinValue / -1</c> an OverflowException on int and
/// long, in a checked context or not (measured on .NET 10, remainder included). JavaScript answered
/// Infinity, NaN or 2147483648 instead, and a BigInt a RangeError of its own, so a count of zero in a
/// UI's <c>total / count</c> rendered Infinity where the server threw.
/// <para>
/// The check costs a call, so a divisor that is a CONSTANT other than 0 and -1 settles it at compile
/// time and keeps the operator it always had: <c>col / 26</c> can throw nothing. C# already refuses a
/// constant zero (CS0020). The narrower widths compute in int and an unsigned value is never negative,
/// so only a zero divisor reaches them, and the runtime's one check for <c>MinValue / -1</c> never
/// fires for them.
/// </para>
/// </summary>
internal static class IntegerDivision
{
    /// <summary>Whether <paramref name="divisor"/> can make the division throw: anything but a
    /// constant other than 0 and -1.</summary>
    public static bool NeedsCheck(ExpressionSyntax divisor, ConversionContext context)
    {
        if (!context.SemanticHelper.TryGetConstantValue(divisor, out var value)) return true;
        return value switch
        {
            char code => code == 0,
            ulong unsigned => unsigned == 0,
            sbyte or byte or short or ushort or int or uint or long =>
                System.Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) is 0 or -1,
            _ => true,
        };
    }

    /// <summary>The quotient or remainder of two numbers: through the runtime's check, or with the
    /// bare operator when the divisor settles it.</summary>
    public static JsExpr OfNumbers(string op, JsExpr left, JsExpr right, bool check, ConversionContext context)
    {
        if (!check)
            return op == "/"
                ? JsExpr.Call(JsExpr.Identifier("Math.trunc"), JsExpr.Binary(left, "/", right))
                : JsExpr.Binary(left, "%", right);
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(op == "/" ? Eq.IntDiv : Eq.IntRem), left, right);
    }

    /// <summary>The quotient or remainder of two longs, BigInts here: the same rule.</summary>
    public static JsExpr OfLongs(string op, JsExpr left, JsExpr right, bool check, ConversionContext context)
    {
        if (!check) return JsExpr.Binary(left, op, right);
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier(op == "/" ? Eq.LongDiv : Eq.LongRem), left, right);
    }
}
