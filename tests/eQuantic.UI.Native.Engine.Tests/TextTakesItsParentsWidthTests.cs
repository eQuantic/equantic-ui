using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A <c>Text</c> takes the width its parent decides (#659). Photon measured a Text at its own width
/// in every parent: a Box's block stretch reached a Box, a Row and a Column and never a Text, and a
/// stretching Row or Column stretched every child but a Text. So a centred line in a sized Box, or in
/// a Column that stretches its children, sat at the start, centred inside a box as wide as itself.
/// <para>
/// Flutter gives a Text the width a tight constraint hands it, and its <c>textAlign</c> reads across
/// that width. The web does the same since a Text lowers as a block (#495): a block takes the width
/// of a block parent, and a flex item is stretched on the cross axis. The HEIGHT stays the text's own
/// in a Box, as a block's height is its lines, and a button still hugs its label, because a block
/// stretch stops at an inline-block.
/// </para>
/// </summary>
public class TextTakesItsParentsWidthTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static readonly LayoutContext Ctx = new(Theme, ApproximateTextMeasurer.Instance);

    private static LayoutNode Layout(VisualNode root) => LayoutEngine.Layout(root, 400, 300, Ctx);

    /// <summary>The placeholder bar of a one-line text: the path a frame with no text service takes,
    /// which places a line exactly where the rasterizer would.</summary>
    private static Rect Bar(VisualNode root)
    {
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(root, 400, 300, Theme, ThemeMode.Light, builder);
        return builder.Build().Commands.ToArray()
            .Single(c => c.Kind == DrawCommandKind.FillRRect).Shape.Rect;
    }

    private static Text Centred(string content = "short") =>
        new(content, TypeRole.BodyM, align: TextAlignment.Center);

    /// <summary>The first node of the laid-out tree that is a Text.</summary>
    private static LayoutNode TextNode(LayoutNode node) =>
        FindText(node) ?? throw new InvalidOperationException("no Text was laid out");

    private static LayoutNode? FindText(LayoutNode node) =>
        node.Source is Text ? node : node.Children.Select(FindText).FirstOrDefault(found => found is not null);

    [Fact]
    public void ACentredLineInASizedBox_IsCentredAcrossTheBox()
    {
        var box = new Box(new BoxStyle { Width = 300 }, Centred());

        TextNode(Layout(box)).Bounds.Width.Should().Be(300, "a block takes the width of a block parent");
        var bar = Bar(box);
        bar.X.Should().BeApproximately((300 - bar.Width) / 2, 0.01f);
    }

    [Fact]
    public void ACentredLineInAStretchingColumn_IsCentredAcrossTheColumn()
    {
        var column = new Column(gap: 0) { Width = 380, Cross = CrossAlign.Stretch };
        column.Add(Centred());

        TextNode(Layout(column)).Bounds.Width.Should().Be(380, "a stretched flex item takes the line's cross size");
        var bar = Bar(column);
        bar.X.Should().BeApproximately((380 - bar.Width) / 2, 0.01f);
    }

    [Fact]
    public void ALineInAFlexibleOfAStretchingColumn_IsCentredAcrossTheColumn()
    {
        var column = new Column(gap: 0) { Width = 380, Height = 200, Cross = CrossAlign.Stretch };
        column.Add(new Flexible(Centred()));

        var bar = Bar(column);
        bar.X.Should().BeApproximately((380 - bar.Width) / 2, 0.01f,
            "the Flexible is stretched, and the Text inside it takes what it was given");
    }

    /// <summary>The fence: a block stretch stops at an inline-block, so a button in a sized box hugs
    /// its label, as a <c>button</c> does in a <c>div</c>.</summary>
    [Fact]
    public void AButtonsLabelInASizedBox_StillHugs()
    {
        var box = new Box(new BoxStyle { Width = 300 }, new Pressable(Centred(), () => { }));

        TextNode(Layout(box)).Bounds.Width.Should().BeLessThan(100);
        Bar(box).X.Should().BeApproximately(0, 0.01f);
    }

    /// <summary>A Box hands its decided height to a container and never to a Text: a block's height is
    /// its lines, whatever the box around it.</summary>
    [Fact]
    public void InABoxWithAHeight_TheTextKeepsTheHeightOfItsLines()
    {
        var box = new Box(new BoxStyle { Width = 300, Height = 120 }, Centred());

        TextNode(Layout(box)).Bounds.Height.Should().BeLessThan(40);
    }

    /// <summary>A stretching Row stretches a Text's height as CSS stretches a flex item's, and its line
    /// stays at the top of the box, where a block's first line is.</summary>
    [Fact]
    public void InAStretchingRow_TheTextTakesTheRowsHeight_AndItsLineStaysAtTheTop()
    {
        var row = new Row(gap: 0) { Height = 60, Cross = CrossAlign.Stretch };
        row.Add(Centred());

        TextNode(Layout(row)).Bounds.Height.Should().Be(60);
        Bar(row).Y.Should().BeLessThan(10);
    }

    /// <summary>The wrapping pass stretches a Text to its line's cross size like any other child.</summary>
    [Fact]
    public void InAWrappingRow_TheTextTakesItsLinesHeight()
    {
        var row = new Row(gap: 0) { Wrap = true, Width = SizeValue.Fill, Cross = CrossAlign.Stretch };
        row.Add(new Box(new BoxStyle { Width = 40, Height = 48 }));
        row.Add(Centred());

        TextNode(Layout(row)).Bounds.Height.Should().Be(48);
    }
}
