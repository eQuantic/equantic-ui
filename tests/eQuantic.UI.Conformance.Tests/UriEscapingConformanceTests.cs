using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>Uri.EscapeDataString</c>, <c>Uri.EscapeUriString</c> and <c>Uri.UnescapeDataString</c>, run on both sides
/// (#1805). They were lowered to <c>encodeURIComponent</c>, <c>encodeURI</c> and <c>decodeURIComponent</c>,
/// which escape other characters: a WhatsApp link a real site built in a component carried <c>%21</c> and
/// <c>%28</c> from the server and <c>!</c> and <c>(</c> from the browser, which rewrote it once the page
/// hydrated. And where .NET writes a lone surrogate as U+FFFD and leaves a malformed escape as it is, the
/// browser's functions threw.
/// </summary>
public class UriEscapingConformanceTests
{
    /// <summary>The UTF-16 code units of a text, which compares a character past the BMP on both sides alike.</summary>
    private static string Units(string expression) =>
        "var t = " + expression + "; var u = \"\"; foreach (var c in t) u += ((int)c).ToString(\"X4\") + \" \"; return u;";

    /// <summary>Every printable ASCII character, from the space to the tilde.</summary>
    private const string Printable =
        "var s = \"\"; for (var c = ' '; c <= '~'; c++) s += c; ";

    [SkippableTheory]
    [InlineData(Printable + "return Uri.EscapeDataString(s);")]                        // only A-Z a-z 0-9 - . _ ~ kept
    [InlineData("return Uri.EscapeDataString(\"Hello! (cake) it's *the* one\");")]     // the five encodeURIComponent keeps
    [InlineData("return Uri.EscapeDataString(\"Ol\\u00e1 \\u2022 \\ud83d\\ude00\");")] // UTF-8, upper-case hex
    [InlineData("return Uri.EscapeDataString(\"\\t\\n\\u007f\");")]
    [InlineData("return Uri.EscapeDataString(\"\");")]
    [InlineData("return Uri.EscapeDataString(\"50% a%41\");")]                         // the percent sign too
    [InlineData("return Uri.EscapeDataString(\"\\ud800\");")]                          // a lone high surrogate is U+FFFD
    [InlineData("return Uri.EscapeDataString(\"a\\udc00b\");")]                        // and a lone low one
    [InlineData("return Uri.EscapeDataString(\"\\ud83d\");")]
    public void EscapeDataString_EscapesAsDotNetEscapes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    [InlineData(Printable + "return Uri.EscapeUriString(s);")]                         // [ and ] kept, which encodeURI escapes
    [InlineData("return Uri.EscapeUriString(\"http://x/a b?q=[1]&r=(2)#f\");")]
    [InlineData("return Uri.EscapeUriString(\"50% a%41 \\u00e1\");")]
    [InlineData("return Uri.EscapeUriString(\"\\ud800x\");")]
    public void EscapeUriString_EscapesAsDotNetEscapes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet("#pragma warning disable SYSLIB0013\n" + statements);
    }

    /// <summary>What decodes, and what .NET leaves as it is: an escape that is no escape, a byte that starts no
    /// UTF-8 character or is cut short, and a surrogate written in UTF-8, each beside what does decode.</summary>
    [SkippableTheory]
    [InlineData("return Uri.UnescapeDataString(\"Ol%C3%A1%21%20%28cake%29\");")]
    [InlineData("return Uri.UnescapeDataString(\"a%2Bb+c\");")]                        // a plus stays a plus
    [InlineData("return Uri.UnescapeDataString(\"%\");")]
    [InlineData("return Uri.UnescapeDataString(\"100%\");")]
    [InlineData("return Uri.UnescapeDataString(\"%2\");")]
    [InlineData("return Uri.UnescapeDataString(\"%zz%41\");")]
    [InlineData("return Uri.UnescapeDataString(\"%E0%A4%A\");")]                       // cut short
    [InlineData("return Uri.UnescapeDataString(\"%C3%28\");")]                         // a lead byte with no continuation
    [InlineData("return Uri.UnescapeDataString(\"%FF%41\");")]                         // a byte UTF-8 never has
    [InlineData("return Uri.UnescapeDataString(\"%ed%a0%80\");")]                      // a surrogate, which UTF-8 never encodes
    [InlineData("return Uri.UnescapeDataString(\"%C0%AF\");")]                         // an overlong slash
    [InlineData("return Uri.UnescapeDataString(\"%F4%90%80%80\");")]                   // past U+10FFFF
    [InlineData("return Uri.UnescapeDataString(\"%F0%9F%98%41\");")]                   // a sequence cut by an ASCII byte
    [InlineData("return Uri.UnescapeDataString(\"%80%E2%82%AC\");")]                   // a stray continuation, then a euro
    public void UnescapeDataString_UnescapesAsDotNetUnescapes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>A character past the BMP, unescaped and carried there and back, compared by its code units.</summary>
    [SkippableTheory]
    [InlineData("Uri.UnescapeDataString(\"%F0%9F%98%80 %c3%a1 %25\")")]
    [InlineData("Uri.UnescapeDataString(Uri.EscapeDataString(\"Hello! (cake) \\u00e1 \\ud83d\\ude00\"))")]
    [InlineData("Uri.UnescapeDataString(\"%F0%9F%98%80%F0%9F\")")]
    public void UnescapeDataString_DecodesWhatLiesPastTheBmp(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Units(expression));
    }

    /// <summary>A null text, refused with the parameter .NET names.</summary>
    [SkippableTheory]
    [InlineData("string s = null; try { Uri.EscapeDataString(s); return \"escaped\"; } catch (ArgumentNullException e) { return e.ParamName + \":\" + e.Message; }")]
    [InlineData("string s = null; try { Uri.UnescapeDataString(s); return \"unescaped\"; } catch (ArgumentNullException e) { return e.ParamName + \":\" + e.Message; }")]
    [InlineData("#pragma warning disable SYSLIB0013\nstring s = null; try { Uri.EscapeUriString(s); return \"escaped\"; } catch (ArgumentNullException e) { return e.ParamName + \":\" + e.Message; }")]
    public void ANullText_IsRefusedAsDotNetRefusesIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
