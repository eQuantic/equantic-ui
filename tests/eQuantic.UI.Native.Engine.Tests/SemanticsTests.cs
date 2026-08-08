using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The semantics tree — what a platform accessibility bridge hands to VoiceOver/TalkBack. Photon
/// draws its own pixels, so without this tree an app is INVISIBLE to a screen reader; these tests
/// conduct the path (a path nobody runs is a path that does not work) and pin its contract:
/// reading order is tree order, a control's inner text is its name, dialogs are seen, and the
/// activate action runs the same handler a tap does.
/// </summary>
public class SemanticsTests
{
    private sealed class Page : Primitives.StatefulComponent
    {
        public int Exports;
        public bool DialogOpen;

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
            column.Add(new Text("Payments", TypeRole.Heading, context.Theme.TextPrimary));
            column.Add(new Pressable(new Box(new BoxStyle { Width = SizeValue.Fixed(80), Height = SizeValue.Fixed(32) }),
                () => SetState(() => Exports++)) { Label = "Export" });
            // No explicit Label: the subtree text is the name.
            column.Add(new Pressable(new Text("New payment", TypeRole.Label, context.Theme.TextPrimary),
                () => { }));
            column.Add(new Pressable(new Text("Delete", TypeRole.Label, context.Theme.TextPrimary),
                () => { }) { Disabled = true });
            column.Add(new TextEntry("42", _ => { }) { Placeholder = "Search payments" });
            column.Add(new Link("/", new Text("Home", TypeRole.Label, context.Theme.TextPrimary)));

            if (DialogOpen)
            {
                var dialog = new Column(gap: Space.S2);
                dialog.Add(new Text("Confirm export", TypeRole.Title, context.Theme.TextPrimary));
                dialog.Add(new Pressable(new Text("Yes", TypeRole.Label, context.Theme.TextPrimary), () => { })
                    { Label = "Confirm" });
                column.Add(new Overlay(dialog));
            }

            return column;
        }
    }

    private static (PhotonHost Host, Page Page) Open(bool dialog = false)
    {
        var page = new Page { DialogOpen = dialog };
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        host.RenderFrame(new DisplayListBuilder());
        return (host, page);
    }

    // ---- the contract ----------------------------------------------------------------------------

    [Fact]
    public void ReadingOrderIsTreeOrder_WithRolesAndNames()
    {
        var (host, _) = Open();

        var semantics = host.Semantics();

        semantics.Select(s => (s.Role, s.Label)).Should().ContainInOrder(
            (SemanticRole.StaticText, "Payments"),
            (SemanticRole.Button, "Export"),
            (SemanticRole.Button, "New payment"),
            (SemanticRole.Button, "Delete"),
            (SemanticRole.TextField, "Search payments"),
            (SemanticRole.Link, "Home"));
    }

    [Fact]
    public void AControlsInnerTextIsItsName_NotASeparateElement()
    {
        var (host, _) = Open();

        var semantics = host.Semantics();

        // "New payment" and "Home" name their controls; neither is announced twice.
        semantics.Where(s => s.Role == SemanticRole.StaticText)
            .Select(s => s.Label)
            .Should().NotContain(new[] { "New payment", "Home" },
                "a control's subtree text is consumed as its accessible name — the web's <button>text</button> rule");
    }

    [Fact]
    public void DisabledSurfacesAsDisabled_AndTheFieldCarriesItsValue()
    {
        var (host, _) = Open();

        var semantics = host.Semantics();

        semantics.Single(s => s.Label == "Delete").Disabled.Should().BeTrue(
            "a disabled control is announced as dimmed, not hidden");
        semantics.Single(s => s.Role == SemanticRole.TextField).Value.Should().Be("42");
    }

    [Fact]
    public void ADialogIsSeen_AfterThePageBeneathIt()
    {
        var (host, _) = Open(dialog: true);

        var labels = host.Semantics().Select(s => s.Label).ToList();

        labels.Should().ContainInOrder("Home", "Confirm export", "Confirm");
    }

    /// <summary>The parity gate: the semantics walk and the input walk are two traversals of the
    /// same tree, and this is what keeps them from drifting apart. Every keyboard stop a frame
    /// registers must be announced, and every announced control must be reachable.</summary>
    [Fact]
    public void SemanticsMatchFocusStops()
    {
        var page = new Page { DialogOpen = true };
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        var frame = host.RenderFrame(new DisplayListBuilder());

        var stopPaths = frame.FocusStops.Select(s => s.Path).ToHashSet();
        var semantics = host.Semantics();

        // Focusable roles, minus the disabled (Tab skips them; a reader announces them).
        var focusable = semantics
            .Where(s => s.Role is SemanticRole.Button or SemanticRole.TextField && !s.Disabled)
            .Select(s => s.Path).ToHashSet();

        focusable.Should().BeSubsetOf(stopPaths, "every announced control must be keyboard-reachable");
        stopPaths.Should().BeSubsetOf(
            semantics.Select(s => s.Path).ToHashSet(),
            "every keyboard stop must be announced");
    }

    [Fact]
    public void ActivatePath_RunsTheSameHandlerATapDoes()
    {
        var (host, page) = Open();
        var export = host.Semantics().Single(s => s.Label == "Export");

        host.ActivatePath(export.Path).Should().BeTrue();

        page.Exports.Should().Be(1);
    }

    [Fact]
    public void ActivatePath_RefusesADisabledControl()
    {
        var (host, _) = Open();
        var delete = host.Semantics().Single(s => s.Label == "Delete");

        host.ActivatePath(delete.Path).Should().BeFalse();
    }

    [Fact]
    public void BeforeTheFirstFrame_ThereIsNothingToAnnounce()
    {
        var host = new PhotonHost(new Page(), PhotonTheme.Instance, ThemeMode.Light, 400, 400);

        host.Semantics().Should().BeEmpty();
    }

    private sealed class AxPage : Primitives.StatefulComponent
    {
        public int Volume = 5;

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2);
            column.Add(new TextEntry("", null) { Label = "Recipient", Placeholder = "name@host" });
            column.Add(new Adjustable(new Box(new BoxStyle { Width = 120, Height = 24 }),
                d => SetState(() => Volume += d)) { Label = "Volume" });
            return column;
        }
    }

    [Fact]
    public void ATextEntry_AnnouncesItsLabel_OverThePlaceholder()
    {
        var (host, _) = Open();
        var withLabel = new PhotonHost(new AxPage(), PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        withLabel.RenderFrame(new DisplayListBuilder());

        withLabel.Semantics().Single(s => s.Role == SemanticRole.TextField).Label
            .Should().Be("Recipient", "the explicit Label names the field");
        host.Semantics().Single(s => s.Role == SemanticRole.TextField).Label
            .Should().Be("Search payments", "the placeholder is only the fallback name");
    }

    [Fact]
    public void AdjustPath_DrivesTheAdjustable_BothWays()
    {
        var page = new AxPage();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        host.RenderFrame(new DisplayListBuilder());
        var slider = host.Semantics().Single(s => s.Role == SemanticRole.Slider);

        host.AdjustPath(slider.Path, +1).Should().BeTrue("VoiceOver's swipe up increments");
        host.RenderFrame(new DisplayListBuilder(), 16);
        host.AdjustPath(slider.Path, -1).Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.AdjustPath(slider.Path, -1).Should().BeTrue();

        page.Volume.Should().Be(4, "+1 then −1 then −1");
        host.AdjustPath("nowhere", 1).Should().BeFalse("a stale path answers false, never throws");
    }
}
