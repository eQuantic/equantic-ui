using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A NULLABLE compat value compared — the shape that crashed a calendar twice in one review.
/// <para>
/// In C# these comparisons are LIFTED and simply answer: <c>null == date</c> is false,
/// <c>null == null</c> is true, and every relational with a null operand is false. Nothing in the
/// C# reads as dangerous, which is exactly the problem: the twin lowered them to
/// <c>left.equals(right)</c> and <c>left.compareTo(right)</c>, which throw the moment either side
/// is null. A page rendered on the server and died on hydration.
/// </para>
/// <para>
/// The primitives were never affected (their lift already routed through <c>$eq.nullable.*</c>);
/// this is the compat family — DateTime, DateOnly, TimeOnly, TimeSpan, DateTimeOffset — whose
/// operators are METHODS on a runtime class.
/// </para>
/// </summary>
public class NullableCompatConformanceTests
{
    [SkippableTheory]
    // ---- DateOnly: the exact shape from the calendar ----
    [InlineData("DateOnly? a = null; var b = new DateOnly(2026, 7, 17); return a == b;")]                 // false
    [InlineData("DateOnly? a = new DateOnly(2026, 7, 17); var b = new DateOnly(2026, 7, 17); return a == b;")] // true
    [InlineData("DateOnly? a = null; var b = new DateOnly(2026, 7, 17); return a != b;")]                 // true
    [InlineData("DateOnly? a = null; DateOnly? b = null; return a == b;")]                                // true — both absent
    [InlineData("DateOnly? a = null; DateOnly? b = new DateOnly(2026, 1, 1); return a == b;")]            // false
    [InlineData("var a = new DateOnly(2026, 7, 17); DateOnly? b = null; return a == b;")]                 // false — null on the RIGHT
    // ---- relational: every one is false when an operand is absent ----
    [InlineData("DateOnly? a = null; var b = new DateOnly(2026, 7, 17); return a < b;")]                  // false
    [InlineData("DateOnly? a = null; var b = new DateOnly(2026, 7, 17); return a > b;")]                  // false
    [InlineData("DateOnly? a = null; var b = new DateOnly(2026, 7, 17); return a <= b;")]                 // false
    [InlineData("DateOnly? a = null; var b = new DateOnly(2026, 7, 17); return a >= b;")]                 // false
    [InlineData("DateOnly? a = new DateOnly(2026, 1, 1); var b = new DateOnly(2026, 7, 17); return a < b;")] // true
    [InlineData("DateOnly? a = new DateOnly(2026, 7, 17); var b = new DateOnly(2026, 7, 17); return a >= b;")] // true
    // ---- DateTime ----
    [InlineData("DateTime? a = null; var b = new DateTime(2026, 7, 17); return a == b;")]                 // false
    [InlineData("DateTime? a = new DateTime(2026, 7, 17); var b = new DateTime(2026, 7, 17); return a == b;")] // true
    [InlineData("DateTime? a = null; var b = new DateTime(2026, 7, 17); return a < b;")]                  // false
    [InlineData("DateTime? a = null; DateTime? b = null; return a != b;")]                                // false
    // ---- TimeSpan, TimeOnly, DateTimeOffset ----
    [InlineData("TimeSpan? a = null; var b = TimeSpan.FromMinutes(5); return a == b;")]                   // false
    [InlineData("TimeSpan? a = TimeSpan.FromMinutes(5); var b = TimeSpan.FromMinutes(5); return a == b;")] // true
    [InlineData("TimeSpan? a = null; var b = TimeSpan.FromMinutes(5); return a > b;")]                    // false
    [InlineData("TimeOnly? a = null; var b = new TimeOnly(10, 30); return a == b;")]                      // false
    [InlineData("TimeOnly? a = new TimeOnly(10, 30); var b = new TimeOnly(10, 30); return a <= b;")]       // true
    [InlineData("DateTimeOffset? a = null; var b = new DateTimeOffset(new DateTime(2026, 1, 2), TimeSpan.Zero); return a == b;")] // false
    // ---- and the ARITHMETIC the same branch models: null propagates, it does not throw ----
    [InlineData("DateTime? a = null; var b = TimeSpan.FromHours(1); var c = a + b; return c == null;")]   // true
    [InlineData("TimeSpan? a = null; var b = TimeSpan.FromHours(1); var c = a + b; return c == null;")]   // true
    [InlineData("DateTime? a = new DateTime(2026, 7, 17); var b = TimeSpan.FromDays(1); return (a + b).ToString();")] // the sum
    public void NullableCompatComparison_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
