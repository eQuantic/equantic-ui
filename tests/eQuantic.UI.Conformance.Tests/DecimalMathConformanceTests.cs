using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A decimal through every member <c>Math</c> has for it and through every explicit conversion to an
/// integer, on both sides (#1766, #1767). <c>Math.Abs</c>, <c>Max</c>, <c>Min</c>, <c>Sign</c> and
/// <c>Clamp</c> went to JavaScript's <c>Math</c>, which read the runtime's Decimal as NaN, with no build
/// error; and a cast to an integer went through the decimal's double, so a long past 2^53 lost its last
/// digits and <c>(int)0.99999999999999999m</c> was 1. <c>decimal</c>'s own statics of the same names
/// reach the same row of the numeric table, and so do its <c>To&lt;integer&gt;</c> conversions.
/// </summary>
public class DecimalMathConformanceTests
{
    [SkippableTheory]
    // ---- Math's members over a decimal, the scale .NET keeps included ----
    [InlineData("decimal a = -12.50m; return Math.Abs(a) + \"|\" + Math.Max(a, 0m) + \"|\" + Math.Min(a, 0m) + \"|\" + Math.Sign(a);")]
    [InlineData("decimal a = 12.50m, z = 0.00m; return Math.Abs(a) + \"|\" + Math.Sign(a) + \"|\" + Math.Sign(z) + \"|\" + Math.Abs(z);")]
    // Two equal values of different scales: Max keeps the first, Min the second, as .NET compares them.
    [InlineData("decimal a = 1.0m, b = 1.00m; return Math.Max(a, b) + \"|\" + Math.Max(b, a) + \"|\" + Math.Min(a, b) + \"|\" + Math.Min(b, a);")]
    [InlineData("decimal a = 5m; return Math.Clamp(a, 1m, 3m) + \"|\" + Math.Clamp(0.5m, 1.0m, 3m) + \"|\" + Math.Clamp(1.0m, 1.00m, 3m) + \"|\" + Math.Clamp(3.0m, 1m, 3.00m);")]
    [InlineData("decimal a = 2m; try { return Math.Clamp(a, 3.5m, 1.25m).ToString(); } catch (ArgumentOutOfRangeException) { return \"out of range\"; } catch (ArgumentException e) { return \"ArgumentException: \" + e.Message; }")]
    // ---- decimal's own statics reach the same row ----
    [InlineData("decimal a = -12.50m; return decimal.Abs(a) + \"|\" + decimal.Max(a, 0m) + \"|\" + decimal.Min(a, 0m) + \"|\" + decimal.Sign(a) + \"|\" + decimal.Clamp(a, -1m, 1m);")]
    // ---- the arguments are evaluated once each, in order ----
    [InlineData("var log = \"\"; decimal Next(decimal v) { log += v + \",\"; return v; } var m = Math.Max(Next(1m), Next(2m)); var c = Math.Clamp(Next(5m), Next(1m), Next(3m)); return m + \"|\" + c + \"|\" + log;")]
    public void AMathMember_OverADecimal_AnswersAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>What every integer type and a char make of a value: toward zero from its own digits, then
    /// its range, the overflow in .NET's words for each type.</summary>
    private const string EveryType =
        "string Each(decimal v) { string Try(Func<string> f) { try { return f(); } catch (OverflowException e) { return e.Message; } } "
        + "return Try(() => ((sbyte)v).ToString()) + \"|\" + Try(() => ((byte)v).ToString()) + \"|\" + Try(() => ((short)v).ToString()) + \"|\" "
        + "+ Try(() => ((ushort)v).ToString()) + \"|\" + Try(() => ((int)v).ToString()) + \"|\" + Try(() => ((uint)v).ToString()) + \"|\" "
        + "+ Try(() => ((long)v).ToString()) + \"|\" + Try(() => ((ulong)v).ToString()) + \"|\" + Try(() => ((int)(char)v).ToString()) + \"|\" "
        + "+ Try(() => ((nint)v).ToString()) + \"|\" + Try(() => ((nuint)v).ToString()); } ";

    [SkippableTheory]
    [InlineData("0.99999999999999999m")]
    [InlineData("-0.5m")]
    [InlineData("255.9m")]
    [InlineData("256m")]
    [InlineData("65535.5m")]
    [InlineData("-1m")]
    [InlineData("2147483647.9999999999999999m")]
    [InlineData("-2147483648.99999999m")]
    [InlineData("2147483648m")]
    [InlineData("9007199254740993m")]
    [InlineData("9223372036854775807.9m")]
    [InlineData("9223372036854775808m")]
    [InlineData("-9223372036854775808.5m")]
    [InlineData("18446744073709551615.5m")]
    [InlineData("18446744073709551616m")]
    public void ADecimalCastToAnInteger_IsCutTowardZeroFromItsDigits(string value)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(EveryType + $"decimal d = {value}; return Each(d);");
    }

    [SkippableTheory]
    // decimal's To<integer> conversions are the casts, their overflow included.
    [InlineData("decimal d = 9007199254740993m; return decimal.ToInt64(d) + \"|\" + decimal.ToUInt64(d) + \"|\" + decimal.ToInt32(2147483647.99m) + \"|\" + decimal.ToByte(255.9m);")]
    [InlineData("decimal d = 2147483648m; try { return decimal.ToInt32(d).ToString(); } catch (OverflowException e) { return e.Message; }")]
    [InlineData("decimal d = -1m; return decimal.ToSByte(d) + \"|\" + decimal.ToInt16(d) + \"|\" + decimal.ToUInt16(0.9m) + \"|\" + decimal.ToUInt32(4294967295.5m);")]
    // A nullable one converts its value, and a cast inside unchecked still refuses what does not fit.
    [InlineData("decimal? d = 9007199254740993m; return ((long?)d).ToString() + \"|\" + ((long)d).ToString();")]
    [InlineData("decimal d = 1e10m; try { return unchecked((int)d).ToString(); } catch (OverflowException e) { return e.Message; }")]
    public void ADecimalConversion_IsTheCast(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>A decimal cast to an enum converts it to the enum's underlying type as a cast to that type
    /// does: toward zero, from its digits.</summary>
    [SkippableTheory]
    [InlineData("return ((Level)0.99999999999999999m).ToString() + \"|\" + ((Level)2.5m).ToString();")]
    [InlineData("return ((long)(Wide)9007199254740993m).ToString();")]
    public void ADecimalCastToAnEnum_IsCutAsItsUnderlyingType(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, "enum Level { Low, Mid, High } enum Wide : long { None }");
    }

    /// <summary>Clamp's refusal writes its values in the current culture, as string.Format writes them.</summary>
    [SkippableFact]
    public void AClampRefusal_WritesItsValuesInTheCulture()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            "decimal a = 2m; try { return Math.Clamp(a, 3.5m, 1.25m).ToString(); } catch (ArgumentException e) { return e.Message; }",
            culture: "pt-BR");
    }
}
