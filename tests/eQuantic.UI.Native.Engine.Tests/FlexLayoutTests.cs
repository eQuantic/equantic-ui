using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Geometry tests for the C# flex layout engine (spec A1/A2/A4) — the seed of the cross-target layout
/// conformance suite (the same trees will later be compared against the web realizer's CSS layout).
/// </summary>
public class FlexLayoutTests
{
    private static readonly LayoutContext Ctx =
        new(PhotonTheme.Instance, ApproximateTextMeasurer.Instance);

    private static LayoutNode Layout(VisualNode root, float w = 400, float h = 300) =>
        LayoutEngine.Layout(root, w, h, Ctx);

    private static Box FixedBox(float w, float h) =>
        new(new BoxStyle { Width = w, Height = h });

    [Fact]
    public void Row_Hug_SumsChildrenAndGaps()
    {
        var row = new Row(gap: 8) { FixedBox(40, 20), FixedBox(30, 25) };
        var node = Layout(row);

        node.Bounds.Width.Should().Be(40 + 8 + 30);
        node.Bounds.Height.Should().Be(25, "hug cross = tallest child");
        node.Children[0].Bounds.X.Should().Be(0);
        node.Children[1].Bounds.X.Should().Be(48);
    }

    [Fact]
    public void Row_CrossCenter_IsTheDefault()
    {
        var row = new Row(gap: 0) { FixedBox(40, 20), FixedBox(30, 40) };
        var node = Layout(row);
        node.Children[0].Bounds.Y.Should().Be(10, "20-tall child centers in the 40 cross extent");
    }

