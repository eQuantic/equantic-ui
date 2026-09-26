using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// White space is .NET's, on both sides: what <c>char.IsWhiteSpace</c> answers true for, and what
/// <c>Trim</c>, its two halves and <c>string.IsNullOrWhiteSpace</c> read. JavaScript's own set is a
/// different one: <c>\s</c> and <c>trim</c> leave U+0085 NEXT LINE, which .NET counts, and take
/// U+FEFF, which it does not (a format character). The code engine's word diff split its tokens on
/// it, and a review found the twin splitting where .NET did not.
/// </summary>
public class WhiteSpaceConformanceTests
{
    [SkippableTheory]
    [InlineData("char.IsWhiteSpace('\\u0085')")]              // true: NEXT LINE is .NET's
    [InlineData("char.IsWhiteSpace('\\uFEFF')")]              // false: a format character
    [InlineData("char.IsWhiteSpace('\\u00A0')")]              // true: a separator
    [InlineData("char.IsWhiteSpace('\\u2028')")]              // true: the line separator
    [InlineData("char.IsWhiteSpace('\\u180E')")]              // false: left the separators in Unicode 6.3
    [InlineData("char.IsWhiteSpace(\"a\\u0085\", 1)")]      // true
    [InlineData("\"\\u0085 a \\uFEFF\".TrimStart().Length")] // 3: the FEFF stays
    [InlineData("\"\\u0085 a \\uFEFF\".TrimEnd().Length")]   // 5: nothing at the end is white
    [InlineData("\"\\u0085 a \\uFEFF\".Trim() == \"a \\uFEFF\"")] // true
    [InlineData("string.IsNullOrWhiteSpace(\"\\u0085\")")]  // true
    [InlineData("string.IsNullOrWhiteSpace(\"\\uFEFF\")")]  // false
    [InlineData("string.Join(\"|\", \"a  b\\u0085c\".Split())")]  // "a||b|c": every white character splits
    [InlineData("\"a\\uFEFFb\".Split().Length")]                  // 1: the byte order mark splits nothing
    public void WhiteSpace_IsDotNets(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }

    /// <summary>Every code unit, counted and summed: two numbers that differ if any one of the
    /// 65,536 is classified differently.</summary>
    [SkippableFact]
    public void EveryCodeUnit_IsClassifiedAsDotNetDoes()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet("""
            var count = 0;
            var sum = 0;
            for (var unit = 0; unit < 65536; unit++)
            {
                if (!char.IsWhiteSpace((char)unit)) continue;
                count++;
                sum += unit;
            }
            return count + ":" + sum;
            """);
    }
}
