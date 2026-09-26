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

    /// <summary>
    /// A <c>DateTimeOffset</c> adds as its clock time's <c>DateTime</c> does, then checks its UTC time:
    /// a clock time outside the calendar is refused in <c>DateTime</c>'s words before the UTC time is
    /// looked at (#422).
    /// </summary>
    [SkippableTheory]
    [InlineData("var o = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(3)); return (o.AddSeconds(0.00001).Ticks - o.Ticks).ToString();")]                                   // "100"
    [InlineData("var o = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(3)); return (o.AddMilliseconds(-0.5).Ticks - o.Ticks).ToString();")]                                 // "-5000": a method the twin lacked
    [InlineData("var o = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(3)); return (o.AddMicroseconds(1.99).Ticks - o.Ticks).ToString();")]                                 // "19": and another
    [InlineData("var o = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(3)); return (o.AddDays(1.23456789).Ticks - o.Ticks).ToString();")]                                   // "1066666656959"
    [InlineData("try { new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(3)).AddDays(1e10); return \"no\"; } catch (Exception e) { return e.Message; }")]                       // "Value to add was out of range. (Parameter 'value')"
    [InlineData("try { DateTimeOffset.MaxValue.AddSeconds(1); return \"no\"; } catch (Exception e) { return e.Message; }")]                                                              // "The added or subtracted value results in an un-representable DateTime. (Parameter 'value')": the clock time first
    [InlineData("try { DateTimeOffset.MinValue.AddDays(-1); return \"no\"; } catch (Exception e) { return e.Message; }")]                                                                // "The added or subtracted value results in an un-representable DateTime. (Parameter 'value')"
    [InlineData("var o = new DateTimeOffset(9999, 12, 31, 19, 59, 59, TimeSpan.FromHours(-3)); return o.AddMinutes(30).Ticks.ToString();")]                                              // "3155378849990000000"
    [InlineData("var o = new DateTimeOffset(9999, 12, 31, 19, 59, 59, TimeSpan.FromHours(-3)); try { o.AddMinutes(90); return \"no\"; } catch (Exception e) { return e.Message; }")]     // "The UTC time represented when the offset is applied must be between year 0 and 10,000. (Parameter 'offset')": then the UTC time
    [InlineData("var o = new DateTimeOffset(1, 1, 1, 4, 0, 0, TimeSpan.FromHours(3)); try { o.AddMinutes(-90); return \"no\"; } catch (Exception e) { return e.Message; }")]             // "The UTC time represented when the offset is applied must be between year 0 and 10,000. (Parameter 'offset')"
    [InlineData("var o = new DateTimeOffset(1, 1, 1, 4, 0, 0, TimeSpan.FromHours(3)); try { o.AddTicks(-3 * 36000000000L); return \"no\"; } catch (Exception e) { return e.Message; }")] // "The UTC time represented when the offset is applied must be between year 0 and 10,000. (Parameter 'offset')"
    public void DateTimeOffsetAdd_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
