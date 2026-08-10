using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// An overlay drawn WHERE IT STANDS.
/// <para>
/// A dialog, a drawer, a sheet and a popover only exist once something opens them, and when they do
/// they take the whole viewport: a scrim over the page, the surface centred or pinned in front of
/// it, an Escape binding, an enter animation. A page that wants to SHOW one — documentation, a
/// design review, a visual-regression suite — wants the surface and none of that, and had no way to
/// ask. Five components rendered blank.
/// </para>
/// </summary>
public class InFlowOverlayTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static (string Html, string Css) Render(VisualNode node)
    {
        var sink = new StyleSink();
        var html = Core.Rendering.HtmlRenderer.RenderNode(
            WebRealizer.Lower(node, Theme, 1f, sink).Render());
        return (html, sink.Css);
    }

    private static Dialog ADialog() =>
        new("Delete this?", "It cannot be undone.", [new DialogAction("Delete")],
            dismissible: true, onDismiss: () => { });

    /// <summary>The card is the dialog; the scrim, the viewport-sized centring, the portal and the
    /// Escape binding are the LAYER that carries it.</summary>
    [Fact]
    public void ADialogInFlow_DrawsItsCardAndNotItsLayer()
    {
        var (layered, _) = Render(ADialog());
        var (inFlow, css) = Render(new InFlow(ADialog()));

        inFlow.Should().Contain("Delete this?", "the surface is the whole point");
        css.Should().NotContain(TokenCss.Value(Theme.Scrim), "the scrim belongs to the layer");
        layered.Should().NotBe(inFlow, "the layered form is a different tree");
    }

    /// <summary>
    /// IN THE FLOW there is no open or closed: the panel is placed, therefore it is there. `Open`
    /// governs whether the LAYER exists, and in the flow there is no layer — a drawer that still
    /// answered `Open` would render the blank this feature exists to fix.
    /// </summary>
    [Fact]
    public void AClosedDrawerInFlow_StillDrawsItsPanel()
    {
        var drawer = new Drawer(new Text("Menu", TypeRole.BodyM), open: false);

        var (blank, _) = Render(drawer);
        var (shown, _) = Render(new InFlow(drawer));

        blank.Should().NotContain("Menu", "closed, it is nothing");
        shown.Should().Contain("Menu");
    }

    /// <summary>A panel stretching a whole documentation section is not the panel. Full height
    /// belongs to the edge it is pinned to, and in the flow it has no edge.</summary>
    [Fact]
    public void ADrawerInFlow_HugsItsContentInsteadOfFillingTheViewport()
    {
        var drawer = new Drawer(new Text("Menu", TypeRole.BodyM), open: true);

        var (_, css) = Render(new InFlow(drawer));

        css.Should().NotContain("height:100%");
    }

    [Fact]
    public void ASheetInFlow_DrawsItsSurface()
    {
        var sheet = new BottomSheet(new Text("Options", TypeRole.BodyM), dismissible: true);

        var (_, css) = Render(new InFlow(sheet));
        var (html, _) = Render(new InFlow(sheet));

        html.Should().Contain("Options");
        css.Should().NotContain(TokenCss.Value(Theme.Scrim));
    }

    /// <summary>The panel is the popover; the anchoring to a trigger — and the trigger itself — is
    /// how it is presented, not what it is.</summary>
    [Fact]
    public void APopoverInFlow_DrawsItsPanelWithoutItsTrigger()
    {
        var popover = new Popover(
            new Text("Open me", TypeRole.Label), new Text("The panel", TypeRole.BodyM), open: true);

        var (html, _) = Render(new InFlow(popover));

        html.Should().Contain("The panel");
        html.Should().NotContain("Open me", "the trigger is the presentation, not the popover");
    }

    /// <summary>The intent ends with the node — a dialog opened for real beside a previewed one
    /// still takes the viewport.</summary>
    [Fact]
    public void TheIntentEndsWithTheNode()
    {
        var column = new Column(gap: Space.S2)
        {
            new InFlow(ADialog()),
            ADialog(),
        };

        var (_, css) = Render(column);

        css.Should().Contain(TokenCss.Value(Theme.Scrim), "the sibling is still a real dialog");
    }

    [Fact]
    public void WithoutTheNode_NothingChanges()
    {
        var (_, css) = Render(ADialog());

        css.Should().Contain(TokenCss.Value(Theme.Scrim));
    }
}
