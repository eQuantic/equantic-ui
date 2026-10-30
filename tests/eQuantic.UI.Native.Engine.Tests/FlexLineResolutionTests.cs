using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A flex line resolves its items the way a browser resolves the CSS the web realizer writes for
/// the same tree: each item STARTS from its flex base size (a Flexible's basis, nothing for a
/// weighted one without a basis, a rigid child's own size), and only what is left over is shared,
/// or only what overflows is taken back. Every expected number is Chrome's, for the declarations
/// the web realizer writes (<c>flex: n s basis</c>, <c>min-width: 0</c> or <c>min-height: 0</c> on a
/// Flexible).
/// <para>
/// Photon resolved four parts of that differently. The single-line pass shared the whole extent by
/// weight and never started a weighted item from its basis (#693); the wrapping pass broke a
/// weighted item without a basis at its child's natural size, where CSS starts it from zero (#728);
/// a single-line column took nothing back from an overflow (#747); and a line that stretches asked a
/// hand-kept list whether a child's cross size was explicit, which missed five node types (#744).
/// </para>
/// </summary>
public class FlexLineResolutionTests
{
    private static readonly LayoutContext Ctx =
        new(PhotonTheme.Instance, ApproximateTextMeasurer.Instance);

    /// <summary>A pane that fills whatever main extent its item is given.</summary>
    private static Box Pane() => new(new BoxStyle { Width = SizeValue.Fill, Height = 20 });

    private static Box FixedBox(float width, float height = 20) =>
        new(new BoxStyle { Width = width, Height = height });

    private static Row Line(float width, params VisualNode[] children)
    {
        var row = new Row(gap: 0) { Width = width };
        foreach (var child in children) row.Add(child);
        return row;
    }

    private static LayoutNode Layout(VisualNode root, float viewportW = 2000, float viewportH = 800) =>
        LayoutEngine.Layout(root, viewportW, viewportH, Ctx);

    // ---- #693: a weighted item starts from its basis ----------------------------------------------

    /// <summary>The issue's row: <c>flex: 1 1 540px</c> beside <c>flex: 1 1 0%</c> in 1440. Each grows
    /// from its start, so the first keeps its 540 more: 990 | 450 in Chrome, where Photon shared the
    /// whole row, 720 | 720.</summary>
    [Fact]
    public void AWeightedItem_GrowsFromItsBasis()
    {
        var node = Layout(Line(1440, new Flexible(Pane(), flex: 1, basis: 540), new Flexible(Pane(), flex: 1)));

        node.Children[0].Bounds.Width.Should().BeApproximately(990, 0.5f, "540 of its own and half of the 900 left over");
        node.Children[1].Bounds.X.Should().BeApproximately(990, 0.5f);
        node.Children[1].Bounds.Width.Should().BeApproximately(450, 0.5f, "half of the 900 left over");
    }

    /// <summary>Weights still share what is left over by weight: <c>flex: 2 1 100px</c> beside
    /// <c>flex: 1 1 200px</c> and a fixed 100 in 1000 leave 600, so 100 + 400 and 200 + 200: 500 |
    /// 400 | 100 in Chrome. The bases are against the weights on purpose: in the same ratio, sharing
    /// the whole row by weight gives the same numbers and the case could not fail.</summary>
    [Fact]
    public void WeightedItems_ShareTheLeftover_ByWeight_OnTopOfTheirBases()
    {
        var node = Layout(Line(1000,
            new Flexible(Pane(), flex: 2, basis: 100), new Flexible(Pane(), flex: 1, basis: 200), FixedBox(100)));

        node.Children[0].Bounds.Width.Should().BeApproximately(500, 0.5f);
        node.Children[1].Bounds.X.Should().BeApproximately(500, 0.5f);
        node.Children[1].Bounds.Width.Should().BeApproximately(400, 0.5f);
        node.Children[2].Bounds.X.Should().BeApproximately(900, 0.5f);
    }

    /// <summary>A basis is a size an overflowing line takes back by the item's shrink, whatever its
    /// weight: <c>flex: 1 1 540px</c> after a fixed 100 in a row of 400 is 300 in Chrome, and
    /// <c>flex: 1 0 540px</c> keeps every pixel of it.</summary>
    [Fact]
    public void AWeightedItem_GivesItsBasisBack_ByItsShrink()
    {
        var shrinking = Layout(Line(400, FixedBox(100), new Flexible(Pane(), flex: 1, basis: 540)));
        shrinking.Children[0].Bounds.Width.Should().BeApproximately(100, 0.5f, "a fixed size never shrinks");
        shrinking.Children[1].Bounds.Width.Should().BeApproximately(300, 0.5f);

        var pinned = Layout(Line(400, new Flexible(Pane(), flex: 1, basis: 540, shrink: 0)));
        pinned.Children[0].Bounds.Width.Should().BeApproximately(540, 0.5f, "shrink 0 refuses to give space back");
    }

