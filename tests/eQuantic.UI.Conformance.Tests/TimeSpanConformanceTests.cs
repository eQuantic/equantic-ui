using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for .NET <c>TimeSpan</c> — the tick-precise compat type, formatted in the .NET "c"
/// constant format (`[-][d.]hh:mm:ss[.fffffff]`). Proves the From* factories, the multi-arg
/// constructors, component/total properties, arithmetic and comparison match .NET.
/// </summary>
public class TimeSpanConformanceTests
{
    [SkippableTheory]
    // From* factories + "c" formatting
    [InlineData("TimeSpan.FromHours(25).ToString()")]                 // "1.01:00:00"
    [InlineData("TimeSpan.FromDays(1.5).ToString()")]                 // "1.12:00:00"
    [InlineData("TimeSpan.FromSeconds(90).ToString()")]               // "00:01:30"
    [InlineData("TimeSpan.FromMinutes(5).ToString()")]                // "00:05:00"
    // Constructors (arity-dispatched)
    [InlineData("new TimeSpan(1, 2, 3).ToString()")]                  // "01:02:03"
    [InlineData("new TimeSpan(2, 3, 4, 5).ToString()")]               // "2.03:04:05"
    // Component (whole) and total (fractional) properties
    [InlineData("TimeSpan.FromHours(25).Days")]                       // 1
    [InlineData("TimeSpan.FromHours(25).Hours")]                      // 1
    [InlineData("TimeSpan.FromMinutes(90).TotalHours")]               // 1.5
    [InlineData("TimeSpan.FromHours(2).TotalMinutes")]                // 120
    // Arithmetic
    [InlineData("(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(30)).ToString()")] // "01:30:00"
    [InlineData("(TimeSpan.FromHours(2) - TimeSpan.FromMinutes(30)).ToString()")] // "01:30:00"
    // Comparison (bool)
    [InlineData("TimeSpan.FromHours(1) > TimeSpan.FromMinutes(30)")]  // true
    [InlineData("TimeSpan.FromHours(1) == TimeSpan.FromMinutes(60)")] // true
    public void TimeSpan_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
