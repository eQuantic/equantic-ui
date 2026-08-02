using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The margins the SYSTEM owns. The HOST reports them and the app never guesses: the same tree
/// insets on a phone and doesn't on a desktop window, because the numbers come from outside it.
/// </summary>
public class SafeAreaTests
{
    private static readonly EdgeInsets Phone = new(0, 54, 0, 34);

    private static LayoutContext Context(EdgeInsets insets) =>
        new(PhotonTheme.Instance, ApproximateTextMeasurer.Instance) { SafeAreaInsets = insets };

    private static VisualNode Content() =>
        new Box(new BoxStyle { Width = 200, Height = 100 });

    [Fact]
    public void HostInsets_PushTheChildIn()
    {
        var tree = LayoutEngine.Layout(new SafeArea(Content()), 400, 800, Context(Phone));

        tree.Bounds.Height.Should().Be(100 + 54 + 34, "the node grows by what it keeps clear");
        tree.Children[0].Bounds.Y.Should().Be(54);
        tree.Children[0].Bounds.X.Should().Be(0, "this phone has no side cutouts");
    }

    [Fact]
    public void WithoutCutouts_TheNodeIsTransparent()
    {
        var tree = LayoutEngine.Layout(new SafeArea(Content()), 400, 800, Context(default));

        tree.Bounds.Height.Should().Be(100, "a desktop window owns none of its edges");
        tree.Children[0].Bounds.Y.Should().Be(0);
    }

    [Fact]
    public void OnlyTheRequestedEdgesInset()
    {
        var tree = LayoutEngine.Layout(new SafeArea(Content(), SafeEdges.Top), 400, 800, Context(Phone));

        tree.Children[0].Bounds.Y.Should().Be(54);
        tree.Bounds.Height.Should().Be(100 + 54, "the bottom was not asked for");
    }

    [Fact]
    public void ExtraPaddingAddsToTheInset_NotInsteadOfIt()
    {
        var tree = LayoutEngine.Layout(
            new SafeArea(Content()) { Extra = EdgeInsets.All(8) }, 400, 800, Context(Phone));

        tree.Children[0].Bounds.Y.Should().Be(54 + 8);
        tree.Bounds.Height.Should().Be(100 + 54 + 34 + 16);
    }

    [Fact]
    public void AnEdgeLeftOutStillKeepsItsOwnPadding()
    {
        var tree = LayoutEngine.Layout(
            new SafeArea(Content(), SafeEdges.Top) { Extra = EdgeInsets.All(8) }, 400, 800, Context(Phone));

        tree.Bounds.Height.Should().Be(100 + 54 + 8 + 8,
            "the bottom keeps the caller's 8 without the host's 34");
    }

    [Fact]
    public void TheChildMeasuresAgainstWhatIsLeft()
    {
        // A Fill child must not measure against the full viewport and then get pushed off it.
        var fill = new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill });
        var tree = LayoutEngine.Layout(new SafeArea(fill), 400, 800, Context(Phone));

        tree.Children[0].Bounds.Height.Should().Be(800 - 54 - 34);
        tree.Bounds.Height.Should().Be(800);
    }
}
