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

    /// <summary>
    /// <c>TimeOnly.AddHours</c> and <c>AddMinutes</c> take ONE product, converted as .NET 9 and later
    /// convert a double (toward zero, NaN to zero, saturated at the ends of a long), wrapped by the day
    /// (#422).
    /// </summary>
    [SkippableTheory]
    [InlineData("var t = new TimeOnly(10, 0); return (t.AddHours(0.0000001).Ticks - t.Ticks).ToString();")]     // "3600", where rounding to the millisecond gave 0
    [InlineData("var t = new TimeOnly(10, 0); return t.AddMinutes(0.5).Ticks.ToString();")]                     // "360300000000"
    [InlineData("var t = new TimeOnly(10, 0); return t.AddMinutes(-0.0000001).Ticks.ToString();")]              // "359999999940": wraps backwards
    [InlineData("var t = new TimeOnly(10, 0); return t.AddHours(1.23456789).Ticks.ToString();")]                // "404444444039": one product, not split
    [InlineData("var t = new TimeOnly(10, 0); return t.AddHours(-10.5).ToString();")]                           // "23:30"
    [InlineData("var t = new TimeOnly(10, 0); return t.AddHours(1e20).Ticks.ToString();")]                      // "460854775807": the product saturates, then wraps
    [InlineData("var t = new TimeOnly(10, 0); return t.AddHours(-1e20).Ticks.ToString();")]                     // "259145224192"
    [InlineData("var t = new TimeOnly(10, 0); return t.AddMinutes(double.NegativeInfinity).Ticks.ToString();")] // "259145224192"
    [InlineData("var t = new TimeOnly(10, 0); return t.AddHours(double.NaN).Ticks.ToString();")]                // "360000000000": NaN adds nothing
    public void TimeOnlyAdd_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
