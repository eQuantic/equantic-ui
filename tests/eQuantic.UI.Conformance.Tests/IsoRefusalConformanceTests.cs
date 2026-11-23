using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// An ISO date that does not exist, refused in .NET's words (#1616). .NET reads the ISO form, a year-first
/// date, a `T` and its clock, through to the end before it checks the date and the clock together, so a day
/// that does not exist is refused as a clock that does not exist is, in the name of the culture's calendar.
/// The browser checked the date before it saw the `T` and refused it as a text it could not read. A clock
/// malformed after the `T`, or a date that does not exist outside the ISO form, is the plain refusal on
/// both sides; so is a `T` after a date that is not the ISO form's, which the browser read as a date
/// (`2024-02T10:00`), and a `T` that starts a word names an unknown word after a date that exists.
/// </summary>
public class IsoRefusalConformanceTests
{
    private static readonly string[] Texts =
    [
        "2024-02-30T10:00", "2024-13-01T10:00", "2024-00-10T10:00", "0000-01-01T10:00", "2023-02-29T10:00:00.5",
        "2024-02-30T25:00", "2024-02-30T10:00Z", "2024-02-30 T10:00", "2024/02/30T10:00", "2024-2-30T10:00",
        "Monday 2024-02-30T10:00", "1445-02-30T10:00",
        // A clock malformed after the T, and dates that do not exist outside the ISO form: the plain refusal.
        "2024-02-30Tjunk", "2024-02-30T10:00 junk", "2024-02-30T10", "2024-02-30", "2024-02-30 10:00",
        // A T that starts a word: the word's refusal after a date that exists, the plain one after one that does not.
        "2024-01-15Tjunk", "2024-01-15Tx", "2024-01-15TT", "2024-02-30Tx", "1/15/2024Tx",
        // A T after a date that is not the ISO form's, three numbers with the year first, and an ISO clock left
        // unfinished: the plain refusal.
        "2024-02T10:00", "2024-13T10:00", "1/15/2024T10:00", "1/15/2024 T10:00", "Jan 15 2024T10:00",
        "2024-01-15T", "2024-01-15T1", "2024-01-15T10:00x",
        // And one that exists, which reads.
        "2024-02-29T10:00",
    ];

    private static string Refused() =>
        "var s = \"\"; foreach (var t in new[] { " + string.Join(", ", Texts.Select(t => "\"" + t + "\"")) + " }) "
        + "{ try { s += DateTime.Parse(t).ToString(\"o\") + \"|\"; } catch (FormatException e) { s += e.Message + \"|\"; } } return s;";

    [SkippableTheory]
    [InlineData(null)]
    [InlineData("en-US")]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void AnIsoDateThatDoesNotExist_IsRefusedInTheCalendarsWords(string? culture)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(Refused(), culture: culture);
    }

    /// <summary>A DateTimeOffset reads the ISO form as a DateTime does, and refuses it in the same words.</summary>
    [SkippableFact]
    public void AnOffsetReadsTheIsoFormAsADateTimeDoes()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            "try { return DateTimeOffset.Parse(\"2024-02-30T10:00+01:00\").ToString(\"o\"); } catch (FormatException e) { return e.Message; }");
    }
}
