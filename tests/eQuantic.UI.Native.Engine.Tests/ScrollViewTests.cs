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

    // ---- content that fills its viewport (#1143) -------------------------------------------------

    [Fact]
    public void AViewportFilledByItsChild_StretchesAShortChildToIt()
    {
        var node = Layout(new ScrollView(TallContent(2)) { Height = SizeValue.Fixed(100), Width = SizeValue.Fill, FillViewport = true });

        node.Children[0].Bounds.Height.Should().Be(100, "content shorter than its viewport is as tall as the viewport");
        node.ScrollRange.Should().Be(0, "and leaves nothing to scroll");
    }

    [Fact]
    public void AViewportFilledByItsChild_StillScrollsALongOne()
    {
        var node = Layout(new ScrollView(TallContent()) { Height = SizeValue.Fixed(100), Width = SizeValue.Fill, FillViewport = true });

        node.Children[0].Bounds.Height.Should().Be(240, "content longer than its viewport keeps its length");
        node.ScrollRange.Should().Be(140);
    }

    /// <summary>A size the content states stands on both axes, as under any stretch and as the web's flex
    /// line keeps it. Its bounds alone were raised to the viewport on the scroll axis, so what it held
    /// stayed laid out for the size it stated (found by Copilot reviewing #1158).</summary>
    [Fact]
    public void AViewportFilledByItsChild_KeepsASizeTheChildStates()
    {
        var node = Layout(new ScrollView(new Primitives.Box(new BoxStyle { Width = 30, Height = 40 }))
        {
            Height = SizeValue.Fixed(100), Width = SizeValue.Fixed(120), FillViewport = true,
        });

        node.Children[0].Bounds.Height.Should().Be(40, "its own height stands");
        node.Children[0].Bounds.Width.Should().Be(30, "and so does its own width");
    }

    /// <summary>A scroller of its own height inside one that fills keeps that height, and scrolls by what
    /// its content runs past IT: raised to the outer viewport, it kept the range of the height it stated, and
    /// showed blank room past its content (found by Copilot reviewing #1158).</summary>
    [Fact]
    public void AScrollerOfItsOwnHeightInsideAFilledViewport_KeepsItsHeightAndItsRange()
    {
        var inner = new ScrollView(TallContent()) { Height = SizeValue.Fixed(40), Width = SizeValue.Fill };
        var node = Layout(new ScrollView(inner) { Height = SizeValue.Fixed(100), Width = SizeValue.Fill, FillViewport = true });

        var scroller = node.Children[0];
        scroller.Bounds.Height.Should().Be(40);
        scroller.ScrollRange.Should().Be(200, "240 of content in a viewport of 40");
    }

    /// <summary>
    /// A text sizes itself to its lines and takes no stretch while it measures: across a sideways viewport
    /// that fills, it is given the viewport's height as a row's stretch gives it the line's, through a
    /// wrapper that takes its size, which the web's flex line does to the element (found by Copilot reviewing
    /// #1158).
    /// </summary>
    [Fact]
    public void ATextInASidewaysViewportThatFills_IsAsTallAsTheViewport()
    {
        var node = Layout(new ScrollView(new Pressable(new Primitives.Text("row", TypeRole.BodyM), () => { }), ScrollAxis.Horizontal)
        {
            Width = SizeValue.Fixed(120), Height = SizeValue.Fixed(80), FillViewport = true,
        });

        node.Children[0].Bounds.Height.Should().Be(80, "the wrapper is as tall as the viewport");
        node.Children[0].Children[0].Bounds.Height.Should().Be(80, "and so is the text whose size it takes");
    }

    [Fact]
    public void ASidewaysViewportFilledByItsChild_StretchesItBothWays()
    {
        var content = new Primitives.Box(new BoxStyle(), new Primitives.Box(new BoxStyle { Width = 50, Height = 20 }));
        var node = Layout(new ScrollView(content, ScrollAxis.Horizontal)
        {
            Width = SizeValue.Fixed(120), Height = SizeValue.Fixed(80), FillViewport = true,
        });

        node.Children[0].Bounds.Width.Should().Be(120, "as wide as its viewport");
        node.Children[0].Bounds.Height.Should().Be(80, "and as tall, which is its viewport across");
    }

    /// <summary>
    /// A scroll view stretched by its row is as tall as the row, as its `height: 100%` is on the web, so
    /// what fills it fills the row. It kept its content's height, and the row only moved its bounds after it
    /// had measured its content against that height: a code editor's code stopped where its file did.
    /// </summary>
    [Fact]
    public void AScrollViewStretchedByItsRow_FillsTheRow()
    {
        var content = new Primitives.Box(new BoxStyle(), new Primitives.Box(new BoxStyle { Width = 50, Height = 20 }));
        var row = new Row(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fixed(90), Cross = CrossAlign.Stretch };
        row.Add(new Flexible(new ScrollView(content, ScrollAxis.Horizontal) { FillViewport = true }));

        var node = Layout(row, 200, 300);

        var scroll = Find(node, n => n.Source is ScrollView)!;
        scroll.Bounds.Height.Should().Be(90, "the row stretches it");
        scroll.Children[0].Bounds.Height.Should().Be(90, "and its content fills what it was given");
    }

    /// <summary>A stack a viewport that fills stretches aligns what it holds in the stretched box, as the web's
    /// grid does in its flex line: it measured at its content's size and was only made larger afterwards, so
    /// a centred box stood at the corner (found by Copilot reviewing #1158).</summary>
    [Fact]
    public void AStackInAViewportThatFills_AlignsWhatItHoldsInTheViewport()
    {
        var stack = new Stack(Alignment.Center);
        stack.Add(new Primitives.Box(new BoxStyle { Width = 20, Height = 20 }));
        var node = Layout(new ScrollView(stack, ScrollAxis.Horizontal)
        {
            Width = SizeValue.Fixed(120), Height = SizeValue.Fixed(80), FillViewport = true,
        });

        (node.Children[0].Bounds.Width, node.Children[0].Bounds.Height).Should().Be((120f, 80f));
        var box = node.Children[0].Children[0].Bounds;
        (box.X, box.Y).Should().Be((50f, 30f), "centred in the viewport it fills");
    }

    /// <summary>A cap stands in a viewport that fills, as the web's `max-height` holds a growing flex item: the
    /// box measured at its cap and was then made as tall as the viewport, with what it held arranged for the
    /// cap (found by Copilot reviewing #1158).</summary>
    [Fact]
    public void ACappedBoxInAViewportThatFills_KeepsItsCap()
    {
        var capped = new Primitives.Box(new BoxStyle { MaxHeight = 40 }, new Primitives.Box(new BoxStyle { Width = 10, Height = 20 }));
        var node = Layout(new ScrollView(capped) { Height = SizeValue.Fixed(100), Width = SizeValue.Fill, FillViewport = true });

        node.Children[0].Bounds.Height.Should().Be(40);
    }

    /// <summary>A grid a viewport that fills stretches is as large as the viewport, as the web's grid is in its
    /// flex item; its tracks stay where they were laid (found by Copilot reviewing #1158).</summary>
    [Fact]
    public void AGridInAViewportThatFills_IsAsLargeAsTheViewport()
    {
        var grid = new Grid([GridTrack.Fixed(20)]);
        grid.Add(new Primitives.Box(new BoxStyle { Width = 20, Height = 20 }));
        var node = Layout(new ScrollView(grid, ScrollAxis.Horizontal)
        {
            Width = SizeValue.Fixed(120), Height = SizeValue.Fixed(80), FillViewport = true,
        });

        (node.Children[0].Bounds.Width, node.Children[0].Bounds.Height).Should().Be((120f, 80f));
        (node.Children[0].Children[0].Bounds.X, node.Children[0].Children[0].Bounds.Y).Should().Be((0f, 0f));
    }

    /// <summary>A component that builds a text is a text to a viewport that fills, as it is to the web's flex
    /// line, which lays out the element it built: its node wrapped the text, and the text stayed one line high
    /// (found by Copilot reviewing #1158).</summary>
    [Fact]
    public void AComponentThatBuildsATextInASidewaysViewportThatFills_IsAsTallAsTheViewport()
    {
        var host = new PhotonHost(new ScrollView(new BuildsAText(), ScrollAxis.Horizontal)
        {
            Width = SizeValue.Fixed(120), Height = SizeValue.Fixed(80), FillViewport = true,
        }, PhotonTheme.Instance, ThemeMode.Light, 200, 200);

        var frame = host.RenderFrame(new DisplayListBuilder());

        Find(frame.Root, n => n.Source is BuildsAText)!.Bounds.Height.Should().Be(80);
        Find(frame.Root, n => n.Source is Primitives.Text)!.Bounds.Height.Should().Be(80);
    }

    private sealed class BuildsAText : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Primitives.Text("row", TypeRole.BodyM);
    }

    private static LayoutNode? Find(LayoutNode node, Func<LayoutNode, bool> predicate)
    {
        if (predicate(node)) return node;
        foreach (var child in node.Children)
            if (Find(child, predicate) is { } found) return found;
        return null;
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
