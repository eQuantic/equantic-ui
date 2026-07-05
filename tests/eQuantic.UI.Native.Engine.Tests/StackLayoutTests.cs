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
