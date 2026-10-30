using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A date read from text in the forms .NET reads beyond the numeric ones (#807): a month's name, a day's
/// name, a date of two numbers, a clock alone, <c>GMT</c>, numbers separated by white space, and a long
/// fraction. Each form runs in four cultures, because which number is the day, the month or the year comes
/// from the culture's patterns, and the year of a date that names none is compared as this year.
/// </summary>
public class DateReadingConformanceTests
{
    private static readonly string[] Cultures = ["en-US", "de-DE", "pt-BR", "ja-JP"];

    private static readonly string[] DateForms =
    [
        // A month's name, whole or abbreviated, in any case, with a day and a year in any order.
        "Jan 15, 2024", "January 15, 2024", "15 Jan 2024", "15 January 2024", "2024 Jan 15", "jan 15, 2024", "JAN 15, 2024",
        "May 3 2024", "3 May, 2024", "Jan. 15, 2024", "Sep 5, 2024",
        // With a clock after it, and a day's name before it.
        "Jan 15, 2024 9:30 AM", "15 Jan 2024 09:30:15", "Monday, January 15, 2024", "Mon, 15 Jan 2024", "Monday, 15 January 2024 09:30:00",
        // A month and one number, which each culture's month-day and year-month patterns read.
        "Jan 2024", "January 2024", "2024 Jan", "Jan 15", "15 Jan", "Jan 5", "5 Jan", "Mar 32",
        // Two numbers of a month name's date.
        "Jan 15 24", "15 Jan 24", "24 Jan 15", "Feb 30 24", "Feb 24 30",
        // A word that is no month's or day's name.
        "Sept 5, 2024", "Janu 5, 2024", "Tuesday, January 15, 2024", "January", "Monday",
        // A date of two numbers.
        "1/15", "01/15", "15/1", "1/2", "1-2", "1.2", "1 2", "13/13", "1/2024", "2024/1",
        // Three numbers separated by white space, and a point after the year.
        "01 15 2024", "2024 01 15", "15 01 2024", "1/15/2024.", "15.01.2024.",
        // A zone named GMT.
        "2024-01-15 GMT", "Mon, 15 Jan 2024 09:30:00 GMT", "2024-01-15 09:30 GMT+01:00",
    ];

    private static readonly string[] ClockForms = ["9:30", "9:30 PM", "09:30:15", "21:00", "PM", "9:30 Z"];

    public static TheoryData<string, string> DateCases() => Each(DateForms);

    public static TheoryData<string, string> ClockCases() => Each(ClockForms);

    private static TheoryData<string, string> Each(string[] forms)
    {
        var data = new TheoryData<string, string>();
        foreach (var culture in Cultures)
            foreach (var form in forms)
                data.Add(form, culture);
        return data;
    }

    [SkippableTheory]
    [MemberData(nameof(DateCases))]
    public void ADate_IsReadAsDotNetReadsIt(string text, string culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            $"var ok = DateTime.TryParse(\"{text}\", out var d); var thisYear = DateTime.Now.Year; "
            + "var day = DateOnly.TryParse(\"" + text + "\", out var o); var off = DateTimeOffset.TryParse(\"" + text + "\", out var f); "
            + "return ok + \"|\" + (ok ? (d.Year == thisYear ? \"now\" : d.Year.ToString()) + \"-\" + d.Month + \"-\" + d.Day + \" \" + d.Hour + \":\" + d.Minute + \":\" + d.Second + \" \" + d.Kind : \"\") "
            + "+ \"|\" + day + \"|\" + (day ? (o.Year == thisYear ? \"now\" : o.Year.ToString()) + \"-\" + o.Month + \"-\" + o.Day : \"\") "
            + "+ \"|\" + off + \"|\" + (off ? f.Month + \"-\" + f.Day + \" \" + f.Hour : \"\");",
            culture: culture);
    }

    /// <summary>A clock with no date is today's.</summary>
    [SkippableTheory]
    [MemberData(nameof(ClockCases))]
    public void AClockAlone_IsToday(string text, string culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            $"var ok = DateTime.TryParse(\"{text}\", out var d); return ok + \"|\" + (ok ? (d.Date == DateTime.Today) + \" \" + d.Hour + \":\" + d.Minute + \":\" + d.Second : \"\");",
            culture: culture);
    }

    /// <summary>A culture that writes its clock with points reads a colon too, and a clock that mixes them.</summary>
    [SkippableTheory]
    [InlineData("9.30", "fi-FI")]
    [InlineData("9:30", "fi-FI")]
    [InlineData("9.30.15", "fi-FI")]
    [InlineData("9:30.15", "fi-FI")]
    [InlineData("9.30:15", "fi-FI")]
    [InlineData("15.1.2024 9.30", "fi-FI")]
    [InlineData("15.1.2024 9:30.15", "fi-FI")]
    [InlineData("9.30", "da-DK")]
    public void AClockOfACultureThatWritesPoints_IsReadAsDotNetReadsIt(string text, string culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            $"var ok = DateTime.TryParse(\"{text}\", out var d); return ok + \"|\" + (ok ? d.Hour + \":\" + d.Minute + \":\" + d.Second : \"\");",
            culture: culture);
    }

    [SkippableTheory]
    // A fraction of eight digits or more is rounded to the tick.
    [InlineData("2024-01-15T09:30:00.12345678")]
    [InlineData("1/15/2024 9:30:00.123456789")]
    [InlineData("1/15/2024 9:30:00.99999995")]
    public void ALongFraction_RoundsToTheTick(string text)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet($"return DateTime.TryParse(\"{text}\", out var d) + \"|\" + d.Ticks.ToString();");
    }

    /// <summary>
    /// <c>new TimeOnly</c> keeps its microsecond, and refuses each part out of its range in .NET's words:
    /// it read four numbers and checked none (#952).
    /// </summary>
    [SkippableTheory]
    [InlineData("return new TimeOnly(1, 2, 3, 4, 5).Ticks.ToString();")]
    [InlineData("var t = new TimeOnly(1, 2, 3, 4, 5); return t.Microsecond + \"|\" + t.Millisecond + \"|\" + t.Hour;")]
    [InlineData("return new TimeOnly(1, 2).Ticks.ToString() + \"|\" + new TimeOnly(1, 2, 3).Ticks + \"|\" + new TimeOnly(1, 2, 3, 4).Ticks;")]
    [InlineData("return new TimeOnly(23, 59, 59, 999, 999).Ticks.ToString() + \"|\" + new TimeOnly(0L).Ticks + \"|\" + new TimeOnly(863999999999L).Ticks;")]
    [InlineData("try { return new TimeOnly(25, 0).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("try { return new TimeOnly(-1, 0).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("try { return new TimeOnly(0, 60).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("try { return new TimeOnly(0, 0, 60).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("try { return new TimeOnly(0, 0, 0, 1000).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("try { return new TimeOnly(0, 0, 0, 0, 1000).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("try { return new TimeOnly(0, 0, 0, 0, -1).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("try { return new TimeOnly(-1L).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("try { return new TimeOnly(864000000000L).ToString(); } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    public void ATimeOnly_IsBuiltAndCheckedAsDotNetDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
