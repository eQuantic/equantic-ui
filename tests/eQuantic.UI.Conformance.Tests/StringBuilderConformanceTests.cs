using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for <c>System.Text.StringBuilder</c> — the compat type. Fluent chains transpile to the
/// runtime <c>stringBuilder</c> and must produce the same final string as .NET, including the quirks:
/// <c>Append(bool)</c> -> "True"/"False" and <c>AppendLine</c> -> "\n" (Unix Environment.NewLine, which
/// is what the .NET evaluator uses on this platform).
/// </summary>
public class StringBuilderConformanceTests
{
    [SkippableTheory]
    [InlineData("new StringBuilder().Append(\"Hello\").Append(\" \").Append(\"World\").ToString()")] // "Hello World"
    [InlineData("new StringBuilder(\"a\").Append(\"b\").Append(\"c\").ToString()")]                  // "abc"
    [InlineData("new StringBuilder().Append(1).Append(2).Append(3).ToString()")]                      // "123"
    [InlineData("new StringBuilder().Append(true).Append(false).ToString()")]                         // "TrueFalse"
    [InlineData("new StringBuilder(\"hello\").Insert(0, \">>\").ToString()")]                         // ">>hello"
    [InlineData("new StringBuilder(\"a-b-c\").Replace(\"-\", \"+\").ToString()")]                     // "a+b+c"
    [InlineData("new StringBuilder(\"hello\").Remove(0, 2).ToString()")]                              // "llo"
    [InlineData("new StringBuilder(\"hello\").Clear().Append(\"x\").ToString()")]                     // "x"
    [InlineData("new StringBuilder(\"hello\").Length")]                                               // 5
    [InlineData("new StringBuilder().Append(\"ab\").Append(\"cd\").Length")]                          // 4
    public void StringBuilder_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
    /// <summary>
    /// The expressions whose .NET answer is the HOST's newline.
    ///
    /// <para>
    /// `Environment.NewLine` is `\r\n` on Windows and `\n` elsewhere, and a browser has no
    /// environment — so the SDK decided its newline is `\n`, and said so in both places that
    /// implement it. The decision was written down twice and held until the suite ran on a Windows
    /// runner for the first time.
    /// </para>
    ///
    /// <para>
    /// Folded on both sides, so a translation defect still fails. What this does NOT do is make the
    /// divergence go away: a page using these produces `\n` in the browser and `\r\n` from a
    /// Windows-hosted server's SSR pass, which the reconciler sees as a text mismatch at hydration.
    /// The fix is a product decision — fence the no-argument forms, or make the server honour the
    /// SDK's newline — and it is open.
    /// </para>
    /// </summary>
    [SkippableTheory]
    [InlineData("new StringBuilder().AppendLine(\"line1\").Append(\"line2\").ToString()",
        "AppendLine appends Environment.NewLine")]
    public void StringBuilder_WhereDotNetAnswersTheHostsNewline(string expression, string why)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNetExceptTheHostsNewline(expression, why);
    }
}
