using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A bool read from text answers as .NET's does, on both sides (#402): <c>bool.Parse</c>,
/// <c>bool.TryParse</c> and <c>Convert.ToBoolean(string)</c>. The twin read
/// <c>String(s).trim().toLowerCase() === 'true'</c>: it never threw, a null was the text "null", a
/// trailing NUL (which .NET trims) made "true\0" false, and TryParse had no translation at all.
/// </summary>
public class BooleanTextConformanceTests
{
    [SkippableTheory]
    // "True" or "False" in any case, trimmed of white space and NUL characters at both ends.
    [InlineData("return bool.Parse(\"true\") + \"|\" + bool.Parse(\"FALSE\") + \"|\" + bool.Parse(\" True \");")]   // "True|False|True"
    [InlineData("return bool.Parse(\"true\\0\") + \"|\" + bool.Parse(\"\\0false\");")]                                 // "True|False"
    // White space is char.IsWhiteSpace's: NBSP and NEL are, U+FEFF is not.
    [InlineData("return bool.Parse(\"\\u00A0true\\u0085\");")]                                                           // true
    // The message names the text, and .NET's JSON writer escapes U+FEFF where the other side's does
    // not, so the harness would compare two spellings of one string: the case swaps it for a marker.
    [InlineData("try { return bool.Parse(\"\\uFEFFtrue\").ToString(); } catch (Exception e) { return e.Message.Replace(\"\\uFEFF\", \"[FEFF]\"); }")]
    // Nothing else is a bool: not a number, not a word with a space in it, not a long s for an s.
    [InlineData("try { return bool.Parse(\"abc\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return bool.Parse(\"1\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return bool.Parse(\"tr ue\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return bool.Parse(\"Fal\\u017Fe\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { return bool.Parse(\"\").ToString(); } catch (Exception e) { return e.Message; }")]
    [InlineData("try { string s = null; return bool.Parse(s).ToString(); } catch (Exception e) { return e.Message; }")]
    // TryParse answers false and leaves false in its out, the out written either way.
    [InlineData("var ok = bool.TryParse(\"x\", out var v); return ok + \"|\" + v;")]                                  // "False|False"
    [InlineData("bool v = true; var ok = bool.TryParse(\"x\", out v); return ok + \"|\" + v;")]                       // "False|False"
    [InlineData("string s = null; var ok = bool.TryParse(s, out var v); return ok + \"|\" + v;")]                     // "False|False"
    [InlineData("var ok = bool.TryParse(\" false \", out var v); return ok + \"|\" + v;")]                            // "True|False"
    [InlineData("return bool.TryParse(\"TRUE\", out _);")]                                                              // true
    // Convert.ToBoolean(string): a null is false, any other text reads as Parse does.
    [InlineData("string s = null; return Convert.ToBoolean(s);")]                                                       // false
    [InlineData("return Convert.ToBoolean(\" False \") + \"|\" + Convert.ToBoolean(\"true\");")]                      // "False|True"
    [InlineData("try { return Convert.ToBoolean(\"abc\").ToString(); } catch (Exception e) { return e.Message; }")]
    public void ABoolFromText_ReadsAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// Every other overload of <c>Convert.ToBoolean</c>, by the type C# binds (found in review, #421):
    /// the lowering compared any value with zero, so a false bool was true (<c>false !== 0</c>).
    /// </summary>
    [SkippableTheory]
    // A bool is itself.
    [InlineData("return Convert.ToBoolean(false) + \"|\" + Convert.ToBoolean(true);")]                                // "False|True"
    [InlineData("bool b = false; return Convert.ToBoolean(b);")]                                                        // false
    // A number is whether it is not zero, in every width.
    [InlineData("return Convert.ToBoolean((byte)0) + \"|\" + Convert.ToBoolean((byte)7) + \"|\" + Convert.ToBoolean((sbyte)-1);")] // "False|True|True"
    [InlineData("return Convert.ToBoolean((short)0) + \"|\" + Convert.ToBoolean((ushort)3);")]                         // "False|True"
    [InlineData("return Convert.ToBoolean(1) + \"|\" + Convert.ToBoolean(0) + \"|\" + Convert.ToBoolean(0u);")]        // "True|False|False"
    [InlineData("return Convert.ToBoolean(0L) + \"|\" + Convert.ToBoolean(long.MinValue) + \"|\" + Convert.ToBoolean(0UL) + \"|\" + Convert.ToBoolean(ulong.MaxValue);")] // "False|True|False|True"
    // A NaN is not zero, and a negative zero is.
    [InlineData("return Convert.ToBoolean(0.0) + \"|\" + Convert.ToBoolean(-0.0) + \"|\" + Convert.ToBoolean(double.NaN) + \"|\" + Convert.ToBoolean(0.25);")] // "False|False|True|True"
    [InlineData("return Convert.ToBoolean(0f) + \"|\" + Convert.ToBoolean(float.NaN);")]                               // "False|True"
    [InlineData("return Convert.ToBoolean(0m) + \"|\" + Convert.ToBoolean(0.00m) + \"|\" + Convert.ToBoolean(-0.5m);")] // "False|False|True"
    // A char and a date have no bool, and .NET says so.
    [InlineData("try { return Convert.ToBoolean('a').ToString(); } catch (InvalidCastException e) { return e.Message; }")]
    [InlineData("try { return Convert.ToBoolean(new DateTime(2026, 1, 2)).ToString(); } catch (InvalidCastException e) { return e.Message; }")]
    // Text with a provider reads as text without one.
    [InlineData("return Convert.ToBoolean(\" TRUE \", System.Globalization.CultureInfo.InvariantCulture);")]          // true
    // A provider is not consulted, and it is still evaluated, after the value, as C# evaluates every
    // argument: its side effect happens and its exception is thrown (found in review, #421).
    [InlineData("int n = 0; IFormatProvider P() { n++; return null; } var b = Convert.ToBoolean(\"true\", P()); return b + \"|\" + n;")] // "True|1"
    [InlineData("int n = 0; IFormatProvider P() { n++; return null; } object o = 1; var b = Convert.ToBoolean(o, P()); return b + \"|\" + n;")] // "True|1"
    [InlineData("var order = \"\"; string V() { order += \"v\"; return \"true\"; } IFormatProvider P() { order += \"p\"; return null; } Convert.ToBoolean(V(), P()); return order;")] // "vp"
    [InlineData("IFormatProvider P() => throw new InvalidOperationException(\"provider\"); try { return Convert.ToBoolean(\"true\", P()).ToString(); } catch (InvalidOperationException e) { return e.Message; }")] // "provider"
    public void ConvertToBoolean_TakesEachOverloadAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
