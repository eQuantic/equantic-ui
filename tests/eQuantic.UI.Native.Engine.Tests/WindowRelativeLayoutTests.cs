using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The window-relative cap on the NATIVE side — the same fact the web writes as a `calc`, resolved
/// here against the window the pass was handed.
/// <para>
/// It cannot be resolved from the available space, which is the reason the layout context had to
/// learn the window at all: an overlay deep in a tree is offered whatever its parent has left, and
/// that is not the window.
/// </para>
/// </summary>
public class WindowRelativeLayoutTests
{
    private static float Measure(VisualNode root, float windowHeight)
    {
        var node = LayoutEngine.Layout(root, 400, windowHeight, new LayoutContext(PhotonTheme.Instance, new ApproximateTextMeasurer()));
        return node.Bounds.Height;
    }

    /// <summary>A panel taller than any window: its own content wants more than the cap allows.</summary>
    private static Box TallPanel(SizeValue cap)
    {
        var column = new Column(gap: 0);
        for (var i = 0; i < 40; i++) column.Add(new Box(new BoxStyle { Height = 40 }));
        return new Box(new BoxStyle { MaxHeight = cap }, column);
    }

    [Theory]
    [InlineData(900, 88, 812)]
    [InlineData(700, 88, 612)]
    [InlineData(500, 0, 500)]
    public void TheCapFollowsTheWindow(float window, float inset, float expected)
    {
        // The whole point: one declaration, a different number in every window. A constant would
        // clip early in the tall one and overflow in the short one.
        Measure(TallPanel(SizeValue.WindowMinus(inset)), window).Should().BeApproximately(expected, 0.5f);
    }

    [Fact]
    public void APlainNumberIsStillDp()
    {
        Measure(TallPanel(620), 900).Should().BeApproximately(620, 0.5f);
    }

    [Fact]
    public void AShortPanelIsNotStretchedToTheCap()
    {
        // A cap is a ceiling, never a height — a panel that fits keeps its own size.
        var small = new Box(new BoxStyle { MaxHeight = SizeValue.WindowMinus(88) },
            new Box(new BoxStyle { Height = 120 }));

        Measure(small, 900).Should().BeApproximately(120, 0.5f);
    }
}
