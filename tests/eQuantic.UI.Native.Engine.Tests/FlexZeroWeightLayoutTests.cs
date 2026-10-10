using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A <see cref="Flexible"/> of weight ZERO takes no share of the leftover (#680): it keeps its
/// basis, or its own size when it has none. That is Flutter's "inflexible and determines its own
/// size" and CSS's <c>flex-grow: 0</c>, and every expected number here is the one a browser gives
/// for the CSS the web realizer writes for the same tree (measured in Chrome 154).
/// <para>
/// The constructor used to raise a zero weight to 1, so "start at 540 and never grow" grew. And an
/// object initializer that got a zero past it found a second defect: the single-line pass deferred
/// the child with the weighted ones and then skipped it for having no weight, so it was never laid
/// out at all.
/// </para>
/// </summary>
public class FlexZeroWeightLayoutTests
{
    private static readonly LayoutContext Ctx =
        new(PhotonTheme.Instance, ApproximateTextMeasurer.Instance);

    /// <summary>A pane that fills whatever main extent its item is given.</summary>
    private static Box Pane() => new(new BoxStyle { Width = SizeValue.Fill, Height = 20 });

    private static Box FixedBox(float width) => new(new BoxStyle { Width = width, Height = 20 });

    private static LayoutNode Layout(Row row, float viewportW = 2000) =>
        LayoutEngine.Layout(row, viewportW, 800, Ctx);

    private static Row Line(float width, params VisualNode[] children)
    {
        var row = new Row(gap: 0) { Width = width };
        foreach (var child in children) row.Add(child);
        return row;
    }

    /// <summary>The case the issue measured, in one line: the zero weight stays at 540 and the
    /// weight of one takes all 900 of the free space (CSS: <c>flex: 0 1 540px</c> beside
    /// <c>flex: 1 1 0%</c>).</summary>
    [Fact]
    public void AZeroWeight_KeepsItsBasis_WhileAWeightOfOneTakesTheFreeSpace()
    {
        var node = Layout(Line(1440, new Flexible(Pane(), flex: 0, basis: 540), new Flexible(Pane(), flex: 1)));

        node.Children.Should().HaveCount(2, "a zero weight is laid out, not dropped");
        node.Children[0].Bounds.Width.Should().BeApproximately(540, 0.5f, "a zero weight never grows past its basis");
        node.Children[1].Bounds.X.Should().BeApproximately(540, 0.5f);
        node.Children[1].Bounds.Width.Should().BeApproximately(900, 0.5f, "the weight of one takes all the free space");
    }

    /// <summary>Without a basis a zero weight starts from its content (CSS <c>flex: 0 1 auto</c>):
    /// a 200 box stays 200, where the clamp gave it half the row.</summary>
    [Fact]
    public void AZeroWeight_WithoutABasis_IsSizedByItsContent()
    {
        var node = Layout(Line(1440, new Flexible(FixedBox(200), flex: 0), new Flexible(Pane(), flex: 1)));

        node.Children[0].Bounds.Width.Should().BeApproximately(200, 0.5f);
        node.Children[1].Bounds.X.Should().BeApproximately(200, 0.5f);
        node.Children[1].Bounds.Width.Should().BeApproximately(1240, 0.5f);
    }

    /// <summary>
    /// Its content decides its size, so a Fill inside it has nothing to fill: the item is as wide as
    /// what it holds, zero for an empty pane, which is the browser's answer for a <c>width: 100%</c>
    /// inside a <c>flex-basis: auto</c> item. Flutter refuses the same tree outright, because it lays
    /// an inflexible child out unbounded on the main axis.
    /// </summary>
    [Fact]
    public void AZeroWeight_WithoutABasis_GivesAFillChildNothingToFill()
    {
        var node = Layout(Line(1440, new Flexible(Pane(), flex: 0), new Flexible(Pane(), flex: 1)));

        node.Children[0].Bounds.Width.Should().BeApproximately(0, 0.5f);
        node.Children[1].Bounds.Width.Should().BeApproximately(1440, 0.5f);
    }

    /// <summary>
    /// Cura's page, which reported the defect: a picture that starts at 540 and never grows, beside
    /// text that starts at 380 and takes the rest, in a wrapping row of 1154. The clamp gave the
    /// picture 657 and squeezed the text to 497; a browser and this engine now agree on 540 and 614.
    /// </summary>
    [Fact]
    public void InAWrappingRow_AZeroWeight_KeepsItsBasis_AndItsNeighbourTakesTheRest()
    {
        var row = new Row(gap: 0) { Wrap = true, Width = 1154 };
        row.Add(new Flexible(Pane(), flex: 0, basis: 540));
        row.Add(new Flexible(Pane(), flex: 1, basis: 380));

        var node = Layout(row);

        node.Children[0].Bounds.Y.Should().Be(node.Children[1].Bounds.Y, "both fit on one line");
        node.Children[0].Bounds.Width.Should().BeApproximately(540, 0.5f);
        node.Children[1].Bounds.X.Should().BeApproximately(540, 0.5f);
        node.Children[1].Bounds.Width.Should().BeApproximately(614, 0.5f);
    }

    /// <summary>A zero weight's basis is still a size an overflowing line can take back, by its own
    /// shrink (CSS <c>flex: 0 1 540px</c> after a rigid 100 in 400 is 300), and <c>shrink: 0</c>
    /// keeps every pixel of it (CSS <c>flex: 0 0 540px</c> in 400 stays 540).</summary>
    [Fact]
    public void AZeroWeight_GivesSpaceBackByItsShrink_AndShrinkZeroKeepsItsBasis()
    {
        var shrinking = Layout(Line(400, FixedBox(100), new Flexible(Pane(), flex: 0, basis: 540, shrink: 1)));
        shrinking.Children[0].Bounds.Width.Should().BeApproximately(100, 0.5f, "a fixed size never shrinks");
        shrinking.Children[1].Bounds.Width.Should().BeApproximately(300, 0.5f);

        var pinned = Layout(Line(400, new Flexible(Pane(), flex: 0, basis: 540, shrink: 0)));
        pinned.Children[0].Bounds.Width.Should().BeApproximately(540, 0.5f, "shrink 0 refuses to give space back");
    }

    /// <summary>
    /// A zero weight's floor is zero, not its child's min-content: the web writes the item
    /// <c>min-width: 0</c>, so a child wider than the item lets it shrink, and overflows it. A rigid
    /// 100 and a zero weight around a 400-wide box, in 400, are 100 and 300 in Chrome 154, at a basis
    /// of 540 and without one, and the box inside stays 400 from x 100, past the item's end. Photon
    /// stopped the item at its child's 400, and the line ran 100 past its row; then, shrinking it,
    /// it painted the box at 300. Around a text the item was already 300, cut as text, and stays
    /// 300.
    /// </summary>
    [Fact]
    public void AZeroWeight_ShrinksPastItsChildsMinContent_AsTheWebsMinWidthZeroLetsIt()
    {
        var items = new (string Name, Flexible Item, float? Child)[]
        {
            ("a box at a basis", new Flexible(FixedBox(400), flex: 0, basis: 540), 400),
            ("a box without a basis", new Flexible(FixedBox(400), flex: 0), 400),
            ("a 320 camera preview at a basis", new Flexible(new CameraPreview(null, 320, 240), flex: 0, basis: 540), 320),
            ("a word at a basis", new Flexible(new Text(new string('a', 48), TypeRole.BodyM), flex: 0, basis: 540), null),
        };

        foreach (var (name, item, child) in items)
        {
            var node = Layout(Line(400, FixedBox(100), item));

            node.Children[0].Bounds.Width.Should().BeApproximately(100, 0.5f, $"{name}: a fixed size never shrinks");
            node.Children[1].Bounds.Width.Should().BeApproximately(300, 0.5f, name);
            (node.Children[1].Bounds.X + node.Children[1].Bounds.Width).Should()
                .BeApproximately(400, 0.5f, $"{name}: the line fits its row");
            if (child is { } width)
            {
                var inside = node.Children[1].Children[0];
                inside.Bounds.X.Should().BeApproximately(100, 0.5f, $"{name}: the child starts where the item does");
                inside.Bounds.Width.Should().Be(width, $"{name}: a fixed child keeps its width and overflows the item");
            }
        }
    }

    /// <summary>
    /// A zero weight is asked to give in proportion to its shrink times its size, as CSS scales a
    /// shrink factor. Two at a basis of 540, <c>shrink: 3</c> and <c>shrink: 1</c>, in 1000, are 480
    /// and 520 in Chrome 154, where Photon split the 80 evenly. And <c>shrink: 3</c> beside a box that
    /// fills the row, whose own floor is zero, is 206.11 beside 793.89, where Photon gave 473.68 and
    /// 526.32.
    /// </summary>
    [Fact]
    public void AZeroWeight_GivesSpaceBackByItsShrinkTimesItsSize()
    {
        var factors = Layout(Line(1000, new Flexible(Pane(), flex: 0, basis: 540, shrink: 3),
            new Flexible(Pane(), flex: 0, basis: 540, shrink: 1)));
        factors.Children[0].Bounds.Width.Should().BeApproximately(480, 0.5f, "shrink 3 gives 60 of the 80");
        factors.Children[1].Bounds.Width.Should().BeApproximately(520, 0.5f, "shrink 1 gives 20");

        var besideAFill = Layout(Line(1000, new Flexible(FixedBox(400), flex: 0, basis: 540, shrink: 3),
            new Box(new BoxStyle { Width = SizeValue.Fill })));
        besideAFill.Children[0].Bounds.Width.Should().BeApproximately(206.11f, 0.5f);
        besideAFill.Children[1].Bounds.Width.Should().BeApproximately(793.89f, 0.5f);
    }

    /// <summary>
    /// What a zero weight holds does not change how it shrinks: one holding text gives space back
    /// together with the others, by its shrink, where Photon used to cut it first, as text. A text
    /// and a box at a basis of 300 each, in 400, are 200 and 200 in Chrome 154 (Photon gave 100 and
    /// 300). Two texts at 300 and 200, <c>shrink: 3</c> and <c>shrink: 1</c>, in 300, are 136.37 and
    /// 163.63 (Photon gave 180 and 120).
    /// </summary>
    [Fact]
    public void AZeroWeight_HoldingText_ShrinksWithTheOthers_ByItsShrink()
    {
        var mixed = Layout(Line(400, new Flexible(new Text("aaaa", TypeRole.BodyM), flex: 0, basis: 300),
            new Flexible(FixedBox(300), flex: 0, basis: 300)));
        mixed.Children[0].Bounds.Width.Should().BeApproximately(200, 0.5f);
        mixed.Children[1].Bounds.Width.Should().BeApproximately(200, 0.5f);

        var texts = Layout(Line(300,
            new Flexible(new Text("aaaa", TypeRole.BodyM), flex: 0, basis: 300, shrink: 3),
            new Flexible(new Text("bbbb", TypeRole.BodyM), flex: 0, basis: 200, shrink: 1)));
        texts.Children[0].Bounds.Width.Should().BeApproximately(136.36f, 0.5f);
        texts.Children[1].Bounds.Width.Should().BeApproximately(163.64f, 0.5f);
    }

    /// <summary>A weight declares the intent to fill, which is why a hugging row that holds one takes
    /// the available extent. A zero weight declares no such intent, so the row keeps hugging.</summary>
    [Fact]
    public void AZeroWeight_LeavesAHuggingRowHuggingItsContent()
    {
        var row = new Row(gap: 0) { FixedBox(40), new Flexible(FixedBox(60), flex: 0) };

        var node = Layout(row, viewportW: 300);

        node.Bounds.Width.Should().Be(100);
        node.Children[1].Bounds.Width.Should().Be(60);
    }
}