    /// <summary>
    /// A weight ANIMATED to zero at a basis of 540 (<c>AnimateChanges</c>) glides down to 540 and
    /// lands there, as the browser transitions <c>flex-grow</c> with the basis held. It used to glide
    /// toward a share of nothing and snap to 540 on the last frame: the start followed the resolved
    /// weight instead of the declared one, and a lone weight under one took the whole leftover.
    /// </summary>
    [Fact]
    public void AWeightAnimatedToZero_GlidesToItsBasis_WithoutASnap()
    {
        var owner = BuildOwner.Structural();
        var clock = new TransitionClock();
        float Width(int flex, float timeMs)
        {
            var row = new Row(gap: 0) { Width = 1440, Height = 20 };
            row.Add(new Flexible(Pane(), flex: flex, basis: 540) { AnimateChanges = true });
            return PhotonRealizer.Realize(row, 1440, 100, PhotonTheme.Instance, ThemeMode.Light,
                new DisplayListBuilder(), timeMs: timeMs, owner: owner, transitions: clock).Root.Children[0].Bounds.Width;
        }

        Width(1, 0).Should().BeApproximately(1440, 0.5f, "open, it takes its basis and all the leftover");

        var widths = new List<float>();
        for (var t = 1000f; t <= 1200f; t += 10) widths.Add(Width(0, t));

        widths.Should().BeInDescendingOrder("closing glides down and never back up");
        widths[^1].Should().BeApproximately(540, 0.5f, "and lands on the basis");
        for (var i = 1; i < widths.Count; i++)
            (widths[i - 1] - widths[i]).Should().BeLessThan(300,
                $"no frame jumps (frame {i}: {widths[i - 1]} to {widths[i]})");
        widths[^2].Should().BeLessThan(600, "the frame before it is already close: nothing is left to snap");
    }

    // ---- #728: a wrapping line breaks a weighted item on its basis --------------------------------

    /// <summary>
    /// The issue's row, measured in Chrome 154: a wrapping row of 1000 holding two
    /// <c>Flexible(box 600 wide, flex: 1)</c> without a basis puts both on ONE line at 500 each,
    /// because CSS starts them from <c>0%</c>. Photon broke them at the boxes' 600 and gave each a
    /// line of 1000.
    /// </summary>
    [Fact]
    public void AWrappingRow_BreaksAWeightedItemWithoutABasis_OnNothing_AndSharesTheLine()
    {
        var row = new Row(gap: 0) { Wrap = true, Width = 1000 };
        row.Add(new Flexible(FixedBox(600), flex: 1));
        row.Add(new Flexible(FixedBox(600), flex: 1));

        var node = Layout(row);

        node.Children[0].Bounds.Should().Be(new Rect(0, 0, 500, 20));
        node.Children[1].Bounds.Should().Be(new Rect(500, 0, 500, 20));
    }

    /// <summary>The gap between two items that ask for nothing is still a gap: with a gap of 10 the
    /// two share 990, 495 each, and the second starts at 505 (Chrome).</summary>
    [Fact]
    public void AWrappingRow_KeepsTheGap_BetweenItemsThatAskForNothing()
    {
        var row = new Row(gap: 10) { Wrap = true, Width = 1000 };
        row.Add(new Flexible(FixedBox(600), flex: 1));
        row.Add(new Flexible(FixedBox(600), flex: 1));

        var node = Layout(row);

        node.Children[0].Bounds.Should().Be(new Rect(0, 0, 495, 20));
        node.Children[1].Bounds.Should().Be(new Rect(505, 0, 495, 20));
    }

    /// <summary>
    /// Starting from nothing needs a line to grow the item, and on an UNBOUNDED main axis no line
    /// resolves: a wrapping row inside a horizontal scroller gives its weighted Flexible its child's
    /// size, the content it contributes, as the single-line pass does. Broken on nothing there, the
    /// item kept nothing and its content vanished (found reviewing this change).
    /// </summary>
    [Fact]
    public void AWrappingRow_OnAnUnboundedAxis_GivesAWeightedItemItsContent()
    {
        var row = new Row(gap: 0) { Wrap = true };
        row.Add(new Flexible(FixedBox(600), flex: 1));

        var scroller = Layout(new ScrollView(row, ScrollAxis.Horizontal));

        scroller.Children[0].Children[0].Bounds.Width.Should().BeApproximately(600, 0.5f);
    }

