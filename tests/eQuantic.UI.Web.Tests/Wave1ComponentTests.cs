using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Variant = eQuantic.UI.Primitives.Variant;

namespace eQuantic.UI.Web.Tests;

/// <summary>Wave-1 write-once components lowered to DOM — the spec metrics (A7/B6/B7/B8/B14/B18) as CI pins.</summary>
public class Wave1ComponentTests
{
    private static readonly PhotonTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    [Fact]
    public void Divider_IsAOneDpBorderHairline()
    {
        var style = Render(new Divider()).Attributes["style"]!;
        style.Should().Contain("height: 1px");
        style.Should().Contain($"background-color: {TokenCss.Value(Theme.Border)}");

        // Leading inset pads the WRAPPER so the line itself is inset (spec A7: inset leading 16).
        var inset = Render(new Divider(DividerInset.Leading));
        inset.Attributes["style"].Should().Contain("padding: 0 0 0 16px");
    }

    [Fact]
    public void Badge_CountPill_And_MaxOverflow()
    {
        var node = Render(new Badge(120));
        node.Attributes["style"].Should().Contain("height: 16px");
        node.Attributes["style"].Should().Contain("min-width: 16px");
        node.Attributes["style"].Should().Contain($"background-color: {TokenCss.Value(Theme.Colors(Variant.Destructive).Base)}");
        node.Children[0].Children[0].Children[0].TextContent.Should().Be("99+");

        Render(Badge.AsDot()).Attributes["style"].Should().Contain("width: 8px");
    }

    [Fact]
    public void Chip_FilterSelected_UsesPrimarySubtleAndBorder()
    {
        var node = Render(new Chip("Income", ChipKind.Filter, selected: true, onPressed: () => { }));
        node.Tag.Should().Be("button", "filter chips toggle");
        var box = node.Children[0];
        box.Attributes["style"].Should().Contain("height: 32px");
        box.Attributes["style"].Should().Contain($"background-color: {TokenCss.Value(Theme.Colors(Variant.Primary).Subtle)}");
        box.Attributes["style"].Should().Contain($"border: 1px solid {TokenCss.Value(Theme.Colors(Variant.Primary).Base)}");
    }

    [Fact]
    public void ProgressBar_SplitsTheTrackByFlexWeights()
    {
        var node = Render(new ProgressBar(0.64f));
        node.Attributes["style"].Should().Contain("height: 4px");
        node.Attributes["style"].Should().Contain($"background-color: {TokenCss.Value(Theme.SurfaceSubtle)}");
        node.Children[0].Attributes["style"].Should().Contain("flex: 640 1 0%");
        node.Children[1].Attributes["style"].Should().Contain("flex: 360 1 0%");

        Render(new ProgressBar(0.5f) { Prominent = true }).Attributes["style"].Should().Contain("height: 8px");
    }

    [Fact]
    public void Avatar_SizesFromTheTierTable_AndDeterministicTint()
    {
        var node = Render(new Avatar("AB", SizeVariant.Large, name: "Ana Beatriz"));
        node.Attributes["style"].Should().Contain("width: 40px");
        node.Attributes["style"].Should().Contain("height: 40px");
        node.Attributes["style"].Should().Contain("border-radius: 999px");
        node.Children[0].Children[0].Children[0].TextContent.Should().Be("AB");
    }

    [Fact]
    public void Banner_UsesTheStatusSubtlePair()
    {
        var node = Render(new Banner(Variant.Warning, "Your card expires this month.", "Renew it."));
        var style = node.Attributes["style"]!;
        style.Should().Contain($"background-color: {TokenCss.Value(Theme.Colors(Variant.Warning).Subtle)}");
        style.Should().Contain("border-radius: 14px");
        style.Should().Contain("padding: 12px 14px 12px 14px");
    }
}
