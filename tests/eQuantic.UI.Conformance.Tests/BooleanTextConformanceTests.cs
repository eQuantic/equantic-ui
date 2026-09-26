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
    [InlineData("return Convert.ToBoolean(1) + \"|\" + Convert.ToBoolean(0);")]                                        // "True|False"
    public void ABoolFromText_ReadsAsDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
