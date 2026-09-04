using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The overlay web half (Phase C slice 1): Overlay lowers to the generated fixed inset-0 stacking
/// layer (.eq-overlay — mechanics in the generated stylesheet, composition in the component); the
/// Dialog composes scrim + centered E5 card from the ordinary vocabulary. Cross-pinned with
/// overlay-dialog.spec.ts.
/// </summary>
public class OverlayDialogRealizerTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static HtmlNode Render(VisualNode node) => WebRealizer.Lower(node, Theme).Render();

    private static HtmlNode? Find(HtmlNode node, Func<HtmlNode, bool> match)
    {
        if (match(node)) return node;
        foreach (var child in node.Children)
        {
            if (Find(child, match) is { } found) return found;
        }
        return null;
    }

    private static Dialog Confirm(bool dismissible = false) => new(
        "Delete card?", "\"Work Visa 4821\" will be removed.",
        [new DialogAction("Keep card", null, Variant.Ghost), new DialogAction("Delete", null, Variant.Destructive)],
        dismissible);

    [Fact]
    public void Overlay_LowersToTheGeneratedFixedLayer()
    {
        var node = Render(new Overlay(new Box(new BoxStyle { Width = 10, Height = 10 })));

        node.Tag.Should().Be("div");
        node.Attributes["class"].Should().Be("eq-overlay");
        node.Attributes.Should().NotContainKey("style", "mechanics live in the generated stylesheet");
        node.Children.Should().HaveCount(1);
    }

    [Fact]
    public void GeneratedStylesheet_CarriesTheOverlayLayer()
    {
        // display:grid is the SIZE HANDOFF: the layer's single cell stretches the child to the
        // viewport, so a hugging layer root (Drawer's bare Stack) doesn't collapse to content
        // height and orphan its Fill-sized descendants at zero.
        PhotonCssGenerator.Generate(Theme).Should().Contain(
            ".eq-overlay { position: fixed; top: 0; right: 0; bottom: 0; left: 0; z-index: 1000; display: grid; }");
    }

    [Fact]
    public void Dialog_ComposesScrim_CenteringColumn_AndTheE5Card()
    {
        var node = Render(Confirm());

        node.Attributes["class"].Should().Be("eq-overlay");

        // Scrim: a full-size DISABLED pressable (dead click — a button must resolve the confirm).
        var scrim = Find(node, n => n.Tag == "button")!;
        scrim.Attributes.Should().ContainKey("disabled");
        var scrimBox = scrim.Children[0];
        scrimBox.Attributes["style"].Should().Contain($"background-color: {TokenCss.Value(Theme.Scrim)}");
        scrimBox.Attributes["style"].Should().Contain("width: 100%; height: 100%");

        // Card: Radius.Xl, E5 shadow, S5 padding, capped at 480.
        var card = Find(node, n =>
            n.Tag == "div" && n.Attributes.TryGetValue("style", out var s) && s!.Contains("border-radius: 20px"))!;
        card.Attributes["style"].Should().Contain("max-width: 480px");
        card.Attributes["style"].Should().Contain($"box-shadow: {TokenCss.Shadow(Theme.Elevation(5))}");
        card.Attributes["style"].Should().Contain("padding: 20px 20px 20px 20px");

        // Actions: right-aligned Medium buttons — Ghost first, Destructive commits.
        var actionsRow = Find(node, n =>
            n.Tag == "div" && n.Attributes.TryGetValue("style", out var s) && s!.Contains("justify-content: flex-end"))!;
        var buttons = actionsRow.Children.Where(c => c.Tag == "button").ToList();
        buttons.Should().HaveCount(2);
    }

    [Fact]
    public void DismissibleDialog_ArmsTheScrim()
    {
        var node = Render(Confirm(dismissible: true));
        var scrim = Find(node, n => n.Tag == "button")!;
        scrim.Attributes.Should().NotContainKey("disabled");
        scrim.Attributes["aria-label"].Should().Be("Dismiss");
    }
}
