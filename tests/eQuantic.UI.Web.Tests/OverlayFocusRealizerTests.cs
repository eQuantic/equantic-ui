using eQuantic.UI.Components;
using eQuantic.UI.Web;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// §10 focus management, SSR half: a MODAL, open overlay layer is a dialog — role, aria-modal (what
/// tells assistive tech the page behind is inert), tabindex=-1 (the initial-focus fallback) and the
/// <c>data-eq-trap</c> marker the client controller discovers after each pass. The keyboard half is
/// client-only, exactly as Pressable's click and Shortcut's chord are; cross-pinned with
/// focus-trap.spec.ts, which asserts the trap and the restoration.
/// </summary>
public class OverlayFocusRealizerTests
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

    private static List<HtmlNode> All(HtmlNode node, Func<HtmlNode, bool> match)
    {
        var found = new List<HtmlNode>();
        void Walk(HtmlNode n)
        {
            if (match(n)) found.Add(n);
            foreach (var child in n.Children) Walk(child);
        }
        Walk(node);
        return found;
    }

    private static HtmlNode Layer(HtmlNode root) =>
        Find(root, n => (n.Attributes.GetValueOrDefault("class") ?? "").Contains("eq-overlay"))!;

    [Fact]
    public void ModalOverlay_IsADialog_AndCarriesTheTrapMarker()
    {
        var layer = Layer(Render(new Overlay(new Primitives.Box(new BoxStyle { Width = 10, Height = 10 }))));

        layer.Attributes["role"].Should().Be("dialog");
        layer.Attributes["aria-modal"].Should().Be("true");
        layer.Attributes["tabindex"].Should().Be("-1",
            "the container is the initial-focus fallback (§10: BottomSheet focuses the sheet itself)");
        layer.Attributes.Should().ContainKey("data-eq-trap",
            "the marker is byte-identical on both producers — no path has to agree across them");
    }

    [Fact]
    public void NonModalOverlay_IsNotADialog_AndNeverTraps()
    {
        // A toast interrupts nothing: trapping focus in it would be a bug, not an oversight.
        var layer = Layer(Render(new Overlay(new Primitives.Box(new BoxStyle { Width = 10, Height = 10 }))
        {
            Modal = false,
        }));

        layer.Attributes.Should().NotContainKey("role");
        layer.Attributes.Should().NotContainKey("aria-modal");
        layer.Attributes.Should().NotContainKey("data-eq-trap");
    }

    [Fact]
    public void ClosedOverlay_IsNotADialogRightNow()
    {
        // The keep-mounted close (Overlay.Motion): the layer stays in the tree and fades out. It is
        // hidden, out of hit-testing and out of the focus order — so it is not a dialog, and
        // dropping the marker is exactly what ends the trap and restores focus.
        var layer = Layer(Render(new Overlay(new Primitives.Box(new BoxStyle { Width = 10, Height = 10 }))
        {
            Open = false,
            Motion = TransitionSpec.Colors(),
        }));

        layer.Attributes.Should().NotContainKey("data-eq-trap");
        layer.Attributes.Should().NotContainKey("aria-modal");
        layer.Attributes["style"].Should().Contain("visibility: hidden");
    }

    [Theory]
    [MemberData(nameof(ModalSurfaces))]
    public void EveryModalSurface_GetsTheDialogContract(string name, VisualNode surface)
    {
        var layer = Layer(Render(surface));

        layer.Attributes["role"].Should().Be("dialog", $"{name} interrupts the page");
        layer.Attributes["aria-modal"].Should().Be("true", $"{name} makes the page behind inert");
        layer.Attributes.Should().ContainKey("data-eq-trap", $"{name} traps focus while open");
    }

    public static TheoryData<string, VisualNode> ModalSurfaces()
    {
        var body = new Primitives.Box(new BoxStyle { Width = 40, Height = 40 });
        return new TheoryData<string, VisualNode>
        {
            { "Dialog", new Dialog("Delete account?", "This cannot be undone.",
                [new DialogAction("Delete", () => { })]) },
            { "BottomSheet", new BottomSheet(body, onDismiss: () => { }) },
            { "Drawer", new Drawer(body, open: true, onDismiss: () => { }) },
        };
    }

    [Fact]
    public void Toast_DoesNotTrap_ItIsTheNonModalLayer()
    {
        var layer = Layer(Render(new Toast("Saved", Variant.Success)));
        layer.Attributes.Should().NotContainKey("data-eq-trap");
    }

    [Fact]
    public void ADialogEmitsExactlyOneTrap_HoweverManyLayersItComposes()
    {
        // Dialog composes scrim + card inside ONE Overlay (with a Presence in between): two traps
        // would fight over the keyboard, and the topmost would win by accident of tree order.
        var node = Render(new Dialog("Delete account?", "This cannot be undone.",
            [new DialogAction("Delete", () => { }), new DialogAction("Cancel", () => { })]));

        All(node, n => n.Attributes.ContainsKey("data-eq-trap")).Should().HaveCount(1);
    }
}
