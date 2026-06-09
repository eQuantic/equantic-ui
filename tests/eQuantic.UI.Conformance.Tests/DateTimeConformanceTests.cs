using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for .NET <c>DateTime</c> — the tick-precise compat type. JS <c>Date</c> formats and
/// computes nothing like .NET; these cases prove construction, components, Add* arithmetic, subtraction
/// (into a TimeSpan), and comparison match .NET exactly. Fixed values only (DateTime.Now is
/// non-deterministic). Results are compared via ToString()/numeric property/bool.
/// </summary>
public class DateTimeConformanceTests
{
    [SkippableTheory]
    // Construction + default invariant ToString (MM/dd/yyyy HH:mm:ss)
    [InlineData("new DateTime(2024, 1, 15).ToString()")]              // "01/15/2024 00:00:00"
    [InlineData("new DateTime(2024, 1, 5, 9, 3, 7).ToString()")]      // "01/05/2024 09:03:07"
    // Add* arithmetic
    [InlineData("new DateTime(2024, 1, 15).AddDays(20).ToString()")]  // "02/04/2024 00:00:00"
    [InlineData("new DateTime(2024, 1, 15).AddHours(25).ToString()")] // "01/16/2024 01:00:00"
    [InlineData("new DateTime(2024, 1, 31).AddMonths(1).ToString()")] // "02/29/2024 00:00:00" (clamp, leap)
    [InlineData("new DateTime(2023, 1, 31).AddMonths(1).ToString()")] // "02/28/2023 00:00:00" (clamp)
    [InlineData("new DateTime(2024, 1, 15).AddYears(1).ToString()")]  // "01/15/2025 00:00:00"
    // Calendar component properties (int)
    [InlineData("new DateTime(2024, 2, 29).Year")]                    // 2024
    [InlineData("new DateTime(2024, 3, 15).Month")]                   // 3
    [InlineData("new DateTime(2024, 3, 15).Day")]                     // 15
    [InlineData("new DateTime(2024, 1, 5, 9, 3, 7).Hour")]            // 9
    [InlineData("new DateTime(2024, 3, 1).DayOfYear")]                // 61 (leap: 31 + 29 + 1)
    // Subtraction -> TimeSpan
    [InlineData("(new DateTime(2024, 1, 20) - new DateTime(2024, 1, 15)).ToString()")] // "5.00:00:00"
    // Static helpers
    [InlineData("DateTime.DaysInMonth(2024, 2)")]                     // 29
    [InlineData("DateTime.IsLeapYear(2023)")]                         // false
    // Comparison (bool)
    [InlineData("new DateTime(2024, 1, 15) < new DateTime(2024, 1, 16)")]   // true
    [InlineData("new DateTime(2024, 1, 15) == new DateTime(2024, 1, 15)")]  // true
    [InlineData("new DateTime(2024, 1, 16) > new DateTime(2024, 1, 15)")]   // true
    public void DateTime_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
