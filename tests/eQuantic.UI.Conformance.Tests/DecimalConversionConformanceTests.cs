using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A decimal that comes from text, from <c>Convert</c> or from a cast, on both sides (#358). It was
/// a JavaScript number wherever it came from anything but a literal: <c>parseFloat</c> lost 0.1 on
/// the way in, a double reached the Decimal through its shortest text (<c>(decimal)(0.1 + 0.2)</c>
/// was 0.30000000000000004 where .NET's is 0.3), and the next decimal operation called a method a
/// number does not have. Each case does decimal arithmetic or prints the decimal it got, and the
/// failures compare .NET's own words.
/// </summary>
public class DecimalConversionConformanceTests
{
    [SkippableTheory]
    // ---- decimal.Parse: .NET's grammar, and its rounding ----
    [InlineData("return (decimal.Parse(\"0.1\") + 0.2m).ToString();")]                                      // "0.3"
    [InlineData("return decimal.Parse(\"1.50\").ToString();")]                                              // "1.50" — the scale it was written with
    [InlineData("return decimal.Parse(\"  -1,234.5  \").ToString();")]                                      // "-1234.5"
    [InlineData("return decimal.Parse(\"5-\").ToString();")]                                                // "-5" — a sign after the digits
    [InlineData("try { return decimal.Parse(\"- 5\").ToString(); } catch (Exception e) { return e.Message; }")]  // FormatException
    [InlineData("try { return decimal.Parse(\"1e5\").ToString(); } catch (Exception e) { return e.Message; }")]  // FormatException — no exponent in Number
    [InlineData("try { return decimal.Parse(\"79228162514264337593543950336\").ToString(); } catch (Exception e) { return e.Message; }")] // OverflowException
    [InlineData("return decimal.Parse(\"7.92281625142643375935439503355\").ToString();")]                   // "7.922816251426433759354395034"
    [InlineData("return decimal.Parse(\"0.00000000000000000000000000015\").ToString();")]                   // "0.0000000000000000000000000002"
    [InlineData("return decimal.Parse(\"12345678901234567890123456788.5\").ToString();")]                   // even, a half: stays
    [InlineData("string s = null; try { return decimal.Parse(s).ToString(); } catch (Exception e) { return e.Message; }")]
    // ---- the styles a call names reach the reader ----
    [InlineData("return decimal.Parse(\"1e5\", System.Globalization.NumberStyles.Float).ToString();")]    // "100000"
    [InlineData("return decimal.Parse(\"1e-3\", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture).ToString();")] // "0.001"
    [InlineData("return decimal.Parse(style: System.Globalization.NumberStyles.Any, s: \"(1,234.50)\").ToString();")] // "-1234.50"
    [InlineData("return decimal.Parse(provider: System.Globalization.CultureInfo.InvariantCulture, s: \"2.5\").ToString();")] // "2.5"
    [InlineData("var styles = System.Globalization.NumberStyles.AllowExponent | System.Globalization.NumberStyles.AllowDecimalPoint; return decimal.Parse(\"1.5e2\", styles).ToString();")] // "150"
    [InlineData("try { return decimal.Parse(\"5\", System.Globalization.NumberStyles.AllowHexSpecifier).ToString(); } catch (Exception e) { return e.Message; }")]
    // A named argument lands in its own parameter, and the arguments still run in written order.
    [InlineData("var log = \"\"; string S() { log += \"s\"; return \"1.5\"; } System.Globalization.NumberStyles St() { log += \"t\"; return System.Globalization.NumberStyles.Float; } var d = decimal.Parse(style: St(), s: S()); return log + \" \" + d;")] // "ts 1.5"
    // ---- decimal.TryParse: the value, or 0 in the out ----
    [InlineData("var ok = decimal.TryParse(\"0.1\", out var d); return (ok ? \"ok \" : \"no \") + (d + 0.2m);")]           // "ok 0.3"
    [InlineData("var ok = decimal.TryParse(\"abc\", out var d); return (ok ? \"ok \" : \"no \") + d;")]                    // "no 0"
    [InlineData("decimal d = 7m; var ok = decimal.TryParse(\"x\", out d); return (ok ? \"ok \" : \"no \") + d;")]          // "no 0" — the out is written
    [InlineData("var ok = decimal.TryParse(\"79228162514264337593543950336\", out var d); return (ok ? \"ok \" : \"no \") + d;")] // "no 0"
    [InlineData("var ok = decimal.TryParse(\"1e5\", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d); return (ok ? \"ok \" : \"no \") + d;")] // "ok 100000"
    [InlineData("string s = null; var ok = decimal.TryParse(s, out var d); return (ok ? \"ok \" : \"no \") + d;")]         // "no 0"
    [InlineData("return decimal.TryParse(\"5\", out _) ? \"ok\" : \"no\";")]                                             // "ok"
    // An out target with an effect of its own runs it once, as C# does, whether the parse succeeds or not.
    [InlineData("var values = new decimal[2]; int index = 0; decimal.TryParse(\"x\", out values[index++]); return index + \"|\" + values[0] + \"|\" + values[1];")] // "1|0|0"
    [InlineData("var values = new decimal[2]; int index = 0; decimal.TryParse(\"1.5\", out values[index++]); return index + \"|\" + values[0];")]               // "1|1.5"
    [InlineData("var values = new int[2]; int index = 0; int.TryParse(\"7\", out values[index++]); return index + \"|\" + values[0];")]                          // "1|7"
    [InlineData("return int.TryParse(\"5\", out _) ? \"ok\" : \"no\";")]                                                 // "ok" — a discard, for every kind
    // ---- Convert.ToDecimal: by the type of what it converts ----
    [InlineData("return (Convert.ToDecimal(\"0.1\") + 0.2m).ToString();")]                                   // "0.3"
    [InlineData("return Convert.ToDecimal(\"1,234.5\").ToString();")]                                        // "1234.5"
    [InlineData("string s = null; return Convert.ToDecimal(s).ToString();")]                                 // "0" — where Parse throws
    [InlineData("return Convert.ToDecimal(0.1 + 0.2).ToString();")]                                          // "0.3"
    [InlineData("return Convert.ToDecimal(0.1f).ToString();")]                                               // "0.1"
    [InlineData("return Convert.ToDecimal(true).ToString();")]                                               // "1"
    [InlineData("return Convert.ToDecimal(long.MaxValue).ToString();")]                                      // exact
    [InlineData("object o = 1.5; return Convert.ToDecimal(o).ToString();")]                                  // "1.5"
    [InlineData("object o = null; return Convert.ToDecimal(o).ToString();")]                                 // "0"
    [InlineData("double? n = null; return Convert.ToDecimal(n).ToString();")]                                // "0"
    [InlineData("float? n = 0.1f; return Convert.ToDecimal(n).ToString();")]                                 // "0.1" — the single's 7 digits
    [InlineData("try { return Convert.ToDecimal('A').ToString(); } catch (Exception e) { return e.Message; }")] // InvalidCastException
    // ---- a cast, and a conversion C# applies itself ----
    [InlineData("double x = 0.1 + 0.2; return ((decimal)x).ToString();")]                                   // "0.3"
    [InlineData("double x = 0.1 + 0.2; return ((decimal)x * 3m).ToString();")]                              // "0.9"
    [InlineData("float f = 0.1f; return ((decimal)f).ToString();")]                                         // "0.1"
    [InlineData("float f = 1234567.5f; return ((decimal)f).ToString();")]                                   // "1234568"
    [InlineData("double x = 123456789012345.678; return ((decimal)x).ToString();")]                         // "123456789012346" — 15 digits
    [InlineData("double x = 1e28; return ((decimal)x).ToString();")]                                        // no text of 1e28 has an exponent
    [InlineData("double x = 2.5e-29; return ((decimal)x).ToString();")]                                     // "0"
    [InlineData("double x = 1e29; try { return ((decimal)x).ToString(); } catch (Exception e) { return e.Message; }")]   // OverflowException
    [InlineData("double x = double.NaN; try { return ((decimal)x).ToString(); } catch (Exception e) { return e.Message; }")] // OverflowException
    [InlineData("double? x = 0.1 + 0.2; decimal? m = (decimal?)x; return m.ToString();")]                   // "0.3" — lifted
    [InlineData("long l = long.MaxValue; decimal m = l; return m.ToString();")]                             // exact
    [InlineData("char c = 'A'; decimal m = c; return m.ToString();")]                                       // "65"
    [InlineData("int i = -5; decimal m = i; return (m / 2).ToString();")]                                   // "-2.5"
    public void ADecimal_FromTextConvertOrACast_IsDotNets(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
