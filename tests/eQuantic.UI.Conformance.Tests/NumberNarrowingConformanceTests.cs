using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A number narrowed to an integer answers .NET's value: <c>Convert.To&lt;integer&gt;</c> rounds half to
/// even and checks the target's range, in .NET's words (#401), and an explicit cast of a double or a
/// float saturates, the narrow widths taking the low bits of the saturated int (#1015).
/// </summary>
public class NumberNarrowingConformanceTests
{
    private const string Catch = "catch (OverflowException e) { return e.Message; }";

    /// <summary>
    /// <c>Convert.To&lt;integer&gt;</c> of what is not text rounds as .NET's overload rounds and checks its
    /// range in .NET's words, which differ by overload (#401): it was <c>$eq.math.round</c> for every
    /// source and target, with no range, no BigInt, a decimal handed back and a char read as a number.
    /// </summary>
    [SkippableTheory]
    // The rows of #401.
    [InlineData("try { return Convert.ToInt32(1e10); } " + Catch)]
    [InlineData("try { return Convert.ToInt32(double.NaN); } " + Catch)]
    [InlineData("try { return Convert.ToByte(300); } " + Catch)]
    [InlineData("try { return Convert.ToByte(-1); } " + Catch)]
    [InlineData("try { return Convert.ToUInt32(-0.6); } " + Catch)]
    [InlineData("try { return Convert.ToInt32(9999999999L); } " + Catch)]
    [InlineData("return (Convert.ToInt64(2.5) + 1L).ToString();")]                                  // 3
    [InlineData("return Convert.ToInt32(2.5m) + \"|\" + Convert.ToInt32(3.5m);")]                   // 2|4
    [InlineData("return Convert.ToInt32('A') + \"|\" + Convert.ToByte('A') + \"|\" + Convert.ToInt64('A');")] // 65|65|65
    // The edges of each width, after .NET's rounding.
    [InlineData("return Convert.ToInt32(2147483647.49) + \"|\" + Convert.ToInt32(-2147483648.5);")]
    [InlineData("try { return Convert.ToInt32(2147483647.5); } " + Catch)]
    [InlineData("try { return Convert.ToInt32(-2147483648.51); } " + Catch)]
    [InlineData("return Convert.ToByte(-0.5) + \"|\" + Convert.ToUInt32(-0.4) + \"|\" + Convert.ToUInt64(-0.5).ToString();")] // 0|0|0
    [InlineData("try { return Convert.ToByte(255.5); } " + Catch)]
    [InlineData("try { return Convert.ToSByte(127.5); } " + Catch)]
    [InlineData("try { return Convert.ToInt16(32767.5); } " + Catch)]
    [InlineData("try { return Convert.ToUInt16(65535.5); } " + Catch)]
    [InlineData("try { return Convert.ToUInt32(4294967295.5); } " + Catch)]
    // A narrow width goes through ToInt32(double), whose words these are.
    [InlineData("try { return Convert.ToInt16(1e10); } " + Catch)]
    // ToInt64 and ToUInt64 of a double are a checked cast, in its words.
    [InlineData("try { return Convert.ToInt64(9.3e18).ToString(); } " + Catch)]
    [InlineData("try { return Convert.ToUInt64(-0.6).ToString(); } " + Catch)]
    [InlineData("try { return Convert.ToUInt64(2e19).ToString(); } " + Catch)]
    [InlineData("return Convert.ToUInt64(1.8e19).ToString() + \"|\" + Convert.ToInt64(-9223372036854775808.0);")]
    // An integer of another width.
    [InlineData("try { return Convert.ToUInt64(-1).ToString(); } " + Catch)]
    [InlineData("try { return Convert.ToUInt32(-1L); } " + Catch)]
    [InlineData("try { return Convert.ToInt64(ulong.MaxValue).ToString(); } " + Catch)]
    [InlineData("try { return Convert.ToInt16(40000); } " + Catch)]
    // A decimal: decimal.Round, then the range.
    [InlineData("try { return Convert.ToByte(255.5m); } " + Catch)]
    [InlineData("try { return Convert.ToInt64(1e28m).ToString(); } " + Catch)]
    [InlineData("try { return Convert.ToUInt64(-0.6m).ToString(); } " + Catch)]
    [InlineData("return Convert.ToUInt64(-0.5m).ToString();")]                                     // 0
    // A char and a bool.
    [InlineData("try { return Convert.ToByte('\\u0100'); } " + Catch)]
    [InlineData("try { return Convert.ToSByte('\\u0080'); } " + Catch)]
    [InlineData("return Convert.ToInt32(true) + \"|\" + Convert.ToByte(false) + \"|\" + Convert.ToInt64(true);")] // 1|0|1
    // An object is read by what it holds.
    [InlineData("object o = null; return Convert.ToInt32(o);")]
    [InlineData("object o = \"12\"; return Convert.ToInt32(o);")]
    [InlineData("object o = 2.5; return Convert.ToInt32(o);")]
    [InlineData("object o = 3L; return Convert.ToInt32(o);")]
    [InlineData("object o = true; return Convert.ToInt32(o);")]
    [InlineData("object o = 2.5m; return Convert.ToInt32(o);")]
    [InlineData("object o = 1e10; try { return Convert.ToInt32(o); } " + Catch)]
    [InlineData("object o = \"1,5\"; try { return Convert.ToInt32(o, System.Globalization.CultureInfo.InvariantCulture); } catch (FormatException e) { return e.Message; }")]
    [InlineData("object o = \"1,5\"; return Convert.ToDouble(o, System.Globalization.CultureInfo.InvariantCulture);")]
    [InlineData("object o = DateTime.MinValue; try { return Convert.ToInt32(o); } catch (InvalidCastException e) { return e.Message; }")]
    // Convert.ToBoolean of an object, from #401's comment.
    [InlineData("object o = null; return Convert.ToBoolean(o);")]
    [InlineData("object o = \"abc\"; try { return Convert.ToBoolean(o); } catch (FormatException e) { return e.Message; }")]
    [InlineData("object o = \"True\"; return Convert.ToBoolean(o);")]
    [InlineData("object o = 1; object z = 0.0; return Convert.ToBoolean(o) + \"|\" + Convert.ToBoolean(z);")]
    public void ConvertToAnInteger_RoundsAndChecksAsDotNetDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// The rest of <c>Convert</c>'s numbers: a nullable and an enum, which C# boxes for the <c>object</c>
    /// overload, <c>ToDouble</c> and <c>ToSingle</c> of what has a number or has none, and <c>ToChar</c>,
    /// which read a text as a zero and wrapped an integer past its range.
    /// </summary>
    [SkippableTheory]
    [InlineData("int? none = null; int? some = 5; return Convert.ToInt32(none) + \"|\" + Convert.ToInt32(some) + \"|\" + Convert.ToBoolean(none) + \"|\" + Convert.ToBoolean(some);")] // 0|5|False|True
    [InlineData("return Convert.ToInt32(Tone.Warm) + \"|\" + Convert.ToByte(Tone.Warm) + \"|\" + Convert.ToInt64(Tone.Warm).ToString();")] // 3|3|3
    [InlineData("return Convert.ToDouble(true) + \"|\" + Convert.ToDouble(2.5m) + \"|\" + Convert.ToSingle(2.5m) + \"|\" + Convert.ToDouble(3L);")] // 1|2.5|2.5|3
    [InlineData("object o = 2.5m; object b = true; object t = \"2.5\"; object n = null; return Convert.ToDouble(o) + \"|\" + Convert.ToDouble(b) + \"|\" + Convert.ToSingle(t) + \"|\" + Convert.ToDouble(n);")] // 2.5|1|2.5|0
    [InlineData("try { return Convert.ToDouble('A'); } catch (InvalidCastException e) { return e.Message; }")]
    [InlineData("try { return Convert.ToSingle(DateTime.MinValue); } catch (InvalidCastException e) { return e.Message; }")]
    [InlineData("return Convert.ToChar(65).ToString() + Convert.ToChar('B') + Convert.ToChar(\"C\") + Convert.ToChar(68L);")] // ABCD
    [InlineData("try { return Convert.ToChar(-1); } " + Catch)]
    [InlineData("try { return Convert.ToChar(70000); } " + Catch)]
    [InlineData("try { return Convert.ToChar(65.0); } catch (InvalidCastException e) { return e.Message; }")]
    [InlineData("try { return Convert.ToChar(true); } catch (InvalidCastException e) { return e.Message; }")]
    [InlineData("try { return Convert.ToChar(1m); } catch (InvalidCastException e) { return e.Message; }")]
    [InlineData("try { return Convert.ToChar(\"AB\"); } catch (FormatException e) { return e.Message; }")]
    [InlineData("string none = null; try { return Convert.ToChar(none); } catch (ArgumentNullException e) { return e.Message; }")]
    [InlineData("object o = 65; object t = \"A\"; object n = null; return Convert.ToChar(o).ToString() + Convert.ToChar(t) + (int)Convert.ToChar(n);")] // AA0
    [InlineData("object o = \"AB\"; try { return Convert.ToChar(o); } catch (FormatException e) { return e.Message; }")]
    [InlineData("object o = 2.5; try { return Convert.ToChar(o); } catch (InvalidCastException e) { return e.Message; }")]
    public void ConvertOfANullableAnEnumOrAChar_AnswersAsDotNetDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, "enum Tone { Cold = 1, Warm = 3 }");
    }

    /// <summary>
    /// An explicit cast of a double or a float saturates, as .NET 9 and later cast on every platform, and
    /// the widths under an int take the low bits of the saturated int, so <c>(byte)300.0</c> is 44 and
    /// <c>(byte)1e12</c> is 255: the whole part wrapped modulo 2^32 where the value was past an int (#1015).
    /// </summary>
    [SkippableTheory]
    [InlineData("double d = 1e12; return (int)d + \"|\" + (uint)d + \"|\" + ((long)d).ToString() + \"|\" + ((ulong)d).ToString();")]
    [InlineData("double d = -1e12; return (int)d + \"|\" + (uint)d + \"|\" + ((long)d).ToString() + \"|\" + ((ulong)d).ToString();")]
    [InlineData("double d = double.NaN; return (int)d + \"|\" + (uint)d + \"|\" + ((long)d).ToString() + \"|\" + ((ulong)d).ToString() + \"|\" + (byte)d + \"|\" + (short)d;")]
    [InlineData("double d = double.PositiveInfinity; return (int)d + \"|\" + (uint)d + \"|\" + ((long)d).ToString() + \"|\" + ((ulong)d).ToString() + \"|\" + (byte)d + \"|\" + (sbyte)d + \"|\" + (short)d + \"|\" + (ushort)d;")]
    [InlineData("double d = double.NegativeInfinity; return (int)d + \"|\" + (uint)d + \"|\" + ((long)d).ToString() + \"|\" + ((ulong)d).ToString() + \"|\" + (byte)d + \"|\" + (sbyte)d + \"|\" + (short)d + \"|\" + (ushort)d;")]
    // A narrow width takes the low bits of the saturated int.
    [InlineData("double d = 300; return (byte)d + \"|\" + (sbyte)d + \"|\" + (short)d + \"|\" + (ushort)d;")]
    [InlineData("double d = -1; return (byte)d + \"|\" + (sbyte)d + \"|\" + (short)d + \"|\" + (ushort)d + \"|\" + (uint)d + \"|\" + ((ulong)d).ToString();")]
    [InlineData("double d = 1e12; return (byte)d + \"|\" + (sbyte)d + \"|\" + (short)d + \"|\" + (ushort)d + \"|\" + (int)(char)d;")]
    [InlineData("double d = 70000; return (short)d + \"|\" + (ushort)d + \"|\" + (int)(char)d;")]
    // The edges of the wide ones.
    [InlineData("double d = 4294967296; return (uint)d + \"|\" + (int)d;")]
    [InlineData("double d = 9.3e18; return ((long)d).ToString() + \"|\" + ((ulong)d).ToString();")]
    [InlineData("double d = 2e19; return ((long)d).ToString() + \"|\" + ((ulong)d).ToString();")]
    // A float, a nullable and a fraction.
    [InlineData("float f = 1e12f; return (int)f + \"|\" + (byte)f + \"|\" + ((long)f).ToString();")]
    [InlineData("float f = float.NaN; return (int)f + \"|\" + ((ulong)f).ToString();")]
    [InlineData("double? d = 1e12; int? i = (int?)d; return i.ToString();")]
    [InlineData("double d = -0.9; return ((int)d).ToString() + \"|\" + (byte)d + \"|\" + ((long)d).ToString();")]
    // A checked cast still throws.
    [InlineData("double d = 1e12; try { return checked((int)d); } " + Catch)]
    // An enum over a byte, which a cast of a double reaches as the width does.
    [InlineData("double d = 1e12; return (int)(Level)d;")]
    public void ACastOfADouble_SaturatesAsDotNetDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, "enum Level : byte { Low = 1 }");
    }
}
