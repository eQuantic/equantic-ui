using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// An integer, a double or a float read from text, on both sides (#376). They were read by
/// <c>parseInt</c> and <c>parseFloat</c>, which take the longest prefix that looks like a number
/// and answer NaN for the rest: "12abc" was 12, "1e3" was 1, "0x1F" was 31, an int past its range
/// kept every digit, a failed TryParse left NaN in its out, and a long's text became a number where
/// every other long is a BigInt. Each type now reads .NET's grammar under its own default style, or
/// the one the call names, and a failure throws .NET's exception with .NET's words.
/// </summary>
public class NumberTextConformanceTests
{
    [SkippableTheory]
    // ---- int: the Integer style, whitespace and a leading sign ----
    [InlineData("int.TryParse(\"x\", out var n); return n;")]                                                          // 0
    [InlineData("var ok = int.TryParse(\"12abc\", out var n); return (ok ? \"ok \" : \"no \") + n;")]                  // "no 0"
    [InlineData("var ok = int.TryParse(\"1e3\", out var n); return (ok ? \"ok \" : \"no \") + n;")]                    // "no 0"
    [InlineData("var ok = int.TryParse(\"1,000\", out var n); return (ok ? \"ok \" : \"no \") + n;")]                  // "no 0"
    [InlineData("var ok = int.TryParse(\"2147483648\", out var n); return (ok ? \"ok \" : \"no \") + n;")]             // "no 0" — overflow
    [InlineData("var ok = int.TryParse(\" 7 \", out var n); return (ok ? \"ok \" : \"no \") + n;")]                    // "ok 7"
    [InlineData("int n = 9; var ok = int.TryParse(\"x\", out n); return (ok ? \"ok \" : \"no \") + n;")]               // "no 0" — the out is written
    [InlineData("string s = null; var ok = int.TryParse(s, out var n); return (ok ? \"ok \" : \"no \") + n;")]         // "no 0"
    [InlineData("try { return int.Parse(\"12abc\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return int.Parse(\"0x1F\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return int.Parse(\"\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return int.Parse(\"   \"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return int.Parse(\"-\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("string s = null; try { return int.Parse(s); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return int.Parse(\"2147483648\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return int.Parse(\"-2147483649\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("return int.Parse(\"-2147483648\");")]
    [InlineData("return int.Parse(\"2147483647\");")]
    // Format errors take precedence over overflow: the digits are read to the end first.
    [InlineData("try { return int.Parse(\"99999999999x\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return int.Parse(\"99999999999 \"); } catch (Exception e) { return e.Message; }")]
    [InlineData("return int.Parse(\"+0005\");")]                                                                        // 5
    [InlineData("return int.Parse(\"-0\");")]                                                                           // 0
    [InlineData("return int.Parse(\"000000000000000000042\");")]                                                        // 42: zeros are not digits
    [InlineData("return int.Parse(\"5\\0\\0\");")]                                                                      // 5: trailing nulls are ignored
    [InlineData("try { return int.Parse(\"5\\0 \"); } catch (Exception e) { return e.Message; }")]                     // nothing after the nulls
    // A no-break space is not .NET's white. It leaves the message as `_`: .NET's JSON encoder escapes
    // a space separator and JSON.stringify does not, so the harness would compare two spellings.
    [InlineData("try { return int.Parse(\"\\u00A05\"); } catch (Exception e) { return e.Message.Replace('\\u00A0', '_'); }")]
    [InlineData("return int.Parse(\"\\t\\n5\\r\\n\");")]                                                                // 5
    [InlineData("try { return int.Parse(\"- 5\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return int.Parse(\"5-\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return int.Parse(\"٣\"); } catch (Exception e) { return e.Message; }")]                         // only ASCII digits
    [InlineData("return int.Parse(\"-7\", System.Globalization.CultureInfo.InvariantCulture);")]                        // -7
    // ---- int: the styles a call names ----
    [InlineData("return int.Parse(\"1,000\", System.Globalization.NumberStyles.AllowThousands);")]                      // 1000
    [InlineData("return int.Parse(\"1e3\", System.Globalization.NumberStyles.AllowExponent);")]                         // 1000
    [InlineData("return int.Parse(\"1.0\", System.Globalization.NumberStyles.AllowDecimalPoint);")]                     // 1
    [InlineData("try { return int.Parse(\"1.5\", System.Globalization.NumberStyles.AllowDecimalPoint); } catch (Exception e) { return e.Message; }")] // a fraction overflows
    [InlineData("try { return int.Parse(\"0.5\", System.Globalization.NumberStyles.Float); } catch (Exception e) { return e.Message; }")]
    [InlineData("return int.Parse(\"2.5e1\", System.Globalization.NumberStyles.Float);")]                               // 25
    [InlineData("return int.Parse(\"-2147483648.000\", System.Globalization.NumberStyles.Float);")]
    [InlineData("try { return int.Parse(\"2147483647.00000000000001\", System.Globalization.NumberStyles.Float); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return int.Parse(\"1e10\", System.Globalization.NumberStyles.Float); } catch (Exception e) { return e.Message; }")]
    [InlineData("return int.Parse(\"(5)\", System.Globalization.NumberStyles.AllowParentheses);")]                      // -5
    [InlineData("return int.Parse(\"5-\", System.Globalization.NumberStyles.AllowTrailingSign);")]                      // -5
    [InlineData("return int.Parse(\"¤5\", System.Globalization.NumberStyles.Currency);")]                               // 5
    [InlineData("return int.Parse(\"  (1,234)  \", System.Globalization.NumberStyles.Any);")]                           // -1234
    [InlineData("return int.Parse(\"1F\", System.Globalization.NumberStyles.HexNumber);")]                              // 31
    [InlineData("return int.Parse(\"ffffffff\", System.Globalization.NumberStyles.HexNumber);")]                        // -1: the bits
    [InlineData("return int.Parse(\"  00000000000000007FFFFFFF  \", System.Globalization.NumberStyles.HexNumber);")]
    [InlineData("try { return int.Parse(\"1FFFFFFFF\", System.Globalization.NumberStyles.HexNumber); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return int.Parse(\"1FFFFFFFFx\", System.Globalization.NumberStyles.HexNumber); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return int.Parse(\"0x1F\", System.Globalization.NumberStyles.HexNumber); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return int.Parse(\"-1F\", System.Globalization.NumberStyles.HexNumber); } catch (Exception e) { return e.Message; }")]
    [InlineData("return int.Parse(\"101\", System.Globalization.NumberStyles.BinaryNumber);")]                          // 5
    [InlineData("return int.Parse(\"11111111111111111111111111111111\", System.Globalization.NumberStyles.AllowBinarySpecifier);")] // -1
    [InlineData("try { return int.Parse(\"5\", System.Globalization.NumberStyles.AllowHexSpecifier | System.Globalization.NumberStyles.AllowLeadingSign); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return int.Parse(\"5\", (System.Globalization.NumberStyles)4096); } catch (Exception e) { return e.Message; }")]
    [InlineData("var ok = int.TryParse(\"1,000\", System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var n); return (ok ? \"ok \" : \"no \") + n;")] // "ok 1000"
    [InlineData("var ok = int.TryParse(\"1.5\", System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var n); return (ok ? \"ok \" : \"no \") + n;")]   // "no 0"
    [InlineData("return int.Parse(style: System.Globalization.NumberStyles.AllowThousands, s: \"2,000\");")]            // 2000
    [InlineData("var log = \"\"; string S() { log += \"s\"; return \"FF\"; } System.Globalization.NumberStyles St() { log += \"t\"; return System.Globalization.NumberStyles.HexNumber; } var n = int.Parse(style: St(), s: S()); return log + \" \" + n;")] // "ts 255"
    // ---- the other widths ----
    [InlineData("return uint.Parse(\"4294967295\");")]
    [InlineData("return uint.Parse(\"-0\");")]                                                                          // 0: a zero has no sign
    [InlineData("try { return uint.Parse(\"-1\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return uint.Parse(\"4294967296\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return uint.Parse(\"-0.0\", System.Globalization.NumberStyles.Float); } catch (Exception e) { return e.Message; }")]
    [InlineData("return uint.Parse(\"-0\", System.Globalization.NumberStyles.Float);")]
    [InlineData("return uint.Parse(\"FFFFFFFF\", System.Globalization.NumberStyles.HexNumber);")]
    [InlineData("return long.Parse(\"9007199254740993\").ToString();")]                                                  // exact
    [InlineData("return (long.Parse(\"5\") + 1L).ToString();")]                                                         // "6": a BigInt
    [InlineData("var ok = long.TryParse(\"x\", out var n); return (ok ? \"ok \" : \"no \") + n;")]                     // "no 0"
    [InlineData("var ok = long.TryParse(\"-9223372036854775808\", out var n); return (ok ? \"ok \" : \"no \") + (n - 1L + 1L);")]
    [InlineData("try { return long.Parse(\"9223372036854775808\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("return long.Parse(\"ffffffffffffffff\", System.Globalization.NumberStyles.HexNumber).ToString();")]   // -1
    [InlineData("return long.Parse(\"1e18\", System.Globalization.NumberStyles.Float).ToString();")]
    [InlineData("return ulong.Parse(\"18446744073709551615\").ToString();")]
    [InlineData("try { return ulong.Parse(\"18446744073709551616\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("var ok = ulong.TryParse(\"-5\", out var n); return (ok ? \"ok \" : \"no \") + n;")]                   // "no 0"
    [InlineData("return short.Parse(\"-32768\");")]
    [InlineData("try { return short.Parse(\"32768\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("return short.Parse(\"FFFF\", System.Globalization.NumberStyles.HexNumber);")]                          // -1
    [InlineData("return ushort.Parse(\"65535\");")]
    [InlineData("try { return ushort.Parse(\"65536\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("return byte.Parse(\"255\");")]
    [InlineData("try { return byte.Parse(\"256\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return byte.Parse(\"-1\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("return sbyte.Parse(\"-128\");")]
    [InlineData("try { return sbyte.Parse(\"128\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("return sbyte.Parse(\"FF\", System.Globalization.NumberStyles.HexNumber);")]                            // -1
    [InlineData("var ok = byte.TryParse(\"300\", out var b); return (ok ? \"ok \" : \"no \") + b;")]                   // "no 0"
    [InlineData("var ok = short.TryParse(\" -12 \", out var s); return (ok ? \"ok \" : \"no \") + s;")]                // "ok -12"
    public void AnIntegerFromText_IsDotNets(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // ---- double: Float | AllowThousands ----
    [InlineData("double.TryParse(\"x\", out var d); return d;")]                                                        // 0
    [InlineData("var ok = double.TryParse(\"12abc\", out var d); return (ok ? \"ok \" : \"no \") + d;")]               // "no 0"
    [InlineData("try { return double.Parse(\"abc\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return double.Parse(\"0x10\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return double.Parse(\"\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return double.Parse(\"1.5e\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return double.Parse(\"1.5.0\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("string s = null; try { return double.Parse(s).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("return double.Parse(\"1,5\").ToString();")]                                                           // "15": the invariant , groups
    [InlineData("return double.Parse(\"1,234.5\").ToString();")]                                                       // "1234.5"
    [InlineData("return double.Parse(\"  -1.5e3  \").ToString();")]                                                    // "-1500"
    [InlineData("return double.Parse(\".5\").ToString();")]                                                            // "0.5"
    [InlineData("return double.Parse(\"5.\").ToString();")]                                                            // "5"
    [InlineData("return double.Parse(\"1E+2\").ToString();")]                                                          // "100"
    [InlineData("return double.Parse(\"0.1\").ToString();")]                                                           // "0.1"
    [InlineData("return (double.Parse(\"0.1\") + double.Parse(\"0.2\")).ToString();")]                                 // "0.30000000000000004"
    [InlineData("return double.Parse(\"123456789012345678901234567890\").ToString();")]
    [InlineData("return double.Parse(\"2.2250738585072011e-308\").ToString();")]
    [InlineData("return double.Parse(\"4.9406564584124654e-324\").ToString();")]                                       // the smallest subnormal
    [InlineData("return double.Parse(\"2.4703282292062328e-324\").ToString();")]                                       // just past half of it
    [InlineData("return double.Parse(\"1e-400\").ToString();")]                                                        // "0"
    [InlineData("return double.Parse(\"1.7976931348623157e308\").ToString();")]
    [InlineData("return double.Parse(\"1e309\").ToString();")]                                                         // too large: infinity, no throw
    [InlineData("return double.Parse(\"-1e309\").ToString();")]
    [InlineData("return double.Parse(\"1e999999999999\").ToString();")]                                                // an exponent past its limit
    [InlineData("return double.Parse(\"-0\").ToString();")]                                                            // "-0"
    [InlineData("return (1 / double.Parse(\"-0.0\")).ToString();")]                                                    // negative infinity
    [InlineData("return double.Parse(\"(1.5)\", System.Globalization.NumberStyles.Any).ToString();")]                  // "-1.5"
    [InlineData("try { return double.Parse(\"(1.5)\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("return double.Parse(\"5\\0\").ToString();")]
    // ---- the symbols: the invariant culture's, in any case, trimmed of .NET's whitespace ----
    [InlineData("return double.Parse(\"Infinity\").ToString();")]
    [InlineData("return double.Parse(\"-infinity\").ToString();")]
    [InlineData("return double.Parse(\"+INFINITY\").ToString();")]
    [InlineData("return double.IsNaN(double.Parse(\"NaN\"));")]
    [InlineData("return double.IsNaN(double.Parse(\" nan \"));")]
    [InlineData("return double.IsNaN(double.Parse(\"-NaN\"));")]
    [InlineData("return double.IsNaN(double.Parse(\"+nan\"));")]
    [InlineData("return double.Parse(\"\\u00A0Infinity\\u2003\").ToString();")]                                        // the symbols trim more
    [InlineData("try { return double.Parse(\"Infinityx\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return double.Parse(\"+-Infinity\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return double.Parse(\"∞\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("var ok = double.TryParse(\"-Infinity\", out var d); return (ok ? \"ok \" : \"no \") + d;")]
    // ---- the styles a call names ----
    [InlineData("return double.Parse(\"1e5\", System.Globalization.NumberStyles.Float).ToString();")]
    [InlineData("try { return double.Parse(\"1,5\", System.Globalization.NumberStyles.Float).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return double.Parse(\"1e5\", System.Globalization.NumberStyles.AllowDecimalPoint).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("return double.Parse(\"¤1,234.5\", System.Globalization.NumberStyles.Currency).ToString();")]
    [InlineData("try { return double.Parse(\"1\", System.Globalization.NumberStyles.AllowHexSpecifier).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return double.Parse(\"1\", (System.Globalization.NumberStyles)4096).ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("var ok = double.TryParse(\"1e5\", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d); return (ok ? \"ok \" : \"no \") + d;")]
    [InlineData("return double.Parse(provider: System.Globalization.CultureInfo.InvariantCulture, s: \"2.5\").ToString();")]
    // ---- float: read ONCE into a single ----
    [InlineData("try { return float.Parse(\"abc\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("float f = float.Parse(\"0.1\"); return ((double)f).ToString();")]                                     // 0.10000000149011612
    [InlineData("var ok = float.TryParse(\"x\", out var f); return (ok ? \"ok \" : \"no \") + f;")]                    // "no 0"
    [InlineData("return float.Parse(\"3.4028235e38\").ToString();")]
    [InlineData("return float.Parse(\"3.5e38\").ToString();")]                                                         // infinity
    [InlineData("return float.Parse(\"-0\").ToString();")]
    // Through a double first, these round twice and land on the wrong single.
    [InlineData("float f = float.Parse(\"1.0000000596046447753906250001\"); return ((double)f).ToString();")]          // 1.0000001192092896
    [InlineData("float f = float.Parse(\"1.0000000596046447753906249999\"); return ((double)f).ToString();")]          // 1
    [InlineData("float f = float.Parse(\"1.000000059604644775390625\"); return ((double)f).ToString();")]              // a true tie: even
    [InlineData("float f = float.Parse(\"1.000000178813934326171875\"); return ((double)f).ToString();")]              // a true tie: even, upward
    [InlineData("float f = float.Parse(\"340282356779733661637539395458142568447\"); return ((double)f).ToString();")] // FLT_MAX, not infinity
    [InlineData("float f = float.Parse(\"340282356779733661637539395458142568448\"); return ((double)f).ToString();")] // the threshold: infinity
    [InlineData("float f = float.Parse(\"7.0064923216240861e-46\"); return ((double)f).ToString();")]                  // past half the smallest: it
    [InlineData("float f = float.Parse(\"7.00649232162408535461864791644958065640130970938257885878534141944895541342930300743319094181060791015625e-46\"); return ((double)f).ToString();")] // the tie: 0
    [InlineData("float f = float.Parse(\"7.006492321624085354618647916449580656401309709382578858785341419448955413429303007433190941810607910156251e-46\"); return ((double)f).ToString();")] // past the tie
    [InlineData("float f = float.Parse(\"-1.0000000596046447753906250001\"); return ((double)f).ToString();")]
    public void ARealFromText_IsDotNets(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [InlineData("try { return Convert.ToInt32(\"12abc\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToDouble(\"abc\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("return Convert.ToInt32(\"  42  \");")]                                                                 // 42
    [InlineData("string s = null; return Convert.ToInt32(s);")]                                                        // 0: where Parse throws
    [InlineData("string s = null; return Convert.ToDouble(s).ToString();")]                                            // "0"
    // A bare null has no type of its own and binds to the string overload all the same.
    [InlineData("return Convert.ToInt32(null);")]                                                                       // 0
    [InlineData("return Convert.ToInt64(null).ToString();")]                                                            // "0"
    [InlineData("return Convert.ToDouble(null).ToString();")]                                                           // "0"
    [InlineData("return Convert.ToInt64(\"9007199254740993\").ToString();")]
    [InlineData("return (Convert.ToInt64(\"5\") + 1L).ToString();")]
    [InlineData("try { return Convert.ToByte(\"256\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return Convert.ToInt16(\"1e3\"); } catch (Exception e) { return e.Message; }")]
    [InlineData("return Convert.ToUInt64(\"18446744073709551615\").ToString();")]
    [InlineData("return Convert.ToUInt32(\"4294967295\");")]
    [InlineData("return Convert.ToSByte(\"-128\");")]
    [InlineData("float f = Convert.ToSingle(\"0.1\"); return ((double)f).ToString();")]
    [InlineData("float f = Convert.ToSingle(\"1.0000000596046447753906250001\"); return ((double)f).ToString();")]
    [InlineData("return Convert.ToDouble(\"1,5\", System.Globalization.CultureInfo.InvariantCulture).ToString();")]  // "15"
    [InlineData("return Convert.ToInt32(provider: System.Globalization.CultureInfo.InvariantCulture, value: \"7\");")]
    public void ConvertOfText_IsDotNets(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
