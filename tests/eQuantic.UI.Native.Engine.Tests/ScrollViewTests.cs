using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>Spec A6 v1: unbounded child measurement, clamped programmatic offset, clipped realization.</summary>
public class ScrollViewTests
{
    private static Column TallContent(int items = 8)
    {
        var column = new Column(gap: 0) { Width = SizeValue.Fill };
        for (var i = 0; i < items; i++)
        {
            column.Add(new Primitives.Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = 30,
                Background = i % 2 == 0
                    ? PhotonTheme.Instance.Colors(Variant.Primary).Subtle
                    : PhotonTheme.Instance.SurfaceSubtle,
            }));
        }
        return column;
    }

    private static LayoutNode Layout(VisualNode root, float w = 120, float h = 100) =>
        LayoutEngine.Layout(root, w, h, new LayoutContext(PhotonTheme.Instance, new ApproximateTextMeasurer()));

    [Fact]
    public void ChildMeasuresUnbounded_ViewportStaysBounded()
    {
        var scroll = new ScrollView(TallContent()) { Height = SizeValue.Fixed(100), Width = SizeValue.Fill };
        var node = Layout(scroll);

        node.Bounds.Height.Should().Be(100, "the viewport is bounded");
        node.Children[0].Bounds.Height.Should().Be(240, "the child lays out its natural extent (8×30)");
    }

    [Fact]
    public void Offset_TranslatesTheChild_AndClamps()
    {
        var scrolled = Layout(new ScrollView(TallContent()) { Height = SizeValue.Fixed(100), Offset = 50 });
        scrolled.Children[0].Bounds.Y.Should().Be(-50);

        var beyond = Layout(new ScrollView(TallContent()) { Height = SizeValue.Fixed(100), Offset = 999 });
        beyond.Children[0].Bounds.Y.Should().Be(-(240 - 100), "the offset clamps to the max scroll extent");
    }

    [Fact]
    public void ScrolledViewport_Golden()
    {
        var scroll = new ScrollView(TallContent()) { Height = SizeValue.Fixed(100), Width = SizeValue.Fill, Offset = 45 };
        var root = new Primitives.Box(new BoxStyle { Padding = EdgeInsets.All(10) }, scroll);

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(140, 120);
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 140, 120);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);
        GoldenImage.Match(surface, "scrollview-offset");
    }
}
