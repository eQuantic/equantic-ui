using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// <c>flex: grow shrink basis</c> inside a WRAPPING container — the second pass that was missing.
/// <para>
/// A wrapping row used to throw a <see cref="Flexible"/> away and measure its child at natural
/// size, so weights never distributed and a basis could not be stated at all. That made the
/// responsive two-pane layout every real app wants inexpressible: panes that sit side by side while
/// there is room for both, and take a full line each when there is not.
/// </para>
/// </summary>
public class FlexBasisWrapLayoutTests
{
    private static Box Pane(float height = 100) =>
        new(new BoxStyle { Height = height, Background = new ColorToken(Color.FromRgb(0x44, 0x44, 0x44)) });

    private static LayoutNode Layout(FlexNode flex, float viewportW, float viewportH = 800) =>
        LayoutEngine.Layout(flex, viewportW, viewportH,
            new LayoutContext(PhotonTheme.Instance, new ApproximateTextMeasurer()));

    /// <summary>
    /// A weighted item whose basis fills its line exactly occupies that basis, although the line
    /// neither grows nor shrinks: in a wrapping row of 540, Chrome 154 gives
    /// <c>Flexible(400 box, flex: 1, basis: 540)</c> an item of 540 with the box at 400 inside it.
    /// Photon had no item on such a line and occupied the box's 400.
    /// </summary>
    [Fact]
    public void AWeightedItem_OccupiesItsBasis_OnALineItFitsExactly()
    {
        var row = new Row(gap: 0) { Wrap = true, Width = 540 };
        row.Add(new Flexible(new Box(new BoxStyle { Width = 400, Height = 20 }), flex: 1, basis: 540));
        var item = Layout(row, viewportW: 2000).Children[0];

        item.Bounds.Width.Should().BeApproximately(540, 0.5f);
        item.Children[0].Bounds.Width.Should().Be(400);
    }

    /// <summary>Wide enough for both bases: one line, and the leftover is shared by weight.</summary>
    [Fact]
    public void WithRoomForBothBases_TheySitSideBySide()
    {
        var row = new Row(gap: 0) { Wrap = true, Width = SizeValue.Fill };
        row.Add(new Flexible(Pane(), flex: 1, basis: 440));
        row.Add(new Flexible(Pane(), flex: 1, basis: 380));

        var node = Layout(row, viewportW: 1000);

        node.Children[0].Bounds.Y.Should().Be(node.Children[1].Bounds.Y, "both fit on one line");
        // 1000 - (440+380) = 180 of leftover, split evenly between two equal weights.
        node.Children[0].Bounds.Width.Should().BeApproximately(440 + 90, 0.5f);
        node.Children[1].Bounds.Width.Should().BeApproximately(380 + 90, 0.5f);
        node.Children[1].Bounds.X.Should().BeApproximately(530, 0.5f, "the second starts where the first ends");
    }

    /// <summary>
    /// The behaviour the whole change exists for: too narrow for both bases, so each pane takes a
    /// line — and then GROWS to the full width, rather than sitting at its basis with a gap beside
    /// it. This is the second pass; line-breaking alone would leave them at 440.
    /// </summary>
    [Fact]
    public void TooNarrowForBothBases_EachTakesAFullLine()
    {
        var row = new Row(gap: 0) { Wrap = true, Width = SizeValue.Fill };
        row.Add(new Flexible(Pane(), flex: 1, basis: 440));
        row.Add(new Flexible(Pane(), flex: 1, basis: 380));

        var node = Layout(row, viewportW: 700);

        node.Children[1].Bounds.Y.Should().BeGreaterThan(node.Children[0].Bounds.Y, "the second wrapped");
        node.Children[0].Bounds.X.Should().Be(0);
        node.Children[1].Bounds.X.Should().Be(0, "a new line restarts at the main origin");
        node.Children[0].Bounds.Width.Should().BeApproximately(700, 0.5f, "alone on its line, it grows to fill it");
        node.Children[1].Bounds.Width.Should().BeApproximately(700, 0.5f);
    }

