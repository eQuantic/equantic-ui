using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>Conformance: string members and operations behave identically in the emitted JS.</summary>
public class StringConformanceTests
{
    [SkippableTheory]
    [InlineData("\"Hello\".Length")]
    [InlineData("\"Hello\".ToUpper()")]
    [InlineData("\"Hello\".ToLower()")]
    [InlineData("\"Hello\".Substring(1)")]
    [InlineData("\"Hello\".Substring(1, 3)")]
    [InlineData("\"Hello\".IndexOf(\"l\")")]
    [InlineData("\"Hello\".IndexOf(\"z\")")]            // not found -> -1
    [InlineData("\"Hello\".Replace(\"l\", \"L\")")]
    [InlineData("\"Hello\".Contains(\"ell\")")]
    [InlineData("\"Hello\".StartsWith(\"He\")")]
    [InlineData("\"Hello\".EndsWith(\"lo\")")]
    [InlineData("\"  hi  \".Trim()")]
    [InlineData("\"a,b,c\".Split(',')")]                // -> ["a","b","c"]
    [InlineData("(\"Hello\" + \" \" + \"World\")")]
    [InlineData("$\"{1 + 1} and {2 * 2}\"")]            // interpolation -> "2 and 4"
    [InlineData("\"Hello\".Length > 3")]
    // Interpolated-string text edge cases (template-literal escaping).
    [InlineData("$\"{{literal}} {1 + 1}\"")]            // doubled braces -> "{literal} 2"
    [InlineData("$\"a {1} b}} c {{\"")]                 // trailing/leading escaped braces -> "a 1 b} c {"
    [InlineData("$\"price ${{0}}={1}\"")]              // $ before escaped brace -> "price ${0}=1"
    [InlineData("$@\"path\\to {1} end\"")]              // verbatim interpolated: backslash is literal
    [InlineData("@\"C:\\x\\y\"")]                        // verbatim literal: "C:\x\y"
    [InlineData("$\"\"\"raw {1} \"q\" end\"\"\"")]      // raw interpolated: literal quotes survive
    public void Strings_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
