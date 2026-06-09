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
    [InlineData("new StringBuilder().AppendLine(\"line1\").Append(\"line2\").ToString()")]            // "line1\nline2"
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
}
