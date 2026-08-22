using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// What a calendar SAYS, pinned per culture for the TypeScript twin to check against.
/// <para>
/// The two sides read different data — <see cref="CultureInfo"/> here, <c>Intl</c> there — so the
/// only thing that keeps a month grid from being labelled one way on the server and another after
/// hydration is a fixture generated from one and asserted by the other. The ten cultures are the
/// ones the surface was probed against, chosen to cover the three first-day answers (Sunday,
/// Monday, Saturday) and the scripts where an abbreviation is not three Latin letters.
/// </para>
/// </summary>
public class CalendarNamesFixtureTests
{
    private static readonly string[] Cultures =
        ["en-US", "pt-BR", "es-ES", "fr-FR", "de-DE", "ja-JP", "ar-EG", "en-GB", "ru-RU", "zh-CN"];

    /// <summary>Derived from THIS file's location, not from the build output — the repository's
    /// own convention (StyleAtomizerTests, FormatSubsetTests), and deterministic where a walk up
    /// from <c>AppContext.BaseDirectory</c> depends on where the runner put the binaries.</summary>
    private static string FixturePath([CallerFilePath] string sourcePath = "")
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
        return Path.Combine(repoRoot, "src", "eQuantic.UI.Runtime", "src", "shared",
            "calendar-names.fixture.json");
    }

    /// <summary>Reads the surface under a given culture — the same call an SSR render makes.</summary>
    private static object Snapshot(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            return new
            {
                firstDayOfWeek = CalendarNames.FirstDayOfWeek,
                dayNamesShort = CalendarNames.DayNamesShort,
                dayNamesLong = CalendarNames.DayNamesLong,
                monthNames = CalendarNames.MonthNames,
                monthNamesShort = CalendarNames.MonthNamesShort,
            };
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void WhatTheCalendarSays_IsPinnedForTheTwin()
    {
        var pinned = Cultures.ToDictionary(culture => culture, Snapshot);
        var json = JsonSerializer.Serialize(pinned, new JsonSerializerOptions
        {
            WriteIndented = true,
            // The names are the point: escaping every accent and every CJK glyph would make the
            // fixture unreadable and its diffs meaningless.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }) + "\n";

        var path = FixturePath();
        // A pin that rewrites itself is not a pin: it would pass in CI while the twin quietly read
        // the freshly-written file from the same workspace. Regeneration is deliberate and named,
        // the way every other fixture in this repository does it.
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_CALENDAR_FIXTURE") == "1")
        {
            File.WriteAllText(path, json);
            return;
        }

        File.Exists(path).Should().BeTrue(
            "the twin asserts against this fixture — generate it once with EQ_UPDATE_CALENDAR_FIXTURE=1");
        File.ReadAllText(path).Should().Be(json,
            "the calendar names changed; regenerate with EQ_UPDATE_CALENDAR_FIXTURE=1 and review the diff");
    }

    [Fact]
    public void TheThirteenthMonth_NeverReachesAPicker()
    {
        // .NET's month arrays carry a thirteenth entry for lunisolar calendars — empty under every
        // Gregorian culture, and a nameless month in a year grid if it were handed out.
        foreach (var culture in Cultures)
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);
                CalendarNames.MonthNames.Should().HaveCount(12).And.NotContain(string.Empty);
                CalendarNames.MonthNamesShort.Should().HaveCount(12).And.NotContain(string.Empty);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