    /// <summary>Weights decide the SHARE of the leftover, not the sizes themselves.</summary>
    [Fact]
    public void LeftoverIsSharedByWeight_NotEvenly()
    {
        var row = new Row(gap: 0) { Wrap = true, Width = SizeValue.Fill };
        row.Add(new Flexible(Pane(), flex: 3, basis: 100));
        row.Add(new Flexible(Pane(), flex: 1, basis: 100));

        var node = Layout(row, viewportW: 600);

        // 400 of leftover: three parts to the first, one to the second.
        node.Children[0].Bounds.Width.Should().BeApproximately(400, 0.5f);
        node.Children[1].Bounds.Width.Should().BeApproximately(200, 0.5f);
    }

    /// <summary>A line that overflows takes space back from the shrinkers, weighted by basis as CSS
    /// scales it — and a shrink of 0 keeps every pixel it asked for.</summary>
    [Fact]
    public void ShrinkZero_KeepsItsBasisWhileTheOtherGivesWay()
    {
        // One line: nothing wraps because the second refuses to be the one that breaks — both are
        // measured together against 500 with a combined basis of 600.
        var row = new Row(gap: 0) { Wrap = true, Width = SizeValue.Fill };
        row.Add(new Flexible(Pane(), flex: 1, basis: 300, shrink: 0));
        row.Add(new Flexible(Pane(), flex: 1, basis: 300, shrink: 0));

        var node = Layout(row, viewportW: 500);

        node.Children[1].Bounds.Y.Should().BeGreaterThan(node.Children[0].Bounds.Y,
            "they cannot both fit, so the second wraps rather than being squeezed");
        node.Children[0].Bounds.Width.Should().BeApproximately(500, 0.5f);
    }

    /// <summary>
    /// A basis of zero is the historical shape and must stay exactly that: the child contributes
    /// nothing of its own, which is what every layout built before this change was measured against.
    /// </summary>
    [Fact]
    public void WithoutABasis_NothingChanges()
    {
        var row = new Row(gap: 8) { Wrap = true, Width = SizeValue.Fill };
        row.Add(new Box(new BoxStyle { Width = 90, Height = 24 }));
        row.Add(new Box(new BoxStyle { Width = 90, Height = 24 }));
        row.Add(new Box(new BoxStyle { Width = 90, Height = 24 }));

        var node = Layout(row, viewportW: 200, viewportH: 400);

        node.Children[0].Bounds.Y.Should().Be(node.Children[1].Bounds.Y);
        node.Children[2].Bounds.Y.Should().BeGreaterThan(node.Children[0].Bounds.Y);
        node.Children[2].Bounds.X.Should().Be(0);
    }

    /// <summary>
    /// A scroller on a wrapping line scrolls as far as the viewport it is drawn at. In a wrapping row
    /// of 300, Chrome 154 caps <c>Flexible(ScrollView { Width = 400 }, flex: 1)</c> around 800 of
    /// content at 300, with a basis of 300 or of 200, and scrolls it by 500 either way. Photon capped
    /// it when it measured it again, and kept the range its first measure had taken at 400.
    /// </summary>
    [Theory]
    [InlineData(300f)] // the line holds it still
    [InlineData(200f)] // the line grows it
    public void AScrollerOnAWrappingLine_ScrollsAsFarAsItsViewportNeeds(float basis)
    {
        var scroller = new ScrollView(new Box(new BoxStyle { Width = 800, Height = 20 }), ScrollAxis.Horizontal)
        {
            Width = 400,
            Height = 20,
        };
        var row = new Row(gap: 0) { Wrap = true, Width = 300 };
        row.Add(new Flexible(scroller, flex: 1, basis: basis));
        var ranges = new Dictionary<ScrollView, (string Path, float MaxOffset)>();

        var item = LayoutEngine.Layout(row, 300, 800,
            new LayoutContext(PhotonTheme.Instance, new ApproximateTextMeasurer()) { ScrollMeta = ranges }).Children[0];

        item.Children[0].Bounds.Width.Should().BeApproximately(300, 0.01f, "the scroller is capped at its item");
        ranges[scroller].MaxOffset.Should().BeApproximately(500, 0.01f, "800 of content in a 300 viewport");
    }
}