    /// <summary>
    /// An item that starts from nothing was measured against the whole line, so it is measured again
    /// where the line leaves it, even when that is nothing: in a wrapping row of 100, after a fixed
    /// 100, a Flexible around text is 0 wide in Chrome, its text one word to a line, and the line takes
    /// that height. Left at its first measure, the item kept one line of text in no width and the row
    /// was too short for it. The text fits the row on one line on purpose: one that already wraps at
    /// 100 is as tall either way, and could not show it.
    /// </summary>
    [Fact]
    public void AWrappingRow_MeasuresAnItemThatStartsFromNothing_WhereTheLineLeavesIt()
    {
        var text = () => new Text("hi there", TypeRole.BodyM);
        var oneLine = Layout(text()).Bounds.Height;
        var row = new Row(gap: 0) { Wrap = true, Width = 100 };
        row.Add(FixedBox(100));
        row.Add(new Flexible(text(), flex: 1));

        var node = Layout(row);

        node.Children[1].Bounds.X.Should().BeApproximately(100, 0.5f, "both sit on one line");
        node.Children[1].Bounds.Width.Should().Be(0);
        node.Children[1].Children[0].Bounds.Height.Should().BeGreaterThan(oneLine, "its text wraps in the width it got");
        node.Bounds.Height.Should().Be(node.Children[1].Children[0].Bounds.Height, "and the line is as tall as what it holds");
    }

    // ---- #747: a single-line column takes its overflow back ---------------------------------------

    /// <summary>
    /// The issue's column, measured in Chrome 154: 400 tall, a box fixed at 100 and
    /// <c>Flexible(box, flex: 0, basis: 540)</c>. Chrome shrinks the zero weight to 300 (the web writes
    /// it <c>min-height: 0</c>) and the content ends at 400; Photon kept 540 and ran 240 past the end.
    /// </summary>
    [Fact]
    public void AColumn_ShrinksAFlexible_ToTakeItsOverflowBack()
    {
        var column = new Column(gap: 0) { Height = 400, Width = 200 };
        column.Add(FixedBox(200, 100));
        column.Add(new Flexible(new Box(new BoxStyle { Width = 200, Height = SizeValue.Fill }), flex: 0, basis: 540));

        var node = Layout(column);

        node.Children[0].Bounds.Height.Should().BeApproximately(100, 0.5f, "a fixed size never shrinks");
        node.Children[1].Bounds.Y.Should().BeApproximately(100, 0.5f);
        node.Children[1].Bounds.Height.Should().BeApproximately(300, 0.5f, "the overflow is taken back from the Flexible");
    }

    /// <summary>Only a Flexible gives height back in a column: an item's min-content height is the
    /// height it measured (<c>min-height: auto</c>), so a hugging box keeps its 100 and the zero
    /// weight beside it gives all 240 (Chrome).</summary>
    [Fact]
    public void AColumn_TakesNothingFromAnItemAtItsContentHeight()
    {
        var column = new Column(gap: 0) { Height = 400, Width = 200 };
        column.Add(new Box(new BoxStyle { Width = 200 }, FixedBox(200, 100)));
        column.Add(new Flexible(new Box(new BoxStyle { Width = 200, Height = SizeValue.Fill }), flex: 0, basis: 540));

        var node = Layout(column);

        node.Children[0].Bounds.Height.Should().BeApproximately(100, 0.5f);
        node.Children[1].Bounds.Height.Should().BeApproximately(300, 0.5f);
    }

    /// <summary>What fills a column (the web writes it <c>min-height: 0</c>), or a scroller, which
    /// takes the room it is given, shrinks together with a Flexible: 400 and 540 share an overflow
    /// of 540 by their sizes, 170.21 | 229.79 in Chrome 152. Keeping its 400 pushed the Flexible's
    /// whole basis out.</summary>
    public static TheoryData<string> ColumnItemsThatGiveHeightBack() =>
        new() { "Box", "Row", "ScrollView", "HuggingScrollView" };

