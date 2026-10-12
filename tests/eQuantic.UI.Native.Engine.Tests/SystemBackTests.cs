using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The platform's back (#772): Android's gesture and button reach <see cref="PhotonHost.Back"/>,
/// which closes what is open, through the Escape every layer already declares. When nothing is
/// open it takes nothing, and the system's own back leaves the app.
/// <para>
/// <see cref="PhotonHost.CanGoBack"/> is pinned beside every case, because Android asks it UP
/// FRONT: predictive back draws "leave the app" or "stay" while the gesture is still under the
/// finger, from whether the app registered for back. A host that answers yes and then takes
/// nothing swallows a back; one that answers no while a dialog is open lets the app vanish
/// under it.
/// </para>
/// </summary>
public class SystemBackTests
{
    private static PhotonHost Open(VisualNode root, float width = 390, float height = 700)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, width, height);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    [Fact]
    public void WithNothingOpen_BackIsTheSystems()
    {
        var host = Open(new Column(gap: 0) { new Text("Home"), new Button("Pay", onPressed: () => { }) });

        host.CanGoBack.Should().BeFalse("nothing is open, so back leaves the app");
        host.Back().Should().BeFalse();
    }

    [Fact]
    public void BackClosesADismissibleDialog()
    {
        var closed = 0;
        var host = Open(new Dialog("Delete this?", "It cannot be undone.",
            [new DialogAction("Delete", () => { })], dismissible: true, onDismiss: () => closed++));

        host.CanGoBack.Should().BeTrue();
        host.Back().Should().BeTrue();
        closed.Should().Be(1);
    }

    [Fact]
    public void ButNotOneThatRefusesToBeDismissed()
    {
        var closed = 0;
        var host = Open(new Dialog("Confirm payment", "€40 will be charged.",
            [new DialogAction("Pay", () => { })], dismissible: false, onDismiss: () => closed++));

        host.CanGoBack.Should().BeFalse("a dialog that cannot be dismissed gives back nothing to close");
        host.Back().Should().BeFalse();
        closed.Should().Be(0);
    }

    [Fact]
    public void TheTopmostLayerCloses()
    {
        var dialogClosed = 0;
        var sheetClosed = 0;
        var stack = new Stack { Width = SizeValue.Fill, Height = SizeValue.Fill };
        stack.Add(new Dialog("Under", "Body", [new DialogAction("Ok", () => { })],
            dismissible: true, onDismiss: () => dialogClosed++));
        stack.Add(new BottomSheet(new Text("Over"), onDismiss: () => sheetClosed++));

        Open(stack).Back().Should().BeTrue();

        sheetClosed.Should().Be(1);
        dialogClosed.Should().Be(0, "the one on top is the one the person is looking at");
    }

    [Fact]
    public void BackClosesAnOpenDrawer()
    {
        var closed = 0;
        var host = Open(new Drawer(new Text("Destinations"), open: true, onDismiss: () => closed++));

        host.Back().Should().BeTrue();
        closed.Should().Be(1);
    }

    [Fact]
    public void BackClosesAnOpenMenu()
    {
        var closed = 0;
        var host = Open(new Anchored(new Button("Menu"), new Text("Item"))
        {
            Open = true,
            OnDismiss = () => closed++,
        });

        host.Back().Should().BeTrue();
        closed.Should().Be(1);
    }

    /// <summary>
    /// An edit does not hold back, as a focused <c>TextField</c> does not hold Flutter's: on a phone
    /// the keyboard has already spent the first back going down, and a back that only took the caret
    /// away would be a press with nothing useful coming of it. With nothing open, back is the
    /// system's, and the field keeps the caret.
    /// </summary>
    [Fact]
    public void AFieldBeingEditedDoesNotHoldBack()
    {
        var host = Open(new Column(gap: 0) { new TextEntry("", _ => { }) { Placeholder = "Name" } });
        host.KeyDown("Tab");
        host.RenderFrame(new DisplayListBuilder());
        host.TextTarget.Should().NotBeNull();

        host.CanGoBack.Should().BeFalse("nothing is open, so back leaves the app");
        host.Back().Should().BeFalse();
        host.RenderFrame(new DisplayListBuilder());

        host.TextTarget.Should().NotBeNull("back took nothing, so the field still has the caret");
    }

    /// <summary>The layer goes first, as a binding does for every key: a field inside a sheet leaves
    /// with the sheet, rather than spending a back on its caret.</summary>
    [Fact]
    public void AFieldInsideALayerLeavesWithIt()
    {
        var closed = 0;
        var host = Open(new BottomSheet(new TextEntry("", _ => { }) { Placeholder = "Reply" },
            onDismiss: () => closed++));
        // Tab walks to the field, past any stop the sheet keeps before it.
        for (var i = 0; i < 4 && host.TextTarget is null; i++)
        {
            host.KeyDown("Tab");
            host.RenderFrame(new DisplayListBuilder());
        }
        host.TextTarget.Should().NotBeNull();

        host.Back().Should().BeTrue();
        closed.Should().Be(1);
    }

    /// <summary>
    /// A focus ring alone is not something back closes. Escape clears it on a desktop, where it is
    /// drawn; on a phone the person would press back and see nothing happen.
    /// </summary>
    [Fact]
    public void AFocusRingAloneDoesNotTakeBack()
    {
        var host = Open(new Column(gap: 0) { new Button("Pay", onPressed: () => { }) });
        host.KeyDown("Tab");
        host.RenderFrame(new DisplayListBuilder());
        host.FocusedPath.Should().NotBeNull();

        host.CanGoBack.Should().BeFalse();
        host.Back().Should().BeFalse();
        host.FocusedPath.Should().NotBeNull("back took nothing, so it cleared nothing either");
    }

    private static VisualNode Inbox() => new ScrollView(new Column(gap: 0)
    {
        new ListItem("First message", "Someone") { OnPressed = () => { } },
    });

    /// <summary>On a phone the chosen item is a PLACE, and back leaves it for the list.</summary>
    [Fact]
    public void OnAPhone_BackLeavesTheDetailForTheList()
    {
        var backs = 0;
        var host = Open(new ListDetail(Inbox, () => new Text("The message body"), onBack: () => backs++)
        {
            Title = "Message detail",
        });

        host.CanGoBack.Should().BeTrue();
        host.Back().Should().BeTrue();
        backs.Should().Be(1);
    }

    /// <summary>Wide, both panes are on screen, so there is nowhere to go back to and back is the
    /// system's.</summary>
    [Fact]
    public void OnATablet_BackIsTheSystems()
    {
        var backs = 0;
        var host = Open(new ListDetail(Inbox, () => new Text("The message body"), onBack: () => backs++)
        {
            Title = "Message detail",
        }, width: 1100);

        host.CanGoBack.Should().BeFalse();
        host.Back().Should().BeFalse();
        backs.Should().Be(0);
    }

    [Fact]
    public void WithTheListOnScreen_BackIsTheSystems()
    {
        var host = Open(new ListDetail(Inbox, onBack: () => { }) { ListTitle = "Inbox" });

        host.CanGoBack.Should().BeFalse("the list is the first screen, and back from it leaves the app");
    }
}
