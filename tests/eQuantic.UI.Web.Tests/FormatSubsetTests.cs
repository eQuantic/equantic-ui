using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The D7 formatting subset, cross-pinned. This side GENERATES the fixture from real .NET — every
/// expected string below is what <c>value.ToString(spec, culture)</c> actually produces — and
/// <c>format-subset.spec.ts</c> asserts the SAME file through the transpiled runtime's formatter.
/// <para>
/// That direction matters: .NET is the truth a developer writing C# expects, and the browser is
/// the side that has to agree. When a case cannot agree, the honest answer is to drop it from the
/// subset and have EQ2100 refuse it at build time — never to approximate it at runtime.
/// </para>
/// <para>
/// Two normalizations, both about ICU rather than about us: the space inside a formatted currency
/// or percent is a NON-BREAKING one whose exact codepoint moved between ICU versions (U+00A0 vs
/// U+202F), and both runtimes may carry different ICU builds — so both dumpers fold those to a
/// plain space. The values compared are otherwise byte-for-byte.
/// </para>
/// Regenerate with <c>EQ_UPDATE_FORMAT_FIXTURE=1</c>.
/// </summary>
public class FormatSubsetTests
{
    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string FixturePath => Path.Combine(RepoRoot(),
        "src", "eQuantic.UI.Runtime", "src", "shared", "__fixtures__", "format-subset.txt");

    /// <summary>The cultures the fixture covers: a comma-decimal one, a dot-decimal one, and one
    /// whose group separator is the other's decimal point — the three ways a number can go wrong —
    /// then one whose data leaves a four-digit number ungrouped where .NET groups it (es-ES writes
    /// 1.234, #445), and one whose minus sign is not a hyphen (sv-SE writes U+2212).</summary>
    private static readonly string[] Cultures = ["en-US", "pt-BR", "de-DE", "es-ES", "sv-SE"];

    /// <summary>The values, the infinities and NaN among them: the culture's symbols, whatever the
    /// specifier.</summary>
    private static readonly double[] Numbers =
        [1234.5, -1234.5, 0, 0.125, 1000000, double.PositiveInfinity, double.NegativeInfinity, double.NaN];

    // The empty spec is the one everybody writes — `{0}` — and it is NOT invariant in .NET: it
    // calls ToString(IFormatProvider), so 1234.5 is "1234,5" in pt-BR. Pinned like the rest. `E2` and
    // the two pictures are drawn in the culture's symbols too (#445).
    private static readonly string[] NumberSpecs =
        ["", "N0", "N2", "F2", "F0", "P1", "P0", "C2", "C0", "E2", "#,##0.00", "0.0%"];

    /// <summary>`D` and `X` are INTEGER specifiers in .NET — <c>(1234.5).ToString("D5")</c> throws,
    /// and a subset that pretended otherwise would be promising something the server cannot do.</summary>
    private static readonly long[] Integers = [42, -42, 1000000];

    // No `X`: hex of a NEGATIVE value is two's complement at the C# type's width
    // (`(-42).ToString("X4")` is 8 digits as an int, 16 as a long). The compiler passes that width
    // with every integer it can type (#445), but an argument typed as an object still reaches the
    // browser as one untyped number, so EQ2100 refuses it in a template until the subset is widened
    // where the width is known (#456).
    private static readonly string[] IntegerSpecs = ["", "D5", "D", "N0"];

    /// <summary>One fixed instant, spelled as LOCAL parts on both sides — the fixture must not
    /// depend on the machine's time zone, only on its culture data.</summary>
    private static readonly DateTime Moment = new(2026, 8, 13, 15, 45, 7);

    private static readonly string[] DateSpecs = ["d", "D", "t", "T", "g", "G", "M", "Y", "yyyy-MM-dd"];

    /// <summary>
    /// The specifiers an INVARIANT conversion is written for: a number a machine reads — a CSS
    /// length, a key, a value on the wire — which has to keep its shape whoever is reading the
    /// page. Currency and percent are absent on purpose: .NET's invariant currency symbol is the
    /// generic sign, and Intl has no notion of a currency without a country, so an invariant `C2`
    /// is a promise neither side can keep.
    /// </summary>
    private static readonly string[] InvariantSpecs = ["", "N0", "N2", "F2", "F0", "0.##", "0.0"];

    /// <summary>ICU's non-breaking spaces, folded — see the type's remarks.</summary>
    internal static string Normalize(string value) =>
        value.Replace(' ', ' ').Replace(' ', ' ');

