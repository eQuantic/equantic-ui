using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The Int64 static surface (a long is a BigInt here, so its table is arrows, not Math.*) and
/// CompareTo across the primitive families — including the two the OLD subtraction bucket got
/// wrong at runtime: long (Math.sign of a BigInt throws) and the boolean/char/Guid receivers that
/// were simply fenced. Long values cross the JSON seam as strings, so the cases observe them via
/// ToString / numeric folds.
/// </summary>
public class Int64AndCompareConformanceTests
{
    [SkippableTheory]
    // Int64 statics — BigInt arrows.
    [InlineData("long.Abs(-7L).ToString()")]                          // "7"
    [InlineData("long.Max(3L, 9L).ToString()")]                       // "9"
    [InlineData("long.Min(3L, 9L).ToString()")]                       // "3"
    [InlineData("long.Clamp(15L, 0L, 10L).ToString()")]               // "10"
    [InlineData("long.Clamp(-5L, 0L, 10L).ToString()")]               // "0"
    [InlineData("long.Sign(-4L)")]                                    // -1 — an int, not a long
    [InlineData("long.Sign(0L)")]                                     // 0
    [InlineData("long.IsPositive(0L)")]                               // true
    [InlineData("long.IsNegative(-1L)")]                              // true
    [InlineData("long.IsEvenInteger(4L)")]                            // true
    [InlineData("long.IsOddInteger(-3L)")]                            // true
    [InlineData("long.MaxValue.ToString()")]                          // "9223372036854775807"
    [InlineData("long.MinValue.ToString()")]                          // "-9223372036854775808"
    // CompareTo — every family, sign observed through the comparisons call sites actually make.
    [InlineData("(3).CompareTo(9)")]                                  // negative → sign
    [InlineData("(9).CompareTo(3) > 0")]                              // true
    [InlineData("(3).CompareTo(3)")]                                  // 0
    [InlineData("\"a\".CompareTo(\"b\")")]                            // -1 (ordinal policy)
    [InlineData("'a'.CompareTo('b')")]                                // -1 — code-unit subtraction
    [InlineData("'z'.CompareTo('a')")]                                // 25 — NOT a normalized sign
    [InlineData("true.CompareTo(false)")]                             // 1
    [InlineData("false.CompareTo(true)")]                             // -1
    [InlineData("true.CompareTo(true)")]                              // 0
    [InlineData("3L.CompareTo(9L)")]                                  // -1 — the old bucket THREW here
    [InlineData("9L.CompareTo(3L)")]                                  // 1
    [InlineData("3L.CompareTo(3L)")]                                  // 0
    public void Int64AndCompare_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
