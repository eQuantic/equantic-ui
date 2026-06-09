using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for .NET <c>DateTimeOffset</c> — the compat type (wall-clock DateTime + offset,
/// compared by the instant). Invariant ToString is "MM/dd/yyyy HH:mm:ss zzz".
/// </summary>
public class DateTimeOffsetConformanceTests
{
    [SkippableTheory]
    // Construction + invariant ToString (with offset)
    [InlineData("new DateTimeOffset(2024, 1, 15, 13, 30, 0, TimeSpan.FromHours(-3)).ToString()")]   // "01/15/2024 13:30:00 -03:00"
    // Local components + offset
    [InlineData("new DateTimeOffset(2024, 1, 15, 13, 30, 0, TimeSpan.FromHours(-3)).Year")]          // 2024
    [InlineData("new DateTimeOffset(2024, 1, 15, 13, 30, 0, TimeSpan.FromHours(-3)).Hour")]          // 13
    [InlineData("new DateTimeOffset(2024, 1, 15, 13, 30, 0, TimeSpan.FromHours(-3)).Offset.ToString()")] // "-03:00:00"
    // UtcDateTime = local − offset
    [InlineData("new DateTimeOffset(2024, 1, 15, 13, 30, 0, TimeSpan.FromHours(-3)).UtcDateTime.ToString()")] // "01/15/2024 16:30:00"
    // ToOffset re-expresses the same instant
    [InlineData("new DateTimeOffset(2024, 1, 15, 13, 30, 0, TimeSpan.FromHours(-3)).ToOffset(TimeSpan.Zero).ToString()")] // "01/15/2024 16:30:00 +00:00"
    // Add keeps offset
    [InlineData("new DateTimeOffset(2024, 1, 15, 13, 30, 0, TimeSpan.FromHours(-3)).AddHours(2).Hour")] // 15
    // Unix time round-trip
    [InlineData("DateTimeOffset.FromUnixTimeSeconds(0).UtcDateTime.ToString()")]                     // "01/01/1970 00:00:00"
    [InlineData("new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()")]        // 0
    // Subtraction (instant) -> TimeSpan
    [InlineData("(new DateTimeOffset(2024, 1, 15, 15, 0, 0, TimeSpan.Zero) - new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero)).ToString()")] // "03:00:00"
    // Comparison/equality by instant (different wall-clock, same moment)
    [InlineData("new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero) == new DateTimeOffset(2024, 1, 15, 13, 0, 0, TimeSpan.FromHours(1))")] // true
    [InlineData("new DateTimeOffset(2024, 1, 15, 13, 0, 0, TimeSpan.Zero) > new DateTimeOffset(2024, 1, 15, 13, 0, 0, TimeSpan.FromHours(1))")]   // true (later instant)
    public void DateTimeOffset_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
