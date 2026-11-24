using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A date read wherever .NET's lexer reads it. The ISO form, a year-first date, a `T` and its clock, after
/// a designator, a clock, a zone or a day's name (#1841): its clock takes the place of the one before it, a
/// zone before it stands unless it writes its own, and a day's name beside it is not asked. A day's name
/// once, anywhere between the parts, asked of the date last (#1842). A number of three digits or more is a
/// year, and a date has one (#1843). A zone may lead the text, and a second zone or a second day's name is
/// the plain refusal.
/// </summary>
public class DateOrderConformanceTests
{
    private static readonly string[] IsoAfter =
    [
        "PM 2024-01-15T10:00", "AM 2024-01-15T10:00", "PM 2024-01-15T22:00", "10:00 2024-01-15T11:00",
        "PM 2024-02-30T10:00", "10:00 2024-02-30T10:00", "10:00 PM 2024-01-15T10:00", "PM 2024-01-15 T10:00",
        "PM  2024-01-15T10:00Z", "Z 2024-01-15T10:00", "Z 2024-01-15T10:00+01:00", "+01:00 2024-01-15T10:00",
        "GMT 2024-01-15T10:00", "10:00 Z 2024-01-15T11:00", "Monday PM 2024-01-15T10:00", "PM Monday 2024-01-15T10:00",
        "Tuesday 2024-01-15T10:00", "Tuesday PM 2024-01-15T10:00", "10:00 PM 2024-02-30T25:00", "PM 2024-01-15T10:00 junk",
        "10:00:00.5 2024-01-15T11:00", "PM 2024-01-15Tx", "10:00 1/15/2024T11:00", "1/15/2024 2024-01-16T10:00",
        "Z 2024-01-15", "Z 10:00 2024-01-15", "Z 2024-01-15 Z"
    ];

    private static readonly string[] DayNames =
    [
        "2024-01-15 10:00 Monday", "2024-01-15 10:00 Tuesday", "10:00 2024-01-15 Monday", "10:00 2024-01-15 Tuesday",
        "2024-01-15 10:00 PM Monday", "2024-01-15 10:00Z Monday", "2024-01-15 10:00 Mon", "Monday 10:00 2024-01-15",
        "10:00 Monday 2024-01-15", "2024-01-15 10:00 Monday Monday", "10:00 Monday Monday 2024-01-15", "Monday 2024-01-15 Monday",
        "2024-01-15 Monday 10:00", "2024-01-15 Monday 10:00 Tuesday", "Mon 10:00 2024-01-15", "2024-01-15 Tuesday junk",
        "Tuesday junk 2024-01-15", "2024-01-15 junk Tuesday",
        // A clock or a day that does not exist is refused before the day's name is asked.
        "2024-01-15 25:00 Tuesday", "Tuesday 2024-01-15 25:00", "2024-02-30 10:00 Tuesday", "25:00 Tuesday",
        // No date: the day's name is asked of today, which exactly one of these names.
        "10:00 Sunday", "10:00 Monday", "10:00 Tuesday", "10:00 Wednesday", "10:00 Thursday", "10:00 Friday",
        "10:00 Saturday"
    ];

    private static readonly string[] Years =
    [
        "2024-01-001", "2024-001-01", "1/001/2024", "001/15/2024",
        "2024-001", "001-2024", "001-01-15", "1/15/024",
        "0001-01-01", "01/015/2024", "2024-01-015", "15/001/2024"
    ];

    private static string Read(string[] texts) =>
        "var s = \"\"; foreach (var t in new[] { " + string.Join(", ", texts.Select(t => "\"" + t + "\"")) + " }) "
        + "{ try { s += DateTime.Parse(t).ToString(\"o\") + \"|\"; } catch (FormatException e) { s += e.Message + \"|\"; } } return s;";

    [SkippableTheory]
    [InlineData(null)]
    [InlineData("en-US")]
    [InlineData("th-TH")]
    [InlineData("ar-SA")]
    public void TheIsoForm_IsReadAfterWhateverCameFirst(string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Read(IsoAfter), culture: culture);
    }

    [SkippableTheory]
    [InlineData(null)]
    [InlineData("en-US")]
    [InlineData("th-TH")]
    public void ADaysName_StandsOnceAnywhere_AndIsAskedOfTheDateLast(string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Read(DayNames), culture: culture);
    }

    [SkippableTheory]
    [InlineData(null)]
    [InlineData("en-US")]
    [InlineData("pt-BR")]
    [InlineData("sv-SE")]
    public void ADateHasOneYear(string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Read(Years), culture: culture);
    }
}
