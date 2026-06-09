using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for case-insensitive / <c>StringComparison</c>-aware string operations — the common
/// "compare ignoring case" need. Scope is the deterministic bool/index operations
/// (<c>Equals</c>/<c>StartsWith</c>/<c>EndsWith</c>/<c>Contains</c>/<c>IndexOf</c>) under Ordinal and
/// the IgnoreCase variants. Culture-sensitive ordering (<c>CompareTo</c>) is intentionally out of scope
/// (it diverges from JS code-unit order on case).
/// </summary>
public class StringComparisonConformanceTests
{
    [SkippableTheory]
    // Equals (instance), with/without StringComparison
    [InlineData("\"Hello\".Equals(\"hello\")")]                                              // false
    [InlineData("\"Hello\".Equals(\"Hello\")")]                                              // true
    [InlineData("\"Hello\".Equals(\"hello\", StringComparison.OrdinalIgnoreCase)")]          // true
    [InlineData("\"Hello\".Equals(\"hello\", StringComparison.Ordinal)")]                    // false
    [InlineData("\"Hello\".Equals(\"hello\", StringComparison.InvariantCultureIgnoreCase)")] // true
    // StartsWith
    [InlineData("\"Hello World\".StartsWith(\"hello\", StringComparison.OrdinalIgnoreCase)")] // true
    [InlineData("\"Hello World\".StartsWith(\"hello\")")]                                     // false
    [InlineData("\"Hello World\".StartsWith(\"Hello\", StringComparison.Ordinal)")]           // true
    // EndsWith
    [InlineData("\"Hello World\".EndsWith(\"WORLD\", StringComparison.OrdinalIgnoreCase)")]   // true
    [InlineData("\"Hello World\".EndsWith(\"world\")")]                                       // false
    // Contains
    [InlineData("\"Hello World\".Contains(\"LO W\", StringComparison.OrdinalIgnoreCase)")]    // true
    [InlineData("\"Hello World\".Contains(\"lo w\")")]                                        // false
    [InlineData("\"Hello World\".Contains(\"lo W\")")]                                        // true
    // IndexOf
    [InlineData("\"Hello World\".IndexOf(\"WORLD\", StringComparison.OrdinalIgnoreCase)")]    // 6
    [InlineData("\"Hello World\".IndexOf(\"world\")")]                                        // -1
    [InlineData("\"Hello World\".IndexOf(\"o\", StringComparison.Ordinal)")]                  // 4
    // string.Equals (static) with StringComparison
    [InlineData("string.Equals(\"A\", \"a\", StringComparison.OrdinalIgnoreCase)")]           // true
    [InlineData("string.Equals(\"A\", \"a\")")]                                               // false
    public void StringComparison_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
