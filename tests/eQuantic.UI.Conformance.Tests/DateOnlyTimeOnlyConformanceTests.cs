using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for .NET <c>DateOnly</c> and <c>TimeOnly</c> (.NET 6+) — the compat types. Fixed values
/// compared via ToString()/property/comparison. Note the invariant short formats: DateOnly.ToString()
/// is "MM/dd/yyyy" and TimeOnly.ToString() is "HH:mm" (no seconds).
/// </summary>
public class DateOnlyTimeOnlyConformanceTests
{
    [SkippableTheory]
    // DateOnly — construction + invariant short date
    [InlineData("new DateOnly(2024, 1, 15).ToString()")]                 // "01/15/2024"
    [InlineData("new DateOnly(2024, 1, 15).AddDays(20).ToString()")]     // "02/04/2024"
    [InlineData("new DateOnly(2024, 1, 31).AddMonths(1).ToString()")]    // "02/29/2024" (clamp, leap)
    [InlineData("new DateOnly(2024, 6, 10).AddYears(1).ToString()")]     // "06/10/2025"
    [InlineData("new DateOnly(2024, 2, 29).Year")]                       // 2024
    [InlineData("new DateOnly(2024, 3, 15).Day")]                        // 15
    [InlineData("new DateOnly(2024, 3, 1).DayOfYear")]                   // 61
    [InlineData("new DateOnly(1, 1, 1).DayNumber")]                      // 0
    // DateOnly comparison
    [InlineData("new DateOnly(2024, 1, 15) < new DateOnly(2024, 1, 16)")]   // true
    [InlineData("new DateOnly(2024, 1, 15) == new DateOnly(2024, 1, 15)")]  // true
    public void DateOnly_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }

    [SkippableTheory]
    // TimeOnly — construction + invariant short time (HH:mm)
    [InlineData("new TimeOnly(13, 45).ToString()")]                      // "13:45"
    [InlineData("new TimeOnly(13, 45, 30).ToString()")]                  // "13:45" (short time omits seconds)
    [InlineData("new TimeOnly(9, 5).ToString()")]                        // "09:05"
    [InlineData("new TimeOnly(23, 0).AddHours(2).ToString()")]           // "01:00" (wraps past midnight)
    [InlineData("new TimeOnly(13, 45, 30).Hour")]                        // 13
    [InlineData("new TimeOnly(13, 45, 30).Minute")]                      // 45
    [InlineData("new TimeOnly(13, 45, 30).Second")]                      // 30
    // TimeOnly comparison
    [InlineData("new TimeOnly(14, 0) > new TimeOnly(13, 0)")]            // true
    [InlineData("new TimeOnly(13, 45) == new TimeOnly(13, 45)")]         // true
    public void TimeOnly_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
