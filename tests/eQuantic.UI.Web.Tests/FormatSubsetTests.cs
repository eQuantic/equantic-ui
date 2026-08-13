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
    /// whose group separator is the other's decimal point — the three ways a number can go wrong.</summary>
    private static readonly string[] Cultures = ["en-US", "pt-BR", "de-DE"];

    private static readonly double[] Numbers = [1234.5, -1234.5, 0, 0.125, 1000000];

    // The empty spec is the one everybody writes — `{0}` — and it is NOT invariant in .NET: it
    // calls ToString(IFormatProvider), so 1234.5 is "1234,5" in pt-BR. Pinned like the rest.
    private static readonly string[] NumberSpecs = ["", "N0", "N2", "F2", "F0", "P1", "P0", "C2", "C0"];

    /// <summary>`D` and `X` are INTEGER specifiers in .NET — <c>(1234.5).ToString("D5")</c> throws,
    /// and a subset that pretended otherwise would be promising something the server cannot do.</summary>
    private static readonly long[] Integers = [42, -42, 1000000];

    // No `X`: hex of a NEGATIVE value is two's complement at the C# type's width
    // (`(-42).ToString("X4")` is 8 digits as an int, 16 as a long), and the browser sees one
    // untyped number. Outside the subset by construction, and EQ2100 refuses it in a template
    // rather than letting the two sides disagree.
    private static readonly string[] IntegerSpecs = ["", "D5", "D", "N0"];

    /// <summary>One fixed instant, spelled as LOCAL parts on both sides — the fixture must not
    /// depend on the machine's time zone, only on its culture data.</summary>
    private static readonly DateTime Moment = new(2026, 8, 13, 15, 45, 7);

    private static readonly string[] DateSpecs = ["d", "D", "t", "T", "g", "G", "M", "Y", "yyyy-MM-dd"];

    /// <summary>ICU's non-breaking spaces, folded — see the type's remarks.</summary>
    internal static string Normalize(string value) =>
        value.Replace(' ', ' ').Replace(' ', ' ');

    [Fact]
    public void TheFormatSubset_IsWhatBothRuntimesProduce()
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

        var actual = builder.ToString();
        if (Environment.GetEnvironmentVariable("EQ_UPDATE_FORMAT_FIXTURE") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FixturePath)!);
            File.WriteAllText(FixturePath, actual);
            return;
        }

        File.Exists(FixturePath).Should().BeTrue(
            "generate once with EQ_UPDATE_FORMAT_FIXTURE=1");
        File.ReadAllText(FixturePath).Should().Be(actual,
            "the .NET formatting the fixture pins changed — regenerate with "
            + "EQ_UPDATE_FORMAT_FIXTURE=1 and make the vitest half pass again, or drop the case "
            + "from the subset (and have EQ2100 refuse it) rather than let the two sides drift");
    }
}
