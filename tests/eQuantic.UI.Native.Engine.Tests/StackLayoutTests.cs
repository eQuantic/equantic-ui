using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>Spec A3 geometry + the badge-overlay golden (Stack + Positioned(top −4, end −4)).</summary>
public class StackLayoutTests
{
    private static LayoutNode Layout(VisualNode root, float w = 200, float h = 200) =>
        LayoutEngine.Layout(root, w, h, new LayoutContext(PhotonTheme.Instance, new ApproximateTextMeasurer()));

    /// <summary>
    /// The cell IS the stack's available space — the contract the web's grid keeps. A stack with a
    /// height of its own hands it to a Fill child whatever the parent said: under an unbounded axis
    /// (a scroll view's) and under an indeterminate one (a hugging column's) alike. Before this, a
    /// Fill canvas inside a 240dp stack measured ZERO there, and a chart drew nothing on Photon.
    /// </summary>
    [Fact]
    public void AFillChild_TakesTheStacksOwnBox_WhateverTheParentOffers()
    {
        var unbounded = new Stack { Width = SizeValue.Fill, Height = SizeValue.Fixed(240) };
        unbounded.Add(new Canvas(_ => { }, SizeValue.Fill, SizeValue.Fill));
        var under = Layout(unbounded, 300, float.PositiveInfinity);
        under.Children[0].Bounds.Height.Should().Be(240, "the stack's own height, not the unbounded axis");
        under.Children[0].Bounds.Width.Should().Be(300);

        var hugging = new Column();
        var inside = new Stack { Width = SizeValue.Fixed(120), Height = SizeValue.Fixed(80) };
        inside.Add(new Primitives.Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }));
        hugging.Add(inside);
        var box = Layout(hugging, 200, 200).Children[0].Children[0];
        box.Bounds.Width.Should().Be(120, "an explicit axis is determinate for the children");
        box.Bounds.Height.Should().Be(80);
    }

    [Fact]
    public void SizesToTheLargestNonPositionedChild()
    {
        var stack = new Stack();
        stack.Add(new Primitives.Box(new BoxStyle { Width = 40, Height = 40 }));
        stack.Add(new Primitives.Box(new BoxStyle { Width = 24, Height = 24 }));
        stack.Add(new Positioned(new Primitives.Box(new BoxStyle { Width = 100, Height = 8 }), top: 0));

        var node = Layout(stack);
        node.Bounds.Width.Should().Be(40, "positioned children never size the stack");
        node.Bounds.Height.Should().Be(40);
    }

    [Fact]
    public void PositionedAnchors_WithSignedOffsets()
    {
        var stack = new Stack();
        stack.Add(new Primitives.Box(new BoxStyle { Width = 40, Height = 40 }));
        stack.Add(new Positioned(new Primitives.Box(new BoxStyle { Width = 16, Height = 16 }), top: -4, end: -4));

        var node = Layout(stack);
        var badge = node.Children[1];
        badge.Bounds.X.Should().Be(40 - 16 + 4, "end −4 overhangs the frame");
        badge.Bounds.Y.Should().Be(-4);
    }

    /// <summary>
    /// An edge is a point plus a fraction of the stack, and the shift a fraction of the child: a
    /// 100 × 30 tooltip at 50% / 25% − 16 of a 400 × 800 stack, centred above its anchor, lands at
    /// (150, 154) — what the web's `left: 50%; top: calc(25% - 16px); translate(-50%, -100%)` draws.
    /// </summary>
    [Fact]
    public void FractionsOfTheStackAndAShiftOfTheChild_PlaceAsTheWebDoes()
    {
        var stack = new Stack { Width = SizeValue.Fixed(400), Height = SizeValue.Fixed(800) };
        stack.Add(new Positioned(new Primitives.Box(new BoxStyle { Width = 100, Height = 30 }), top: -16)
        {
            StartFraction = 0.5f,
            TopFraction = 0.25f,
            ShiftX = -0.5f,
            ShiftY = -1f,
        });

        var tip = Layout(stack).Children[0];

        tip.Bounds.X.Should().Be(150);
        tip.Bounds.Y.Should().Be(154);
    }

    /// <summary>An end fraction measures from the stack's end edge: 10% of 400 leaves a 40 × 20
    /// child's right edge 40 from the stack's, at x = 320.</summary>
    [Fact]
    public void AnEndFraction_MeasuresFromTheEndEdge()
    {
        var stack = new Stack { Width = SizeValue.Fixed(400), Height = SizeValue.Fixed(800) };
        stack.Add(new Positioned(new Primitives.Box(new BoxStyle { Width = 40, Height = 20 })) { EndFraction = 0.1f });

        Layout(stack).Children[0].Bounds.X.Should().Be(320);
    }

    /// <summary>
    /// A child that fills, anchored on one side, fills to the opposite edge: in a 400dp stack it is
    /// 200 wide at 50% and 300 wide at 100dp, as the web's `left; right: 0` draws it (#648 review).
    /// </summary>
    [Theory]
    [InlineData(null, 0.5f, 200f, 200f)]
    [InlineData(100f, null, 100f, 300f)]
    public void AFillingChildAnchoredOnOneSide_FillsToTheOppositeEdge(float? start, float? fraction, float x, float width)
    {
        var stack = new Stack { Width = SizeValue.Fixed(400), Height = SizeValue.Fixed(100) };
        stack.Add(new Positioned(new Primitives.Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 }), start: start)
        {
            StartFraction = fraction,
        });

        var child = Layout(stack).Children[0];

        child.Bounds.X.Should().Be(x);
        child.Bounds.Width.Should().Be(width);
    }

    /// <summary>
    /// The same through a component that builds the Positioned: the contract is the parent's, and
    /// the web resolves it through the component too (#648 review).
    /// </summary>
    [Fact]
    public void AFillingChildPositionedByAComponent_FillsToTheOppositeEdge()
    {
        var stack = new Stack { Width = SizeValue.Fixed(400), Height = SizeValue.Fixed(100) };
        stack.Add(new HalfwayBar());

        var child = Layout(stack).Children[0];

        child.Bounds.X.Should().Be(200);
        child.Bounds.Width.Should().Be(200);
    }

    private sealed class HalfwayBar : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Positioned(new Primitives.Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 })) { StartFraction = 0.5f };
    }

    [Fact]
    public void CenterAlignment_CentersNonPositionedChildren()
    {
        var stack = new Stack(Alignment.Center) { Width = SizeValue.Fixed(100), Height = SizeValue.Fixed(100) };
        stack.Add(new Primitives.Box(new BoxStyle { Width = 20, Height = 20 }));

        var child = Layout(stack).Children[0];
        child.Bounds.X.Should().Be(40);
        child.Bounds.Y.Should().Be(40);
    }

    [Fact]
    public void BadgeOverlay_Golden()
    {
        var stack = new Stack();
        stack.Add(new Avatar("AB", SizeVariant.XLarge, name: "Ana Beatriz") { Status = PresenceStatus.Online });
        stack.Add(new Positioned(new Badge(3) { Ring = true }, top: -4, end: -4));

        var root = new Primitives.Box(new BoxStyle { Padding = EdgeInsets.All(Space.S4) }, stack);
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(96, 96);
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Dark, 96, 96);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        backend.Render(builder.Build(), surface);
        GoldenImage.Match(surface, "stack-badge-overlay");
    }
}