    [Fact]
    public void Row_Flexible_SharesLeftoverByWeight()
    {
        var row = new Row(gap: 8) { Width = SizeValue.Fixed(200) };
        row.Add(FixedBox(40, 20));
        row.Add(new Flexible(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }), flex: 1));
        row.Add(new Flexible(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }), flex: 2));
        var node = Layout(row);

        // leftover = 200 − 40 − 2×8 = 144 → 48 / 96.
        node.Children[1].Bounds.Width.Should().BeApproximately(48, 0.01f);
        node.Children[2].Bounds.Width.Should().BeApproximately(96, 0.01f);
        node.Children[2].Bounds.X.Should().BeApproximately(40 + 8 + 48 + 8, 0.01f);
        node.Bounds.Width.Should().Be(200);
    }

    [Fact]
    public void Row_SpaceBetween_DistributesFreeSpace()
    {
        var row = new Row(gap: 0) { Width = SizeValue.Fixed(200), Main = MainAlign.SpaceBetween };
        row.Add(FixedBox(40, 20));
        row.Add(FixedBox(40, 20));
        row.Add(FixedBox(40, 20));
        var node = Layout(row);

        // free = 200 − 120 = 80 → 40 between each pair.
        node.Children[0].Bounds.X.Should().Be(0);
        node.Children[1].Bounds.X.Should().Be(80);
        node.Children[2].Bounds.X.Should().Be(160);
    }

    [Fact]
    public void Row_Spacer_PushesSiblingsApart()
    {
        var row = new Row(gap: 0) { Width = SizeValue.Fixed(200) };
        row.Add(FixedBox(40, 20));
        row.Add(new Spacer());
        row.Add(FixedBox(40, 20));
        var node = Layout(row);

        node.Children[^1].Bounds.X.Should().Be(160, "spacer absorbs the 120 leftover");
    }

    [Fact]
    public void Spacer_Fixed_IsRigid()
    {
        var row = new Row(gap: 0) { FixedBox(40, 20), Spacer.Fixed(24), FixedBox(40, 20) };
        var node = Layout(row);
        node.Bounds.Width.Should().Be(104);
        node.Children[^1].Bounds.X.Should().Be(64);
    }

    [Fact]
    public void Spacer_CollapsesWhenUnbounded()
    {
        // In genuinely unbounded space (e.g. horizontal scroll content) a flexible spacer has no
        // leftover to take — it collapses to 0 ("loses to content", spec A4).
        var row = new Row(gap: 0) { FixedBox(40, 20), new Spacer(), FixedBox(40, 20) };
        var node = Layout(row, w: float.PositiveInfinity);
        node.Bounds.Width.Should().Be(80);
    }

    [Fact]
    public void HugRowWithFlexible_TakesTheAvailableExtent()
    {
        // CSS parity: flexible children declare the intent to fill — a Hug row holding one, in finite
        // space, distributes over the available extent (a stretched row behaves this way on web).
        var row = new Row(gap: 0) { FixedBox(40, 20), new Flexible(new Box(new BoxStyle { Height = 20 })) };
        var node = Layout(row, w: 300);
        node.Bounds.Width.Should().Be(300);
        node.Children[1].Bounds.Width.Should().Be(260);
    }

    [Fact]
    public void Column_DefaultsToStretch()
    {
        var column = new Column(gap: 0) { Width = SizeValue.Fixed(120) };
        column.Add(new Box(new BoxStyle { Height = 30 }));
        var node = Layout(column);

        node.Children[0].Bounds.Width.Should().Be(120, "Column cross default = Stretch (spec A2)");
    }

    [Fact]
    public void MainAlign_CenterAndEnd_OffsetContent()
    {
        var center = new Row(gap: 0) { Width = SizeValue.Fixed(100), Main = MainAlign.Center };
        center.Add(FixedBox(40, 10));
        Layout(center).Children[0].Bounds.X.Should().Be(30);

        var end = new Row(gap: 0) { Width = SizeValue.Fixed(100), Main = MainAlign.End };
        end.Add(FixedBox(40, 10));
        Layout(end).Children[0].Bounds.X.Should().Be(60);
    }

    [Fact]
    public void Box_Padding_WrapsChild()
    {
        var box = new Box(new BoxStyle { Padding = EdgeInsets.All(10) }, FixedBox(30, 20));
        var node = Layout(box);

        node.Bounds.Width.Should().Be(50);
        node.Bounds.Height.Should().Be(40);
        node.Children[0].Bounds.X.Should().Be(10);
        node.Children[0].Bounds.Y.Should().Be(10);
    }

    [Fact]
    public void Box_SizeResolution_ExplicitOverFillOverHug()
    {
        Layout(new Box(new BoxStyle { Width = 77 })).Bounds.Width.Should().Be(77);
        Layout(new Box(new BoxStyle { Width = SizeValue.Fill })).Bounds.Width.Should().Be(400);
        Layout(new Box(new BoxStyle(), FixedBox(30, 10))).Bounds.Width.Should().Be(30);
    }

    [Fact]
    public void Box_MinMax_Clamp()
    {
        Layout(new Box(new BoxStyle { Width = 10, MinWidth = 64 })).Bounds.Width.Should().Be(64);
        Layout(new Box(new BoxStyle { Width = SizeValue.Fill, MaxWidth = 240 })).Bounds.Width.Should().Be(240);
    }

    [Fact]
    public void Text_Wraps_AtAvailableWidth()
    {
        var text = new Text("alpha beta gamma delta epsilon zeta eta theta iota kappa", TypeRole.BodyL);
        var node = Layout(text, w: 140);

        node.Text!.Lines.Count.Should().BeGreaterThan(1, "140dp can't hold the whole sentence");
        node.Bounds.Width.Should().BeLessThanOrEqualTo(140);
        node.Bounds.Height.Should().Be(node.Text.Lines.Count * 24, "BodyL line height is 24");
    }

    [Fact]
    public void Text_MaxLines_EllipsizesLastLine()
    {
        var text = new Text("alpha beta gamma delta epsilon zeta eta theta iota kappa", TypeRole.BodyL, maxLines: 2);
        var node = Layout(text, w: 140);

        node.Text!.Lines.Count.Should().Be(2);
        node.Text.Lines[^1].Ellipsized.Should().BeTrue();
    }

    [Fact]
    public void TruncationContract_TextShrinksBeforePushingSiblings()
    {
        // Spec A2: text shrinks to ellipsis before any sibling is pushed out; fixed children never shrink.
        var row = new Row(gap: 8) { Width = SizeValue.Fixed(150) };
        row.Add(FixedBox(60, 20));
        row.Add(new Text("a very long transaction description that cannot possibly fit", TypeRole.BodyM));
        var node = Layout(row, w: 150);

        var box = node.Children[0];
        var text = node.Children[1];
        box.Bounds.Width.Should().Be(60, "fixed children never shrink");
        text.Bounds.Width.Should().BeLessThanOrEqualTo(150 - 60 - 8 + 0.01f);
        (text.Bounds.X + text.Bounds.Width).Should().BeLessThanOrEqualTo(150.01f, "nothing is pushed out");
        text.Text!.Lines[^1].Ellipsized.Should().BeTrue(
            "the shrunk text was CUT — and since TruncationContractTests, the measurer that\n            cut it also put the mark in, so this flag and the glyphs agree");
    }

    /// <summary>
    /// Wrapping a child must not change what a shrinking row does to it. It does not — and the
    /// REASON is worth keeping, because for a while it was an accident and this test could not tell
    /// the difference.
    /// <para>
    /// WHAT IT USED TO PIN. A bare <c>Text</c> was cut by the truncation contract, which found text
    /// among a row's children BY TYPE and so could not see a wrapped one. What shrank the wrapper
    /// instead was its min-content floor answering ZERO — an omission from a hand-kept list that
    /// happened to land the wrapped text where the contract would have put the bare one. Letting
    /// every reader look through, as the shape invites, broke exactly this: the wrapper stopped
    /// shrinking at its child's longest word, 150 where bare gives 22. So it stood as the guard
    /// against the blanket fix while #225 was open.
    /// </para>
    /// <para>
    /// WHAT IT PINS NOW: the same parity, arrived at on purpose. The contract looks through
    /// transparent wrappers (<c>LayoutTransparency</c>) and cuts the item by re-measuring it, so the
    /// wrapped text takes the bare text's path rather than landing on its number by cancellation.
    /// <c>LayoutTransparencyTests</c> asks the same question of all twenty wrappers, and of the line
    /// count and the ellipsis this one cannot see — because a single unbreakable word cannot wrap
    /// either way, which is how a wrapped text that wrapped to eight lines passed here for months.
    /// </para>
    /// <para>
    /// Asserted as PARITY rather than as a pixel: the number is the text measurer's business and the
    /// claim is only that wrapping changes nothing.
    /// </para>
    /// </summary>
    [Fact]
    public void ALayoutTransparentWrapper_DoesNotChangeItsChildsFloor()
    {
        static float WidthOfSecondChild(VisualNode second)
        {
            var row = new Row(gap: 8) { Width = SizeValue.Fixed(150) };
            row.Add(FixedBox(120, 20));
            row.Add(second);
            return Layout(row, w: 150).Children[1].Bounds.Width;
        }

        var text = new Text("antidisestablishmentarianism", TypeRole.BodyM);

        var bare = WidthOfSecondChild(text);
        var wrapped = WidthOfSecondChild(new Draggable(text));

        wrapped.Should().BeApproximately(bare, 0.01f,
            "Draggable carries no geometry of its own, so a row must treat it exactly as it treats "
            + "the text inside it — however the engine happens to arrive there");
    }

    [Fact]
    public void Pressable_HitRect_ExpandsTo48()
    {
        var builder = new DisplayListBuilder();
        var pressable = new Pressable(FixedBox(20, 20));
        var result = eQuantic.UI.Native.Components.PhotonRealizer.Realize(
            pressable, 200, 200, PhotonTheme.Instance, ThemeMode.Light, builder);

        result.HitRegions.Should().HaveCount(1);
        var hit = result.HitRegions[0].Bounds;
        hit.Width.Should().Be(Touch.MinTarget);
        hit.Height.Should().Be(Touch.MinTarget);
        hit.X.Should().Be(-14, "expansion is symmetric around the 20dp visual");
    }
}
