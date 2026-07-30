using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Spec S4 — the grid track-sizing pass: fixed/flex/auto tracks, auto-flow with span clamping,
/// rows sized to their tallest cell.
/// </summary>
public class S4GridLayoutTests
{
    private static Box Cell(float h = 24, float? w = null) => new(new BoxStyle
    {
        Width = w is { } fixedW ? fixedW : SizeValue.Fill,
        Height = h,
        Background = new ColorToken(Color.FromRgb(0x88, 0x88, 0x88)),
    });

    private static LayoutNode Layout(Grid grid, float w = 320, float h = 400) =>
        LayoutEngine.Layout(grid, w, h, new LayoutContext(PhotonTheme.Instance, new ApproximateTextMeasurer()));

    [Fact]
    public void FlexTracks_ShareTheLeftover_ByWeight()
    {
        // 320 avail: fixed 100 + gap 2×10 = 120 used → 200 leftover; flex 1:3 → 50 / 150.
        var grid = new Grid([GridTrack.Flex(1), GridTrack.Flex(3), GridTrack.Fixed(100)], gap: 10)
        {
            Width = SizeValue.Fill,
        };
        grid.Add(Cell()); grid.Add(Cell()); grid.Add(Cell());

        var node = Layout(grid);

        node.Children[0].Bounds.Width.Should().Be(50);
        node.Children[1].Bounds.Width.Should().Be(150);
        node.Children[1].Bounds.X.Should().Be(60, "50 + 10 gap");
        node.Children[2].Bounds.Width.Should().Be(100);
    }

    [Fact]
    public void AutoFlow_WrapsRows_AndSpansClamp()
    {
        var grid = new Grid(GridTrack.Repeat(2, GridTrack.Flex()), gap: 8) { Width = SizeValue.Fill };
        grid.Add(Cell(h: 30));
        grid.Add(Cell(h: 50));
        grid.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }) { GridSpan = 2 });

        var node = Layout(grid, 208);

        node.Children[0].Bounds.Y.Should().Be(0);
        node.Children[1].Bounds.Y.Should().Be(0);
        node.Children[2].Bounds.Y.Should().Be(50 + 8, "row 1 is its tallest cell (50) + rowGap");
        node.Children[2].Bounds.Width.Should().Be(208, "the span covers both 100dp tracks + the 8 gap");
        node.Bounds.Height.Should().Be(50 + 8 + 20);
    }

    [Fact]
    public void AutoTrack_SizesToItsWidestItem()
    {
        var grid = new Grid([GridTrack.Auto, GridTrack.Flex()], gap: 0) { Width = SizeValue.Fill };
        grid.Add(Cell(w: 72));
        grid.Add(Cell());

        var node = Layout(grid, 300);

        node.Children[0].Bounds.Width.Should().Be(72);
        node.Children[1].Bounds.X.Should().Be(72);
        node.Children[1].Bounds.Width.Should().Be(228);
    }

    [Theory]
    [InlineData(ThemeMode.Light, "s4-grid-light")]
    [InlineData(ThemeMode.Dark, "s4-grid-dark")]
    public void GridGallery_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        var grid = new Grid(GridTrack.Repeat(3, GridTrack.Flex()), gap: Space.S2)
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S3),
        };
        grid.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 40, Background = new ColorToken(Color.FromRgb(0x3A, 0x6E, 0xA5)), CornerRadius = new CornerRadii(Radius.Md) }) { GridSpan = 2 });
        grid.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 40, Background = new ColorToken(Color.FromRgb(0xB0, 0x5A, 0x2B)), CornerRadius = new CornerRadii(Radius.Md) }));
        for (var i = 0; i < 3; i++)
            grid.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 28, Background = new ColorToken(Color.FromRgb(0x4E, 0x7A, 0x52)), CornerRadius = new CornerRadii(Radius.Sm) }));

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(280, 140);
        var host = new PhotonHost(grid, PhotonTheme.Instance, mode, 280, 140);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, golden);
    }
}
