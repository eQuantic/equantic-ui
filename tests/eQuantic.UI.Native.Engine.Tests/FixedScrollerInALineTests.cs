using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A size the app fixed on a line's axis is not squeezed, on either target, and a scroller is no
/// exception to it. The web left a fixed scroller out of <c>flex-shrink: 0</c>, so a browser shrank it
/// and Photon did not, and Photon asked it for room it could not give (#1186). Its width stays a
/// ceiling, the web's <c>max-width: 100%</c>, wherever it stands in a line. Every number is Chrome
/// 152's for the web realizer's own HTML.
/// </summary>
public class FixedScrollerInALineTests
{
    private static readonly LayoutContext Ctx = new(PhotonTheme.Instance, ApproximateTextMeasurer.Instance);

    private static Box Sized(float width, float height) => new(new BoxStyle { Width = width, Height = height });

    private static LayoutNode Laid(VisualNode root) => LayoutEngine.Layout(root, 2000, 800, Ctx);

    private static Row Row400(VisualNode scroller)
    {
        var row = new Row(gap: 0) { Width = 400 };
        row.Add(Sized(100, 20));
        row.Add(scroller);
        return row;
    }

    [Fact]
    public void AFixedWidth_IsCappedByTheLine_AndGivesNothingBack()
    {
        var node = Laid(Row400(new ScrollView(Sized(800, 20), ScrollAxis.Horizontal) { Width = 540, Height = 20 }));

        node.Children[0].Bounds.Width.Should().Be(100, "a fixed box keeps its width");
        node.Children[1].Bounds.Width.Should().Be(400, "the line's width is the scroller's ceiling");
    }

    [Fact]
    public void AFixedWidthUnderTheLine_KeepsIt()
    {
        var node = Laid(Row400(new ScrollView(Sized(800, 20), ScrollAxis.Horizontal) { Width = 200, Height = 20 }));

        node.Children[1].Bounds.Width.Should().Be(200);
    }

    [Fact]
    public void AHuggingScroller_GivesWay()
    {
        var node = Laid(Row400(new ScrollView(Sized(800, 20), ScrollAxis.Horizontal) { Height = 20 }));

        node.Children[1].Bounds.Width.Should().Be(300);
    }

    [Fact]
    public void AFixedHeightInAColumn_KeepsItsHeight()
    {
        var column = new Column(gap: 0) { Width = 200, Height = 400 };
        column.Add(Sized(200, 100));
        column.Add(new ScrollView(Sized(200, 800)) { Height = 540 });

        var node = Laid(column);

        node.Children[1].Bounds.Y.Should().Be(100);
        node.Children[1].Bounds.Height.Should().Be(540);
    }

    [Fact]
    public void AFixedWidthInAColumn_IsCappedByTheColumn()
    {
        var column = new Column(gap: 0) { Width = 200, Height = 400 };
        column.Add(new ScrollView(Sized(800, 20), ScrollAxis.Horizontal) { Width = 540, Height = 20 });

        Laid(column).Children[0].Bounds.Width.Should().Be(200);
    }

    /// <summary>Fixed across a column is not fixed down it: the web writes `flex-shrink: 0` only on the
    /// line's axis, so a scroller 540 wide with no height of its own shrinks to what the column leaves
    /// it, 300 in Chrome 152, as a hugging one does.</summary>
    [Fact]
    public void AFixedWidthInAColumn_StillShrinksDownIt()
    {
        var column = new Column(gap: 0) { Width = 600, Height = 400 };
        column.Add(Sized(600, 100));
        column.Add(new ScrollView(Sized(540, 800)) { Width = 540 });

        var node = Laid(column);

        node.Children[1].Bounds.Height.Should().Be(300);
    }
}
