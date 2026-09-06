using eQuantic.UI.Charts;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The bar chart on Photon: the same component the web realizer renders, laid out by the engine and
/// drawn through its own shapes. Asserted on the DISPLAY LIST, which is what the GPU receives — the
/// bars are filled rounded rectangles in the series' palette colours, placed beside the value-axis
/// band the chrome reserves.
/// </summary>
public class BarChartPhotonTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static DrawCommand[] Render(VisualNode root, float width, float height)
    {
        var host = new PhotonHost(root, Theme, ThemeMode.Light, width, height);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 0);
        return builder.Build().Commands.ToArray();
    }

    /// <summary>The filled rects in <paramref name="color"/> that are BARS: 24dp thick here — the
    /// cap, since four categories leave the slot room to spare. The legend's swatch in the same colour
    /// is 12dp and is chrome, not a mark.</summary>
    private static List<DrawCommand> Marks(DrawCommand[] commands, Color color) => commands
        .Where(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == color
                    && MathF.Abs(c.Shape.Rect.Width - BarChartLayout.MaxThickness) < 0.01f)
        .ToList();

    private static BarChart Chart(BarLayout layout = BarLayout.Grouped) => new(
        [new ChartSeries("Alpha", [12, 18, 9, 4]), new ChartSeries("Beta", [15, 21, 14, 8])],
        new CategoryAxis(["Q1", "Q2", "Q3", "Q4"]),
        layout: layout,
        title: "Revenue");

    [Fact]
    public void TheBars_AreDrawnWithTheEnginesOwnShapes_InTheSeriesColours()
    {
        var commands = Render(Chart(), 400, 320);

        var alpha = Theme.Data.SeriesColor(0).Resolve(ThemeMode.Light);
        var beta = Theme.Data.SeriesColor(1).Resolve(ThemeMode.Light);
        // Four bars per series, each a rounded rect plus the plain one that squares its baseline end.
        Marks(commands, alpha).Should().HaveCount(8);
        Marks(commands, beta).Should().HaveCount(8);
        // And every bar lands right of the band the value-axis labels reserve (at least its minimum;
        // a long label widens it): the canvas is offered the plot beside it and draws in its own coordinates.
        Marks(commands, alpha).Should().OnlyContain(c => c.Shape.Rect.X >= BarChart.MinValueAxisWidth);
        // The legend swatch in the same colour is chrome — 12dp, in the row above the plot.
        commands.Count(c => c.Kind == DrawCommandKind.FillRRect && c.Paint.Color == alpha && c.Shape.Rect.Width == 12)
            .Should().Be(1, "the legend's swatch");
    }

    [Fact]
    public void TheMarks_SitBesideTheValueAxisBand_TheChromeReserved()
    {
        var commands = Render(Chart(), 400, 320);

        var alpha = Theme.Data.SeriesColor(0).Resolve(ThemeMode.Light);
        var bars = Marks(commands, alpha);
        bars.Should().HaveCount(8);
        bars.Should().OnlyContain(c => c.Shape.Rect.X >= BarChart.MinValueAxisWidth);
        // Bars stand on the baseline: the tallest Alpha bar (18 of a 0..25 scale) is taller than the shortest (4).
        var heights = bars.Select(c => c.Shape.Rect.Height).OrderBy(h => h).ToList();
        heights.Last().Should().BeGreaterThan(heights.First());
    }

    /// <summary>
    /// What a scroll view relies on: the chart's measured height covers everything it places. Laid
    /// out under an unbounded height, as a scroll view's content is, no descendant may end below the
    /// chart's own bottom — a scroll region sized by that height would otherwise refuse to scroll to
    /// a control it can see.
    /// </summary>
    [Fact]
    public void TheChart_MeasuresAsTallAsEverythingItPlaces_UnderAnUnboundedHeight()
    {
        var context = new LayoutContext(Theme, new ApproximateTextMeasurer());
        var node = LayoutEngine.Layout(Chart(), 400, float.PositiveInfinity, context);

        var (deepest, chain) = DeepestBottom(node, "");
        deepest.Should().BeLessThanOrEqualTo(node.Bounds.Y + node.Bounds.Height + 0.5f,
            $"the chart measured {node.Bounds.Height}dp tall but places a descendant ending at {deepest}dp: {chain}");
    }

    /// <summary>Bounds come back from <see cref="LayoutEngine.Layout"/> ABSOLUTE, so a bottom is read
    /// off each node directly; the chain names who reaches deepest.</summary>
    private static (float Bottom, string Chain) DeepestBottom(LayoutNode node, string chain)
    {
        var here = chain + "/" + (node.Source?.GetType().Name ?? "?") + $"[{node.Bounds.Y:0.#}+{node.Bounds.Height:0.#}]";
        var best = (Bottom: node.Bounds.Y + node.Bounds.Height, Chain: here);
        foreach (var child in node.Children)
        {
            var deeper = DeepestBottom(child, here);
            if (deeper.Bottom > best.Bottom) best = deeper;
        }

        return best;
    }

    /// <summary>A screen that REBUILDS its chart every pass, which is what a real page does: the
    /// retained instance then learns the fresh arguments through AdoptConfig, and whatever that
    /// method throws away is thrown away on every interaction.</summary>
    private sealed class Screen : StatefulComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column { Width = SizeValue.Fill };
            column.Add(Chart());
            return column;
        }
    }

    /// <summary>
    /// The hover on a chart INSIDE a page — the only arrangement a consumer has. The hover's own
    /// SetState rebuilds the page, the page hands the retained chart a fresh configuration, and the
    /// tooltip must survive that: what the pointer is over is state, and the pointer has not moved.
    /// Hosted as a bare root instead, this passes with or without that being true — nothing rebuilds
    /// a root, so AdoptConfig never runs. "12" is Alpha's first value and appears nowhere else.
    /// </summary>
    [Fact]
    public void HoveringABar_ShowsItsTooltip_ThroughThePageRebuildItCauses()
    {
        var host = new PhotonHost(new Screen(), Theme, ThemeMode.Light, 400, 320);
        var first = new DisplayListBuilder();
        host.RenderFrame(first, 0);
        Labels(host).Should().NotContain("12", "nothing is hovered yet");

        var alpha = Theme.Data.SeriesColor(0).Resolve(ThemeMode.Light);
        var bar = Marks(first.Build().Commands.ToArray(), alpha).First().Shape.Rect;
        host.PointerMove(bar.X + bar.Width / 2, bar.Y + bar.Height / 2);
        host.RenderFrame(new DisplayListBuilder(), 16);

        Labels(host).Should().Contain("12").And.Contain("Q1", "the tooltip names the category and the value");

        host.PointerMove(2, 2);   // off the plot: the tooltip goes
        host.RenderFrame(new DisplayListBuilder(), 32);
        Labels(host).Should().NotContain("12");
    }

    /// <summary>
    /// The hover: the pointer over a bar brings up the tooltip with the bar's value — routed by the
    /// host to the canvas's own handler, the same handler the browser's pointermove reaches. "12"
    /// is Alpha's first value and appears nowhere else (the axis ticks are 0, 5, 10 … 25).
    /// </summary>
    [Fact]
    public void HoveringABar_ShowsItsTooltip_WithTheValue()
    {
        var host = new PhotonHost(Chart(), Theme, ThemeMode.Light, 400, 320);
        var first = new DisplayListBuilder();
        host.RenderFrame(first, 0);
        Labels(host).Should().NotContain("12", "nothing is hovered yet");

        var alpha = Theme.Data.SeriesColor(0).Resolve(ThemeMode.Light);
        var bar = Marks(first.Build().Commands.ToArray(), alpha).First().Shape.Rect;
        host.PointerMove(bar.X + bar.Width / 2, bar.Y + bar.Height / 2);
        host.RenderFrame(new DisplayListBuilder(), 16);

        Labels(host).Should().Contain("12").And.Contain("Q1", "the tooltip names the category and the value");

        host.PointerMove(2, 2);   // off the plot: the tooltip goes
        host.RenderFrame(new DisplayListBuilder(), 32);
        Labels(host).Should().NotContain("12");
    }

    private static List<string> Labels(PhotonHost host) =>
        host.Semantics().Where(s => s.Role == SemanticRole.StaticText).Select(s => s.Label ?? "").ToList();

    [Fact]
    public void StackedBars_DrawEverySegment_InItsOwnSeriesColour()
    {
        var commands = Render(Chart(BarLayout.Stacked), 400, 320);

        var alpha = Theme.Data.SeriesColor(0).Resolve(ThemeMode.Light);
        var beta = Theme.Data.SeriesColor(1).Resolve(ThemeMode.Light);
        // Alpha is the inner segment (square both ends: one rect each); Beta is on top (rounded + square).
        Marks(commands, alpha).Should().HaveCount(4);
        Marks(commands, beta).Should().HaveCount(8);
    }
}
