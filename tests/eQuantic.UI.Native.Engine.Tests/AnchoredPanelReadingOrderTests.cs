using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// An anchored node's panel is read IN PLACE, right after its anchor, as the web lowers it inside the anchor's
/// markup and as Flutter attaches an <c>OverlayPortal</c>'s child to the portal (#1868). Photon read every
/// layer after the page, so what followed the anchor was read before the panel; and a dialog panel's stop was
/// the whole layer, so the scrim's dismiss button was read inside the dialog.
/// </summary>
public class AnchoredPanelReadingOrderTests
{
    private static List<(SemanticRole Role, string Label)> Read(VisualNode page)
    {
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 600);
        host.RenderFrame(new DisplayListBuilder());
        return host.Semantics().Select(node => (node.Role, node.Label)).ToList();
    }

    private static Column Page(VisualNode middle)
    {
        var page = new Column(gap: 8);
        page.Add(new Text("Before", TypeRole.BodyM));
        page.Add(middle);
        page.Add(new Text("After", TypeRole.BodyM));
        return page;
    }

    [Fact]
    public void AnOpenMenu_IsReadRightAfterItsTrigger_BeforeWhatFollowsOnThePage()
    {
        var items = new Column(gap: 0);
        items.Add(new Pressable(new Text("Rename", TypeRole.BodyM), () => { }) { Role = PressableRole.MenuItem });
        items.Add(new Pressable(new Text("Delete", TypeRole.BodyM), () => { }) { Role = PressableRole.MenuItem });
        var menu = new Anchored(new Pressable(new Text("More", TypeRole.BodyM), () => { }), items)
        {
            Open = true,
            PanelRole = AnchorPanelRole.Menu,
        };

        Read(Page(menu)).Should().Equal(
            [
                (SemanticRole.StaticText, "Before"),
                (SemanticRole.Button, "More"),
                (SemanticRole.MenuItem, "Rename"),
                (SemanticRole.MenuItem, "Delete"),
                (SemanticRole.StaticText, "After"),
            ],
            "the menu's items follow the trigger they hang from, as the web reads them");
    }

    [Fact]
    public void ADialogPanel_IsTheDialog_AfterTheScrimsDismissButton()
    {
        var calendar = new Column(gap: 0);
        calendar.Add(new Text("July 2026", TypeRole.BodyM));
        var picker = new Anchored(new Pressable(new Text("Pick", TypeRole.BodyM), () => { }), calendar)
        {
            Open = true,
            PanelRole = AnchorPanelRole.Dialog,
            OnDismiss = () => { },
        };

        Read(Page(picker)).Should().Equal(
            [
                (SemanticRole.StaticText, "Before"),
                (SemanticRole.Button, "Pick"),
                (SemanticRole.Button, "Dismiss"),
                (SemanticRole.Dialog, ""),
                (SemanticRole.StaticText, "July 2026"),
                (SemanticRole.StaticText, "After"),
            ],
            "the scrim's dismiss button stands outside the dialog, which is the panel, as on the web");
    }
}
