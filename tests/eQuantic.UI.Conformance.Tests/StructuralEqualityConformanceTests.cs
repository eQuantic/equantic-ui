using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for value (structural) equality: records and value tuples compare by VALUE — a place
/// where naive JS reference equality (<c>===</c> / <c>includes</c>) silently diverges. Records are
/// modelled as plain objects (positional <c>new Point(1,2)</c> → <c>{x,y}</c>), tuples as arrays.
/// </summary>
public class StructuralEqualityConformanceTests
{
    private const string Prelude = "public record Point(int X, int Y);";

    [SkippableTheory]
    // Record == / != (records define value ==/!=)
    [InlineData("new Point(1, 2) == new Point(1, 2)")]   // true
    [InlineData("new Point(1, 2) == new Point(1, 3)")]   // false
    [InlineData("new Point(1, 2) != new Point(3, 4)")]   // true
    [InlineData("new Point(1, 2) != new Point(1, 2)")]   // false
    // `with` produces a copy with members replaced (structural)
    [InlineData("(new Point(1, 2) with { Y = 9 }).Y")]                 // 9
    [InlineData("(new Point(1, 2) with { Y = 9 }) == new Point(1, 9)")] // true
    [InlineData("(new Point(1, 2) with { X = 5, Y = 6 }) == new Point(5, 6)")] // true
    // Contains is value-based for records
    [InlineData("new[] { new Point(1, 2), new Point(3, 4) }.Contains(new Point(3, 4))")] // true
    [InlineData("new[] { new Point(1, 2) }.Contains(new Point(9, 9))")]                  // false
    // Instance .Equals is value-based for records
    [InlineData("new Point(1, 2).Equals(new Point(1, 2))")] // true
    [InlineData("new Point(1, 2).Equals(new Point(9, 9))")] // false
    public void Records_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, Prelude);
    }

    [SkippableTheory]
    // record struct — positional params are the fields, so value ==/Equals work the same way.
    [InlineData("new RP(1, 2) == new RP(1, 2)")]          // true
    [InlineData("new RP(1, 2) == new RP(1, 3)")]          // false
    [InlineData("new RP(1, 2).Equals(new RP(1, 2))")]     // true
    [InlineData("(new RP(1, 2) with { B = 9 }) == new RP(1, 9)")] // true
    public void RecordStructs_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, "public readonly record struct RP(int A, int B);");
    }

    [SkippableTheory]
    // Value tuples compare element-wise
    [InlineData("(1, 2) == (1, 2)")]        // true
    [InlineData("(1, 2) == (1, 3)")]        // false
    [InlineData("(1, 2) != (3, 4)")]        // true
    [InlineData("(1, \"a\") == (1, \"a\")")] // true
    [InlineData("(1, \"a\") == (1, \"b\")")] // false
    public void Tuples_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }

    [SkippableTheory]
    // Distinct dedups records by value (not reference); Count reflects the unique set.
    [InlineData("var pts = new List<Point> { new Point(1, 2), new Point(1, 2), new Point(3, 4) }; return pts.Distinct().Count();")] // 2
    // A record-keyed lookup pattern via value Contains.
    [InlineData("var pts = new List<Point> { new Point(1, 2), new Point(3, 4) }; return pts.Contains(new Point(3, 4));")]            // true
    [InlineData("var seen = new List<Point> { new Point(1, 2) }; return seen.Contains(new Point(2, 1));")]                          // false
    public void RecordStatements_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Prelude);
    }
}