    [Theory]
    [MemberData(nameof(ColumnItemsThatGiveHeightBack))]
    public void AColumn_SharesItsOverflow_WithWhatFillsIt(string type)
    {
        VisualNode first = type switch
        {
            "Box" => new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }),
            "Row" => new Row(gap: 0) { Width = SizeValue.Fill, Height = SizeValue.Fill },
            "ScrollView" => new ScrollView(FixedBox(200, 900)) { Height = SizeValue.Fill },
            _ => new ScrollView(FixedBox(200, 900)),
        };

        var node = Layout(ColumnOf(first));

        node.Children[0].Bounds.Height.Should().BeApproximately(170.21f, 0.01f);
        node.Children[1].Bounds.Y.Should().BeApproximately(170.21f, 0.01f);
        node.Children[1].Bounds.Height.Should().BeApproximately(229.79f, 0.01f);
    }

    /// <summary>The box's own minimum is the web's <c>min-height</c>, so it stops there and the
    /// Flexible gives the rest (300 | 100 in Chrome 152). A fixed WIDTH is the web's
    /// <c>flex-shrink: 0</c>, so that box gives nothing however it fills (400 | 0).</summary>
    [Fact]
    public void AColumn_StopsAtAFloorTheWebWrites_AndTakesNothingFromARigidItem()
    {
        var floored = Layout(ColumnOf(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill, MinHeight = 300 })));
        floored.Children[0].Bounds.Height.Should().BeApproximately(300, 0.01f);
        floored.Children[1].Bounds.Height.Should().BeApproximately(100, 0.01f);

        var rigid = Layout(ColumnOf(new Box(new BoxStyle { Width = 200, Height = SizeValue.Fill })));
        rigid.Children[0].Bounds.Height.Should().BeApproximately(400, 0.01f);
        rigid.Children[1].Bounds.Height.Should().BeApproximately(0, 0.01f);
    }

    /// <summary>A floor the item already stands on takes it out of the sharing: a box at a minimum of
    /// 400 gives nothing, and the Flexible beside it gives all of its 540 (400 | 0), where the sum of
    /// the weights still counted the box and left the Flexible 229.79.</summary>
    [Fact]
    public void AColumn_SharesNothingWithAnItemAlreadyAtItsFloor()
    {
        var node = Layout(ColumnOf(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill, MinHeight = 400 })));

        node.Children[0].Bounds.Height.Should().BeApproximately(400, 0.01f);
        node.Children[1].Bounds.Height.Should().BeApproximately(0, 0.01f);
    }

    /// <summary>A component is what it builds: a box that fills out of a component shares the
    /// overflow as a written one does, and one at a fixed width still gives nothing.</summary>
    [Fact]
    public void AColumn_SharesItsOverflow_WithWhatAComponentBuilds()
    {
        static Framework.LayoutNode Laid(VisualNode first) =>
            PhotonRealizer.Realize(ColumnOf(first), 2000, 800, PhotonTheme.Instance, ThemeMode.Light, new DisplayListBuilder()).Root;

        var filling = Laid(new Builds(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill })));
        filling.Children[0].Bounds.Height.Should().BeApproximately(170.21f, 0.01f);
        filling.Children[1].Bounds.Height.Should().BeApproximately(229.79f, 0.01f);

        var rigid = Laid(new Builds(new Box(new BoxStyle { Width = 200, Height = SizeValue.Fill })));
        rigid.Children[0].Bounds.Height.Should().BeApproximately(400, 0.01f);
        rigid.Children[1].Bounds.Height.Should().BeApproximately(0, 0.01f);
    }

    private static Column ColumnOf(VisualNode first)
    {
        var column = new Column(gap: 0) { Height = 400, Width = 200 };
        column.Add(first);
        column.Add(new Flexible(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }), flex: 0, basis: 540));
        return column;
    }

    // ---- #744: a line that stretches keeps an explicit cross size ---------------------------------

    /// <summary>Each node type that declares a height of its own and draws on Photon, at 20, in a row
    /// 100 tall that stretches its children. A browser keeps every one at 20 under
    /// <c>align-items: stretch</c> (measured in Chrome 152 on the web realizer's own HTML); Photon
    /// stretched the four after the Box to 100, where a Box kept 20.</summary>
    public static TheoryData<string> FixedHeightNodes() =>
        new() { "Box", "Stack", "Canvas", "CameraPreview", "ScrollView" };

    private static VisualNode FixedHeight(string type) => type switch
    {
        "Box" => FixedBox(40, 20),
        "Stack" => new Stack { Width = 40, Height = 20 },
        "Canvas" => new Canvas(_ => { }, width: 40, height: 20),
        "CameraPreview" => new CameraPreview(null, 40, 20),
        "ScrollView" => new ScrollView(FixedBox(40, 60)) { Width = 40, Height = 20 },
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    [Theory]
    [MemberData(nameof(FixedHeightNodes))]
    public void AStretchingRow_KeepsAnExplicitHeight(string type)
    {
        var row = new Row(gap: 0, cross: CrossAlign.Stretch) { Width = 400, Height = 100 };
        row.Add(FixedHeight(type));

        Layout(row, 400, 100).Children[0].Bounds.Height.Should().Be(20, $"a {type} 20 tall keeps its height");
    }

    /// <summary>The fifth type the issue named, a WebFrame, cannot cross to Photon and measures as a
    /// zero box there (<c>MeasureVisitor.Visit(WebFrame)</c>), so its explicit height is not drawn
    /// either. What it shares with the others is the answer a stretch reads: the row no longer
    /// stretches it to its 100.</summary>
    [Fact]
    public void AStretchingRow_DoesNotStretchAWebFrame_WhichMeasuresAsNothing()
    {
        var row = new Row(gap: 0, cross: CrossAlign.Stretch) { Width = 400, Height = 100 };
        row.Add(new WebFrame(WebContent.Document("<p>x</p>"), "frame") { Width = 40, Height = 20 });

        Layout(row, 400, 100).Children[0].Bounds.Height.Should().Be(0);
    }

    /// <summary>A component is what it builds, as the web lowers it: one that builds a box 20 tall
    /// keeps 20 in a row that stretches, where asking the component itself read nothing and
    /// stretched it to 100.</summary>
    [Fact]
    public void AStretchingRow_KeepsTheHeight_OfWhatAComponentBuilds()
    {
        var row = new Row(gap: 0, cross: CrossAlign.Stretch) { Width = 400, Height = 100 };
        row.Add(new Builds(FixedBox(40, 20)));

        var frame = PhotonRealizer.Realize(row, 400, 100, PhotonTheme.Instance, ThemeMode.Light, new DisplayListBuilder());

        frame.Root.Children[0].Bounds.Height.Should().Be(20);
    }

    /// <summary>A code surface is as tall as its code, with its notes open or not: they hang off it
    /// and take no part in its size, so a note opening does not let the row stretch it to 100.</summary>
    [Fact]
    public void AStretchingRow_KeepsTheHeightOfACodeSurfaceWithANoteOpen()
    {
        var row = new Row(gap: 0, cross: CrossAlign.Stretch) { Width = 400, Height = 100 };
        row.Add(new CodeSurface(FixedBox(40, 20), new CodeEditorController("code"))
        {
            Notes = [new Positioned(FixedBox(40, 60), top: 0, start: 0)],
        });

        var frame = PhotonRealizer.Realize(row, 400, 100, PhotonTheme.Instance, ThemeMode.Light, new DisplayListBuilder());

        frame.Root.Children[0].Bounds.Height.Should().Be(20);
    }

    /// <summary>A window-relative height is as explicit as a fixed one (the web writes it as a
    /// <c>calc</c> of the viewport), so a row that stretches keeps it: 800 less 750 is 50, not the
    /// row's 100.</summary>
    [Fact]
    public void AStretchingRow_KeepsAWindowRelativeHeight()
    {
        var row = new Row(gap: 0, cross: CrossAlign.Stretch) { Width = 400, Height = 100 };
        row.Add(new Box(new BoxStyle { Width = 40, Height = SizeValue.WindowMinus(750) }));

        Layout(row, 400, 800).Children[0].Bounds.Height.Should().Be(50);
    }

    /// <summary>The same reading on the main axis: a Flexible whose child is a component that builds
    /// a box 400 wide keeps the box at 400 in a share of 300, as the web keeps it inside the flex
    /// item, where asking the component read nothing and pinned it to the share.</summary>
    [Fact]
    public void AFlexibleSlot_KeepsTheSizeOfWhatAComponentBuilds()
    {
        var row = new Row(gap: 0) { Width = 600, Height = 20 };
        row.Add(new Flexible(new Builds(FixedBox(400)), flex: 1));
        row.Add(new Flexible(Pane(), flex: 1));

        var item = PhotonRealizer.Realize(row, 600, 100, PhotonTheme.Instance, ThemeMode.Light, new DisplayListBuilder())
            .Root.Children[0];

        item.Bounds.Width.Should().BeApproximately(300, 0.5f, "the item is its share");
        item.Children[0].Bounds.Width.Should().Be(400, "and what the component built keeps its own width");
    }

    private sealed class Builds(VisualNode built) : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => built;
    }
}
