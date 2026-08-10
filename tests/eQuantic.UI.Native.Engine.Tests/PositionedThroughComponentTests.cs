using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>The native half: the same tree, laid out at the corner rather than the origin.</summary>
public class PositionedThroughComponentTests
{
    private sealed class CornerAction : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new Positioned(new Box(new BoxStyle { Width = 32, Height = 32 }), top: 0, end: 0);
    }

    private static LayoutNode Corner(VisualNode corner)
    {
        var stack = new Stack { Width = SizeValue.Fill, Height = SizeValue.Fill };
        stack.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 100 }));
        stack.Add(corner);
        var frame = PhotonRealizer.Realize(stack, 200, 100, PhotonTheme.Instance, ThemeMode.Light,
            new DisplayListBuilder());
        return frame.Root.Children[1];
    }

    [Fact]
    public void AComponentReturningPositioned_LandsAtTheCorner()
    {
        Corner(new CornerAction()).Bounds.X.Should().Be(168, "200 wide, 32 across, pinned to the end");
    }

    /// <summary>The direct form, which is what the component form has to match.</summary>
    [Fact]
    public void ItMatchesTheDirectForm()
    {
        var direct = new Positioned(new Box(new BoxStyle { Width = 32, Height = 32 }), top: 0, end: 0);

        Corner(new CornerAction()).Bounds.X.Should().Be(Corner(direct).Bounds.X);
    }

    /// <summary>A positioned child takes no part in the stack's CONTENT size — otherwise a corner
    /// badge would grow the slab it sits on.</summary>
    [Fact]
    public void ItDoesNotContributeToTheStacksSize()
    {
        var stack = new Stack();
        stack.Add(new Box(new BoxStyle { Width = 100, Height = 50 }));
        stack.Add(new CornerAction());

        var frame = PhotonRealizer.Realize(stack, 400, 400, PhotonTheme.Instance, ThemeMode.Light,
            new DisplayListBuilder());

        frame.Root.Bounds.Width.Should().Be(100);
        frame.Root.Bounds.Height.Should().Be(50);
    }
}
