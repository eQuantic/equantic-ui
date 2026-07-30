using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Spec S3 — the wrapping flex pass: natural-size children break onto new lines at the main extent,
/// lines stack with RunGap, per-line Main alignment and per-child cross (AlignSelf honored).
/// </summary>
public class S3WrapLayoutTests
{
    private static Box Chip(float width = 60, float height = 24) =>
        new(new BoxStyle { Width = width, Height = height, Background = new ColorToken(Color.FromRgb(0x88, 0x88, 0x88)) });

    private static LayoutNode Layout(FlexNode flex, float viewportW = 200, float viewportH = 400) =>
        LayoutEngine.Layout(flex, viewportW, viewportH,
            new LayoutContext(PhotonTheme.Instance, new ApproximateTextMeasurer()));

    [Fact]
    public void Wrap_BreaksLines_AtTheMainExtent()
    {
        // Three 90dp chips + 8dp gap in a 200dp row: two fit (90+8+90=188), the third wraps.
        var row = new Row(gap: 8) { Wrap = true, Width = SizeValue.Fill };
        row.Add(Chip(90));
        row.Add(Chip(90));
        row.Add(Chip(90));

        var node = Layout(row);

        node.Children[0].Bounds.Y.Should().Be(node.Children[1].Bounds.Y, "first two share a line");
        node.Children[2].Bounds.Y.Should().BeGreaterThan(node.Children[0].Bounds.Y, "the third wrapped");
        node.Children[2].Bounds.X.Should().Be(0, "a new line restarts at the main origin");
        node.Bounds.Height.Should().Be(24 + 8 + 24, "two lines stacked with the run gap (= gap)");
    }

    [Fact]
    public void RunGap_SpacesLines_IndependentlyOfGap()
    {
        var row = new Row(gap: 4) { Wrap = true, RunGap = 20, Width = SizeValue.Fill };
        for (var i = 0; i < 4; i++) row.Add(Chip(90));

        var node = Layout(row);

        node.Children[2].Bounds.Y.Should().Be(24 + 20, "lines stack with RunGap, not Gap");
        node.Bounds.Height.Should().Be(24 + 20 + 24);
    }

    [Fact]
    public void AlignSelf_AppliesWithinTheLine()
    {
        var row = new Row(gap: 8) { Wrap = true, Cross = CrossAlign.End, Width = SizeValue.Fill };
        row.Add(Chip(60, 40));
        row.Add(Chip(60, 20));
        var node = Layout(row);

        node.Children[1].Bounds.Y.Should().Be(20, "End-aligned within its 40dp line");
    }

    [Theory]
    [InlineData(ThemeMode.Light, "s3-wrap-light")]
    [InlineData(ThemeMode.Dark, "s3-wrap-dark")]
    public void WrapGallery_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        var column = new Column(gap: Space.S3) { Padding = EdgeInsets.All(Space.S3), Width = SizeValue.Fill };
        var tags = new Row(gap: Space.S2) { Wrap = true, RunGap = Space.S3, Width = SizeValue.Fill };
        foreach (var w in new float[] { 70, 90, 60, 110, 80, 75, 95 })
        {
            tags.Add(new Box(new BoxStyle
            {
                Width = w, Height = 28,
                Background = new ColorToken(Color.FromRgb(0x3A, 0x6E, 0xA5)),
                CornerRadius = new CornerRadii(Radius.Full),
            }));
        }
        column.Add(tags);

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(280, 180);
        var host = new PhotonHost(column, PhotonTheme.Instance, mode, 280, 180);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, golden);
    }
}
