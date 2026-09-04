using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The web half of "a box with a decided height hands it down". CSS gives it for free on the width
/// — a div's child div is full-width — and nothing at all on the height, so the height is said out
/// loud. Without this the two targets would answer differently for the same tree, which is the one
/// thing the write-once architecture cannot allow.
/// <para>
/// Cross-pinned with <c>lowering.spec.ts</c> and with the engine's DecidedHeightTests.
/// </para>
/// </summary>
public class DecidedHeightRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static Row Bar()
    {
        var row = new Row(gap: 8) { Cross = CrossAlign.Center };
        row.Add(new Text("Title", TypeRole.Label));
        return row;
    }

    [Fact]
    public void ARowInsideABar_IsToldToFillIt()
    {
        var node = Render(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 56 }, Bar()));

        node.Children[0].Attributes["style"].Should().Contain("height: 100%");
    }

    /// <summary>
    /// A PERCENTAGE, never the number: inside a parent whose own height is auto, CSS computes a
    /// percentage height as auto — which is exactly the indeterminate case the layout engine
    /// answers by hugging. One declaration, both answers.
    /// </summary>
    [Fact]
    public void ItIsAPercentage_SoAnAutoHeightParentStillHugs()
    {
        var node = Render(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }, Bar()));

        node.Children[0].Attributes["style"].Should().Contain("height: 100%");
    }

    [Fact]
    public void AHuggingBox_SaysNothing()
    {
        var node = Render(new Box(new BoxStyle { Width = SizeValue.Fill }, Bar()));

        (node.Children[0].Attributes.GetValueOrDefault("style") ?? "").Should().NotContain("height");
    }

    /// <summary>A control hugs inside a taller box — the inline-block fence the width already
    /// respects, so a button in a 56dp bar is a button and not a 56dp slab.</summary>
    [Fact]
    public void AControl_KeepsItsOwnHeight()
    {
        var node = Render(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 56 },
            new Button("Save")));

        (node.Children[0].Attributes.GetValueOrDefault("style") ?? "").Should().NotContain("height: 100%");
    }

    [Fact]
    public void AChildWithItsOwnHeight_IsNeverOverruled()
    {
        var inner = new Box(new BoxStyle { Width = SizeValue.Fill, Height = 20 });
        var node = Render(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 56 }, inner));

        var style = node.Children[0].Attributes["style"]!;
        style.Should().Contain("height: 20px").And.NotContain("height: 100%");
    }
}
