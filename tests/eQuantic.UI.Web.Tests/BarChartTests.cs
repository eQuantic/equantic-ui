using System.Globalization;
using System.Runtime.CompilerServices;
using eQuantic.UI.Charts;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The bar chart, asserted through what it RENDERS and through the geometry both compilations
/// solve. The layout half is a cross-pin: <see cref="BarChartLayout"/> runs as C# here and on
/// Photon and as the transpiled twin in the browser, and the pinned fixture at
/// <c>src/eQuantic.UI.Runtime/src/shared/__fixtures__/bar-chart-layout.txt</c> is the promise that
/// both place every bar on the same numbers — <c>bar-chart-layout.spec.ts</c> asserts the SAME file
/// against the twin. One dumper, mirrored line for line. Regenerate with
/// <c>EQ_UPDATE_BAR_CHART_FIXTURE=1</c>.
/// </summary>
public class BarChartTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static string RepoRoot([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));

    private static string FixturePath => Path.Combine(RepoRoot(),
        "src", "eQuantic.UI.Runtime", "src", "shared", "__fixtures__", "bar-chart-layout.txt");

    /// <summary>The canonical data both sides solve: a negative value, a zero, and an uneven tail.</summary>
    private static readonly ChartSeries[] Series =
    [
        new("Alpha", [12, 18, 9, 4]),
        new("Beta", [15, 21, 14, 8]),
        new("Gamma", [-3, 6, 0, 10]),
    ];

    private static readonly CategoryAxis Categories = new(["Q1", "Q2", "Q3", "Q4"]);
    private static readonly bool[] All = [true, true, true];
    private static readonly bool[] WithoutBeta = [true, false, true];

    // ---- Rendering ------------------------------------------------------------------------------

    private static HtmlNode Render(VisualNode node, string culture = "en-US")
    {
        var previousFormat = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            return WebRealizer.Lower(node, Theme).Render();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousFormat;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    private static IEnumerable<HtmlNode> Descendants(HtmlNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var deeper in Descendants(child)) yield return deeper;
        }
    }

    private static List<string> Texts(HtmlNode root) =>
        Descendants(root).Select(n => n.TextContent).Where(t => !string.IsNullOrEmpty(t)).Select(t => t!).ToList();

    [Fact]
    public void ARenderedChart_HasItsChromeAsNodes_AndItsMarksAsACanvas()
    {
        var chart = new BarChart(Series, Categories, new ValueAxis(Format: "N0"), title: "Revenue");

        var html = Render(chart);
        var texts = Texts(html);

        // Two or more series: a legend, one entry per series, each an accessible press target.
        Descendants(html).Where(n => n.Attributes.TryGetValue("aria-label", out var l) && l is "Alpha" or "Beta" or "Gamma")
            .Should().HaveCount(3, "the legend has one pressable entry per series");
        // The category axis under the plot, and the value ticks beside it — text, never paint.
        texts.Should().Contain(["Q1", "Q2", "Q3", "Q4"]);
        // Nice(-3, 21, 5): step 5 from -5 to 25, seven ticks.
        texts.Should().Contain(["-5", "0", "5", "10", "15", "20", "25"]);
        // The marks: one canvas, named after the chart so a screen reader has something to say.
        var svg = Descendants(html).Single(n => n.Tag == "svg");
        svg.Attributes.Should().ContainKey("aria-label").WhoseValue.Should().Be("Revenue");
        // The footer offers the WCAG twin, in the SDK's own words.
        texts.Should().Contain("Show as table");
        texts.Should().Contain("Revenue");
    }

    [Fact]
    public void ASingleSeries_DrawsNoLegend()
    {
        var chart = new BarChart([Series[0]], Categories);

        var html = Render(chart);

        Descendants(html).Where(n => n.Attributes.TryGetValue("aria-label", out var l) && l == "Alpha")
            .Should().BeEmpty("one series needs no legend box — the title names it");
    }

    [Fact]
    public void TheTableView_IsTheSameDataAsATable()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var table = BarChart.Table(Series, Categories, new ValueAxis(Format: "C0"), Theme);

            table.Columns.Should().HaveCount(4, "the category column and one per series");
            table.Columns.Skip(1).Select(c => c.Header).Should().Equal("Alpha", "Beta", "Gamma");
            table.Rows.Should().HaveCount(4);
            table.Rows[0].Key.Should().Be("Q1");
            // The same format the axis and the tooltip use, in the request's culture.
            Texts(Render(table)).Should().Contain(["$12", "$15", "-$3", "$0"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ANinthSeries_IsRefusedByName_NotGivenAColour()
    {
        var nine = Enumerable.Range(0, 9).Select(i => new ChartSeries($"S{i}", [1, 2])).ToList();
        var chart = new BarChart(nine, new CategoryAxis(["a", "b"]));

        // Built directly: through the realizer, ComponentBoundary would contain the throw and draw
        // its card in the chart's place — which is right for a page, and hides the refusal here.
        var act = () => chart.Build(new ComponentContext(Theme));

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*fold the tail into Other*");
    }

    // ---- The value scale ------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 21, 5, 0, 25, 5)]
    [InlineData(-3, 21, 5, -5, 25, 5)]
    [InlineData(0, 0.7, 5, 0, 0.8, 0.2)]
    [InlineData(0, 1000, 5, 0, 1000, 200)]
    [InlineData(5, 5, 5, 5, 6, 0.2)]
    public void NiceTicks_SnapToCleanSteps(double low, double high, int target, double min, double max, double step)
    {
        var ticks = ValueScale.Nice(low, high, target);

        ticks.Min.Should().BeApproximately(min, 1e-9);
        ticks.Max.Should().BeApproximately(max, 1e-9);
        ticks.Step.Should().BeApproximately(step, 1e-9);
        ticks.At(ticks.Count - 1).Should().BeApproximately(max, 1e-9, "the last tick lands on Max exactly");
    }

    // ---- The geometry ---------------------------------------------------------------------------

    [Fact]
    public void GroupedColumns_ShareTheSlot_WithTheGapAndTheCap()
    {
        var g = BarChartLayout.Solve(Series, All, 4, BarLayout.Grouped, ChartOrientation.Vertical, new ValueAxis(), 320, 200);

        g.Bars.Should().HaveCount(12);
        // slot 80; (80 - 4 gaps) / 3 = 24 = the cap; group 76 wide, centred: starts at 2.
        g.Bars[0].X.Should().Be(2);
        g.Bars[0].Width.Should().Be(24);
        g.Bars[1].X.Should().Be(2 + 24 + BarChartLayout.Gap);
        g.Bars[3].X.Should().Be(80 + 2, "the second category starts a slot later");
        // Ticks -5..25 over 200dp: zero sits 5/30 of the way up, i.e. 200 - 33.333 from the top.
        g.Baseline.Should().BeApproximately(200 - (200f / 6), 0.01f);
        // A negative value grows DOWN from the baseline; a zero has no height but a place.
        var gamma1 = g.Bars.Single(b => b.Series == 2 && b.Category == 0);
        gamma1.Negative.Should().BeTrue();
        gamma1.Y.Should().BeApproximately(g.Baseline, 0.01f);
        g.Bars.Single(b => b.Series == 2 && b.Category == 2).Height.Should().Be(0);
        g.Bars.Should().OnlyContain(b => b.DataEnd, "every grouped bar carries the rounded data end");
    }

    [Fact]
    public void StackedSegments_LeaveTheGap_AndOnlyTheOutermostRoundsOff()
    {
        var g = BarChartLayout.Solve(Series, All, 4, BarLayout.Stacked, ChartOrientation.Vertical, new ValueAxis(), 320, 200);

        var q2 = g.Bars.Where(b => b.Category == 1).ToList();
        q2.Should().HaveCount(3);
        q2.Where(b => b.DataEnd).Should().ContainSingle("one outermost positive segment in Q2 (no negatives there)");
        q2.Single(b => b.DataEnd).Series.Should().Be(2, "the last positive series is on top");
        // The inner segments end 2dp short so the surface shows between fills.
        var alpha = q2.Single(b => b.Series == 0);
        var beta = q2.Single(b => b.Series == 1);
        (alpha.Y - (beta.Y + beta.Height)).Should().BeApproximately(BarChartLayout.Gap, 0.01f);
        // Q1 stacks a negative under the baseline: it is the only negative, so it is a data end too.
        g.Bars.Where(b => b.Category == 0 && b.Negative).Should().ContainSingle().Which.DataEnd.Should().BeTrue();
    }

    [Fact]
    public void HidingASeries_KeepsTheOthersInPlace_AndTheirColourSlot()
    {
        var all = BarChartLayout.Solve(Series, All, 4, BarLayout.Grouped, ChartOrientation.Vertical, new ValueAxis(), 320, 200);
        var without = BarChartLayout.Solve(Series, WithoutBeta, 4, BarLayout.Grouped, ChartOrientation.Vertical, new ValueAxis(), 320, 200);

        without.Bars.Should().HaveCount(8);
        without.Bars.Select(b => b.Series).Distinct().Should().BeEquivalentTo([0, 2], "the series index — and so the colour slot — survives the filter");
        // Beta held the maximum (21): without it the scale relaxes to the data that is left.
        all.Ticks.Max.Should().Be(25);
        without.Ticks.Max.Should().Be(20);
    }

    [Fact]
    public void HitTest_AnswersWithinTheSlack_AndNotBeyond()
    {
        var g = BarChartLayout.Solve(Series, All, 4, BarLayout.Grouped, ChartOrientation.Vertical, new ValueAxis(), 320, 200);
        var first = g.Bars[0];

        BarChartLayout.HitTest(g, first.X + (first.Width / 2), first.Y + (first.Height / 2)).Should().Be(0);
        BarChartLayout.HitTest(g, first.X - BarChartLayout.HitSlack, first.Y + 1).Should().Be(0);
        BarChartLayout.HitTest(g, -20, -20).Should().Be(-1);
    }

    // ---- The shared dumper (mirrored in bar-chart-layout.spec.ts) -------------------------------

    /// <summary>Three decimals, floored at the half — the SAME arithmetic the twin applies to the
    /// same float, so both sides print one text; a negative zero prints as zero.</summary>
    private static string F(float v)
    {
        var d = Math.Floor(((double)v * 1000) + 0.5) / 1000;
        if (d == 0) d = 0;
        return d.ToString(CultureInfo.InvariantCulture);
    }

    private static string F(double v) => F((float)v);

    private static string Dump(BarChartGeometry g)
    {
        var lines = new List<string>
        {
            $"size {F(g.Width)}x{F(g.Height)} {g.Orientation.ToString().ToLowerInvariant()}",
            $"ticks {F(g.Ticks.Min)} {F(g.Ticks.Max)} step {F(g.Ticks.Step)} count {g.Ticks.Count}",
            $"baseline {F(g.Baseline)}",
        };
        foreach (var b in g.Bars)
        {
            lines.Add($"bar c{b.Category} s{b.Series} {F(b.X)},{F(b.Y)} {F(b.Width)}x{F(b.Height)}"
                + (b.Negative ? " neg" : "") + (b.DataEnd ? " end" : ""));
        }

        return string.Join("\n", lines);
    }

    private static string Scenario(string name, BarLayout layout, ChartOrientation orientation, bool[] visible,
        ValueAxis axis, float width, float height) =>
        $"== {name} ==\n" + Dump(BarChartLayout.Solve(Series, visible, Categories.Categories.Count, layout, orientation, axis, width, height));

    [Fact]
    public void TheLayoutFixture_IsWhatBothCompilationsProduce()
    {
        var actual = string.Join("\n",
        [
            Scenario("grouped-vertical", BarLayout.Grouped, ChartOrientation.Vertical, All, new ValueAxis(), 320, 200),
            Scenario("stacked-vertical", BarLayout.Stacked, ChartOrientation.Vertical, All, new ValueAxis(), 320, 200),
            Scenario("grouped-horizontal", BarLayout.Grouped, ChartOrientation.Horizontal, All, new ValueAxis(), 320, 200),
            Scenario("stacked-horizontal-hidden", BarLayout.Stacked, ChartOrientation.Horizontal, WithoutBeta, new ValueAxis(), 320, 200),
            Scenario("grouped-fixed-axis", BarLayout.Grouped, ChartOrientation.Vertical, All, new ValueAxis(null, 0, 40, "N0", 5), 250, 100),
        ]) + "\n";

        if (Environment.GetEnvironmentVariable("EQ_UPDATE_BAR_CHART_FIXTURE") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FixturePath)!);
            File.WriteAllText(FixturePath, actual);
        }

        File.Exists(FixturePath).Should().BeTrue(
            $"the runtime's spec asserts the same fixture at {FixturePath} — write it once with EQ_UPDATE_BAR_CHART_FIXTURE=1");
        File.ReadAllText(FixturePath).Should().Be(actual,
            "the geometry the C# solver produces changed — if that is intended, regenerate with EQ_UPDATE_BAR_CHART_FIXTURE=1 and run the vitest twin");
    }

    /// <summary>
    /// The three ways an author overrides the axis, which the wiki documents: BOTH bounds are taken
    /// exactly, with the tick count dividing them; ONE bound clamps that end and the other still
    /// follows the data through the nice scale. A lone bound doing nothing would be a silent trap —
    /// "always start at zero" is the commonest thing anyone asks a chart for.
    /// </summary>
    [Fact]
    public void FixedBounds_AreTakenExactly_AndALoneBoundStillClampsItsEnd()
    {
        var both = BarChartLayout.Ticks(Series, All, 4, BarLayout.Grouped, new ValueAxis(Min: 0, Max: 40, Ticks: 5));
        (both.Min, both.Max, both.Count).Should().Be((0d, 40d, 5));

        // Gamma dips to -3, so the data alone would open the axis below zero (see NiceTicks).
        var floored = BarChartLayout.Ticks(Series, All, 4, BarLayout.Grouped, new ValueAxis(Min: 0));
        floored.Min.Should().Be(0, "the lone floor holds");
        floored.Max.Should().Be(25, "the top still follows the data");

        var ceiled = BarChartLayout.Ticks(Series, All, 4, BarLayout.Grouped, new ValueAxis(Max: 100));
        ceiled.Max.Should().Be(100);
        ceiled.Min.Should().BeLessThan(0, "the floor still follows Gamma's negative");
    }

    // ---- The value-axis band ----------------------------------------------------------------------

    /// <summary>
    /// The band beside a vertical plot is sized by its longest label, from the TEXT, so both
    /// compilations reserve the same width without measuring anything: "25" fits the minimum,
    /// "$200,000" does not — the first currency axis in a sample clipped every label to "$200,…".
    /// </summary>
    [Fact]
    public void TheValueAxisBand_WidensToItsLongestLabel()
    {
        var small = ValueScale.Nice(0, 21, 5);
        BarChart.ValueBandWidth(new ValueAxis(), small).Should().Be(BarChart.MinValueAxisWidth);

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var money = ValueScale.Nice(0, 171_000, 5);
            var axis = new ValueAxis(Format: "C0");
            var longest = Enumerable.Range(0, money.Count).Max(i => axis.Label(money.At(i)).Length);
            longest.Should().Be("$200,000".Length);
            BarChart.ValueBandWidth(axis, money).Should().BeGreaterThan(BarChart.MinValueAxisWidth)
                .And.Be(longest * 7f + Space.S2);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    // ---- The empty chart --------------------------------------------------------------------------

    /// <summary>
    /// No series yet — the state a dashboard is in before its first fetch answers. The chrome stands
    /// (title, axis on a clean 0..1 scale), no mark is drawn, nothing divides by the zero categories.
    /// </summary>
    [Fact]
    public void AnEmptyChart_StandsItsChrome_AndDrawsNoMark()
    {
        var html = Render(new BarChart([], new CategoryAxis([]), title: "Nothing yet"));
        Texts(html).Should().Contain("Nothing yet");

        var geometry = BarChartLayout.Solve([], [], 0, BarLayout.Grouped, ChartOrientation.Vertical,
            new ValueAxis(), 320, 200);
        geometry.Bars.Should().BeEmpty();
        geometry.Ticks.Min.Should().Be(0);
        geometry.Ticks.Max.Should().BeGreaterThan(0, "a degenerate range still gets a readable axis");
    }
}
