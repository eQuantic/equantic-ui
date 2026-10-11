using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>TryParse(string, out T)</c> of a <c>DateTime</c>, a <c>DateTimeOffset</c>, a <c>DateOnly</c>, a
/// <c>TimeOnly</c> and a <c>TimeSpan</c>, run on both sides (#718). The first four called a
/// <c>tryParse</c> the runtime did not have, a TypeError on the page, and <c>DateOnly.TryParse</c>
/// answered the date or null where .NET answers true or false. Each answer prints whether the text was
/// read and what the out argument holds then: the value read, or the type's default for a text that
/// was not.
/// </summary>
public class DateTimeTryParseConformanceTests
{
    [SkippableTheory]
    // A DateTime: an ISO date, a date and time, the invariant culture's own form, and what is no date.
    [InlineData("var ok = DateTime.TryParse(\"2024-01-15\", out var d); return ok + \":\" + d.Ticks;")]
    [InlineData("var ok = DateTime.TryParse(\"2024-01-15T09:30:05.25\", out var d); return ok + \":\" + d.Ticks;")]
    [InlineData("var ok = DateTime.TryParse(\"01/15/2024 09:30:00\", out var d); return ok + \":\" + d.Ticks;")]
    [InlineData("var ok = DateTime.TryParse(\"2026-02-29\", out var d); return ok + \":\" + d.Ticks;")]
    [InlineData("var ok = DateTime.TryParse(\"2026-01-15T09:30:00 junk\", out var d); return ok + \":\" + d.Ticks;")]
    [InlineData("var ok = DateTime.TryParse(\"abc\", out var d); return ok + \":\" + d.Ticks;")]
    [InlineData("var ok = DateTime.TryParse(\"\", out var d); return ok + \":\" + d.Ticks;")]
    [InlineData("var ok = DateTime.TryParse(null, out var d); return ok + \":\" + d.Ticks;")]
    // A DateTimeOffset: its offset read with it, and what is no date.
    [InlineData("var ok = DateTimeOffset.TryParse(\"2024-01-15T09:30:00+02:00\", out var o); return ok + \":\" + o.Ticks + \":\" + o.Offset.Ticks;")]
    [InlineData("var ok = DateTimeOffset.TryParse(\"2024-01-15T09:30:00-05:30\", out var o); return ok + \":\" + o.Ticks + \":\" + o.Offset.Ticks;")]
    [InlineData("var ok = DateTimeOffset.TryParse(\"abc\", out var o); return ok + \":\" + o.Ticks + \":\" + o.Offset.Ticks;")]
    [InlineData("var ok = DateTimeOffset.TryParse(null, out var o); return ok + \":\" + o.Ticks + \":\" + o.Offset.Ticks;")]
    // A DateOnly and a TimeOnly.
    [InlineData("var ok = DateOnly.TryParse(\"2024-01-15\", out var d); return ok + \":\" + d.DayNumber;")]
    [InlineData("var ok = DateOnly.TryParse(\"2026-02-29\", out var d); return ok + \":\" + d.DayNumber;")]
    [InlineData("var ok = DateOnly.TryParse(\"abc\", out var d); return ok + \":\" + d.DayNumber;")]
    [InlineData("var ok = DateOnly.TryParse(null, out var d); return ok + \":\" + d.DayNumber;")]
    [InlineData("var ok = TimeOnly.TryParse(\"09:30\", out var t); return ok + \":\" + t.Ticks;")]
    [InlineData("var ok = TimeOnly.TryParse(\"09:30:15.5\", out var t); return ok + \":\" + t.Ticks;")]
    [InlineData("var ok = TimeOnly.TryParse(\"25:00\", out var t); return ok + \":\" + t.Ticks;")]
    [InlineData("var ok = TimeOnly.TryParse(\"abc\", out var t); return ok + \":\" + t.Ticks;")]
    [InlineData("var ok = TimeOnly.TryParse(null, out var t); return ok + \":\" + t.Ticks;")]
    // A TimeSpan.
    [InlineData("var ok = TimeSpan.TryParse(\"1.02:03:04.5\", out var s); return ok + \":\" + s.Ticks;")]
    [InlineData("var ok = TimeSpan.TryParse(\"-02:03:04\", out var s); return ok + \":\" + s.Ticks;")]
    [InlineData("var ok = TimeSpan.TryParse(\"abc\", out var s); return ok + \":\" + s.Ticks;")]
    [InlineData("var ok = TimeSpan.TryParse(null, out var s); return ok + \":\" + s.Ticks;")]
    public void TryParse_AnswersAsDotNetAnswers(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>The out argument is any place C# can write: a discard, a variable declared before, an
    /// element of an array, and one named out of its order, which still runs where it is written.</summary>
    [SkippableTheory]
    [InlineData("return DateTime.TryParse(\"2024-01-15\", out _).ToString();")]                                                             // True
    [InlineData("var d = new DateTime(2000, 1, 1); var ok = DateTime.TryParse(\"abc\", out d); return ok + \":\" + d.Ticks;")]             // False:0
    [InlineData("var a = new TimeSpan[2]; var ok = TimeSpan.TryParse(\"01:00:00\", out a[1]); return ok + \":\" + a[1].Ticks;")]           // True:36000000000
    [InlineData("var log = \"\"; string S() { log += \"s\"; return \"2024-01-15\"; } var ok = DateOnly.TryParse(result: out var d, s: S()); return log + ok + \":\" + d.DayNumber;")]
    [InlineData("var n = 0; if (TimeOnly.TryParse(\"09:30\", out var t)) n = t.Hour; return n.ToString();")]                              // 9
    public void TryParse_WritesItsOutArgumentAsDotNetWritesIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
