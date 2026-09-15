using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using eQuantic.UI.Components;
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
        var previousFormat = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        var asked = new CultureInfo(culture);
        try
        {
            // BOTH halves of .NET's pair. The names and the pattern follow the FORMAT culture,
            // the hint's letters follow the UI culture through the resx, and pinning one while
            // the machine supplies the other is how this fixture would carry whatever language
            // the developer's laptop is in.
            CultureInfo.CurrentCulture = asked;
            CultureInfo.CurrentUICulture = asked;
            return new
            {
                firstDayOfWeek = CalendarNames.FirstDayOfWeek,
                dayNamesShort = CalendarNames.DayNamesShort,
                dayNamesLong = CalendarNames.DayNamesLong,
                monthNames = CalendarNames.MonthNames,
                monthNamesShort = CalendarNames.MonthNamesShort,
                // Not a NAME, but the same contract: the twin derives the field's hint from the
                // pattern and the letters, and it has to reach the same string this side does.
                shortDatePattern = CalendarNames.ShortDatePattern,
                dateFormatLetters = SdkStrings.DateFormatLetters,
                dateFormatHint = SdkStrings.DateFormatHint,
            };
        }
        finally
        {
            CultureInfo.CurrentCulture = previousFormat;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    /// <summary>
    /// The half that is OURS: which table of the culture each member reads. Derived from the
    /// host's own <see cref="DateTimeFormatInfo"/>, so it asserts the same thing on every runner.
    /// <para>
    /// It used to compare a committed snapshot of ten cultures byte for byte, which pinned the
    /// MACHINE's ICU rather than this repository's code: macOS abbreviates <c>ar-EG</c> Sunday as
    /// "الأحد" and the Linux and Windows runners as "أحد", all correct Arabic, and whichever host
    /// wrote the file the other two disagreed with it. Nothing about that is ours to fix — a
    /// calendar's spelling belongs to the platform's globalization, the way <c>ToString("d")</c>
    /// does — and a pin that fails on two of three hosts is measuring the host.
    /// </para>
    /// <para>
    /// What IS ours is the mapping, and it has already been wrong once: <c>DayNamesShort</c> must
    /// read <c>AbbreviatedDayNames</c> and never <c>ShortestDayNames</c>, which is the finding
    /// that shaped <see cref="CalendarNames"/>. That swap is what this catches, on any ICU.
    /// </para>
    /// </summary>
    [Fact]
    public void TheCalendarReadsTheCulturesOwnTables()
    {
        foreach (var name in Cultures)
        {
            var culture = new CultureInfo(name);
            var format = culture.DateTimeFormat;
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = culture;

                CalendarNames.DayNamesShort.Should().Equal(format.AbbreviatedDayNames,
                    "{0}'s abbreviations are AbbreviatedDayNames — ShortestDayNames is different "
                    + "data the twin's Intl has no equivalent for", name);
                CalendarNames.DayNamesLong.Should().Equal(format.DayNames, "{0}", name);
                CalendarNames.FirstDayOfWeek.Should().Be((int)format.FirstDayOfWeek, "{0}", name);
                CalendarNames.ShortDatePattern.Should().Be(format.ShortDatePattern, "{0}", name);

                // The thirteenth month is .NET's lunisolar slot, empty under every Gregorian
                // culture — and a nameless month in a year grid if it were handed out.
                CalendarNames.MonthNames.Should().Equal(format.MonthNames.Take(12), "{0}", name);
                CalendarNames.MonthNamesShort.Should()
                    .Equal(format.AbbreviatedMonthNames.Take(12), "{0}", name);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }

    /// <summary>
    /// The committed fixture is a SAMPLE for <c>calendar-names.spec.ts</c>, not a pin: the twin
    /// installs it as a server catalog and asserts that it reads it back verbatim, which is a
    /// proof about plumbing that any fixed data serves. It is generated once, reviewed in the
    /// diff, and never compared to the host again — see
    /// <see cref="TheCalendarReadsTheCulturesOwnTables"/> for what the host IS asked.
    /// <para>Regenerate with <c>EQ_UPDATE_CALENDAR_FIXTURE=1</c>.</para>
    /// </summary>
    [Fact]
    public void TheTwinsSample_ExistsAndCoversTheProbedCultures()
    {
        var path = FixturePath();
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_CALENDAR_FIXTURE") == "1")
        {
            var pinned = Cultures.ToDictionary(culture => culture, Snapshot);
            File.WriteAllText(path, JsonSerializer.Serialize(pinned, new JsonSerializerOptions
            {
                WriteIndented = true,
                // LF: WriteIndented indents with Environment.NewLine, and this file is committed.
                NewLine = "\n",
                // The names are the point: escaping every accent and every CJK glyph would make
                // the fixture unreadable and its diffs meaningless.
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }) + "\n");
            return;
        }

        File.Exists(path).Should().BeTrue(
            "the twin reads this sample — generate it once with EQ_UPDATE_CALENDAR_FIXTURE=1");

        // The SHAPE is still ours: a culture added to the probe list and not to the sample would
        // leave the twin asserting against a file that never learned about it.
        using var sample = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var culture in Cultures)
            sample.RootElement.TryGetProperty(culture, out _).Should().BeTrue(
                "{0} is probed here but missing from the twin's sample — regenerate with "
                + "EQ_UPDATE_CALENDAR_FIXTURE=1", culture);
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