    /// <summary>The subset as this host's .NET spells it — the sample's generator, and the
    /// subject of the invariant check below.</summary>
    private static string Dump()
    {
        var builder = new StringBuilder();
        foreach (var name in Cultures)
        {
            var culture = CultureInfo.GetCultureInfo(name);
            // The currency CODE the client needs (Intl takes no symbol) — the same value the build
            // writes into each culture catalog, from the same .NET source.
            var currency = new RegionInfo(culture.Name).ISOCurrencySymbol;
            // The culture's own patterns travel with it, exactly as the build writes them into
            // each catalog: .NET's `d` IS ShortDatePattern, and Intl's short-date preset is a
            // different editorial choice (a two-digit year in en-US, which .NET never prints).
            var dtf = culture.DateTimeFormat;
            builder.Append("culture ").Append(name).Append(' ').Append(currency)
                .Append('|').Append(dtf.ShortDatePattern)
                .Append('|').Append(dtf.LongDatePattern)
                .Append('|').Append(dtf.ShortTimePattern)
                .Append('|').Append(dtf.LongTimePattern)
                .Append('|').Append(dtf.MonthDayPattern)
                .Append('|').Append(dtf.YearMonthPattern)
                .Append('\n');

            foreach (var value in Numbers)
                foreach (var spec in NumberSpecs)
                    builder.Append("num|").Append(name).Append('|')
                        .Append(value.ToString(CultureInfo.InvariantCulture)).Append('|')
                        .Append(spec).Append('|')
                        .Append(Normalize(spec.Length == 0
                            ? value.ToString(culture)
                            : value.ToString(spec, culture))).Append('\n');

            foreach (var value in Integers)
                foreach (var spec in IntegerSpecs)
                    builder.Append("int|").Append(name).Append('|')
                        .Append(value.ToString(CultureInfo.InvariantCulture)).Append('|')
                        .Append(spec).Append('|')
                        .Append(Normalize(spec.Length == 0
                            ? value.ToString(culture)
                            : value.ToString(spec, culture))).Append('\n');

            foreach (var spec in DateSpecs)
                builder.Append("date|").Append(name).Append('|')
                    .Append(Moment.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)).Append('|')
                    .Append(spec).Append('|')
                    .Append(Normalize(Moment.ToString(spec, culture))).Append('\n');
        }

        // The INVARIANT section: no culture installed against it, because that is the point — the
        // client replays these with pt-BR active and must produce these strings anyway.
        foreach (var value in Numbers)
            foreach (var spec in InvariantSpecs)
                builder.Append("inv|")
                    .Append(value.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(spec).Append('|')
                    .Append(Normalize(spec.Length == 0
                        ? value.ToString(CultureInfo.InvariantCulture)
                        : value.ToString(spec, CultureInfo.InvariantCulture))).Append('\n');

        return builder.ToString();
    }

    /// <summary>
    /// The committed fixture is a SAMPLE for <c>format-subset.spec.ts</c>, not a pin on this
    /// host's globalization. It is generated once, reviewed in the diff, and never compared to
    /// the host again.
    /// <para>
    /// It used to be compared byte for byte, and that measured the MACHINE: <c>en-US</c>'s long
    /// time pattern is <c>h:mm:ss tt</c> on macOS and <c>h:mm tt</c> on the Windows runner, both
    /// correct for the ICU they carry. Whichever host wrote the file, the other two disagreed —
    /// and none of that is this repository's code. What is still ours is the SUBSET: a culture or
    /// a specifier added here and not regenerated would leave the twin asserting against a file
    /// that never learned about it.
    /// </para>
    /// <para>Regenerate with <c>EQ_UPDATE_FORMAT_FIXTURE=1</c>.</para>
    /// </summary>
    [Fact]
    public void TheTwinsSample_ExistsAndCoversTheDeclaredSubset()
    {
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_FORMAT_FIXTURE") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FixturePath)!);
            File.WriteAllText(FixturePath, Dump());
            return;
        }

        File.Exists(FixturePath).Should().BeTrue(
            "the twin reads this sample — generate it once with EQ_UPDATE_FORMAT_FIXTURE=1");

        // Drop the host's data and NOTHING else.
        //
        // The `culture` row's `|`-separated fields are the culture's own PATTERNS, which is the
        // data that differs per host — `en-US`'s long time pattern is `h:mm:ss tt` on macOS and
        // `h:mm tt` on the Windows runner. Its HEAD is not: the culture's name, and the ISO
        // currency code the client is sent, which every ICU spells the same. So the head stays and
        // only the patterns go.
        //
        // The `inv` rows stay WHOLE. They are formatted against the invariant culture, which is
        // .NET's own and not the machine's, and they are the half of the subset this pin can still
        // compare by value — dropping them would have left the invariant contract to the vitest
        // side alone. Every other row ends in a culture-formatted value, so its last field goes.
        static IEnumerable<string> Keys(string dump) => dump
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line switch
            {
                _ when line.StartsWith("culture ", StringComparison.Ordinal) =>
                    line.Split('|')[0],
                _ when line.StartsWith("inv|", StringComparison.Ordinal) =>
                    line,
                _ => string.Join('|', line.Split('|').SkipLast(1)),
            });

        Keys(File.ReadAllText(FixturePath)).Should().BeEquivalentTo(Keys(Dump()),
            "the subset changed — regenerate with EQ_UPDATE_FORMAT_FIXTURE=1, or drop the case "
            + "(and have EQ2100 refuse it) rather than let the two sides drift");
    }

    /// <summary>
    /// The one promise in the subset that is OURS rather than the platform's: an invariant
    /// conversion must not follow the active culture. The client replays those rows with pt-BR
    /// installed and has to produce the same strings, so the server must too — a number a machine
    /// reads keeps its shape whoever is reading the page.
    /// </summary>
    [Fact]
    public void TheInvariantSection_DoesNotFollowTheActiveCulture()
    {
        static string[] InvariantRows()
        {
            var rows = new List<string>();
            foreach (var value in Numbers)
                foreach (var spec in InvariantSpecs)
                    rows.Add(Normalize(spec.Length == 0
                        ? value.ToString(CultureInfo.InvariantCulture)
                        : value.ToString(spec, CultureInfo.InvariantCulture)));
            return [.. rows];
        }

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var underEnglish = InvariantRows();
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
            InvariantRows().Should().Equal(underEnglish,
                "an invariant conversion that moved with the active culture would put a comma "
                + "decimal on the wire for a Brazilian reader and a dot for an American one");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
