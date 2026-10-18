using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A DISMISSED layer, on the one target that used to ignore the fact (#252). <see cref="Overlay.Open"/>
/// is the keep-mounted state: with <see cref="Overlay.Motion"/> set the layer stays in the tree so the
/// transition can run, and the web ends that state at <c>visibility: hidden</c> — out of paint, out of
/// hit-testing, out of the focus order and out of the accessibility tree.
/// <para>
/// Photon read the flag nowhere at all. A closed dialog painted at full opacity, took taps, kept its
/// Tab stop and was read out — indistinguishable from an open one except that the reader was not even
/// told it was a dialog, since the layer's own announcement is the one thing that WAS gated on Open.
/// </para>
/// </summary>
public class ClosedOverlayTests
{
    private static readonly TransitionSpec Fade = new(StyleChannels.Opacity, 200);

    private static VisualNode Dialog(string title)
    {
        var body = new Column(gap: Space.S2);
        body.Add(new Text(title, TypeRole.Title));
        body.Add(new Pressable(new Text("OK", TypeRole.Label), () => { }) { Label = $"{title} OK" });
        return body;
    }

    private static VisualNode Page(params VisualNode[] layers)
    {
        var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        column.Add(new Text("page", TypeRole.BodyM));
        foreach (var layer in layers) column.Add(layer);
        return column;
    }

    private static (PhotonHost Host, RealizeResult Frame, int Commands) Render(VisualNode page)
    {
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        var builder = new DisplayListBuilder();
        var frame = host.RenderFrame(builder);
        return (host, frame, builder.Build().Commands.Length);
    }

    /// <summary>
    /// Every route at once, against the page ALONE as the baseline — the four answers used to be
    /// identical with the layer open and closed, and a test that checked only one of them would have
    /// called three-quarters of the defect fixed.
    /// </summary>
    [Fact]
    public void AClosedLayerIsPaintedTappedFocusedAndReadByNobody()
    {
        var bare = Render(Page());
        var closed = Render(Page(new Overlay(Dialog("Confirm")) { Open = false, Motion = Fade, Label = "Confirm" }));

        closed.Commands.Should().Be(bare.Commands, "a dismissed layer draws exactly nothing");
        closed.Frame.OverlayRoots.Should().BeEmpty();
        closed.Frame.HitRegions.Should().NotContain(region => Positions.InALayer(region.Element));
        closed.Frame.FocusStops.Should().NotContain(stop => Positions.InALayer(stop.Element));
        closed.Host.Semantics().Should().NotContain(node => Positions.InALayer(closed.Frame, node.Id));
        closed.Host.Semantics().Should().BeEquivalentTo(bare.Host.Semantics());
    }

    /// <summary>The same tree with the layer OPEN, so the assertions above are known to be able to
    /// fail — every one of them holds the other way round.</summary>
    [Fact]
    public void AnOpenLayerIsAllFourOfThose()
    {
        var bare = Render(Page());
        var open = Render(Page(new Overlay(Dialog("Confirm")) { Open = true, Motion = Fade, Label = "Confirm" }));

        open.Commands.Should().BeGreaterThan(bare.Commands);
        open.Frame.OverlayRoots.Should().ContainSingle();
        open.Frame.HitRegions.Should().Contain(region => Positions.InALayer(region.Element));
        open.Frame.FocusStops.Should().Contain(stop => Positions.InALayer(stop.Element));
        open.Host.Semantics().Should().Contain(node => Positions.InALayer(open.Frame, node.Id));
    }

    /// <summary>A page that holds a drawer and a dialog, and closes the drawer.</summary>
    private sealed class TwoLayers : Primitives.StatefulComponent
    {
        private bool _drawerOpen = true;

        public void CloseTheDrawer() => SetState(() => _drawerOpen = false);

        public override VisualNode Build(ComponentContext context) => Page(
            new Overlay(Dialog("Drawer")) { Open = _drawerOpen, Motion = Fade, Label = "Drawer" },
            new Overlay(Dialog("Dialog")) { Open = true, Motion = Fade, Label = "Dialog" });
    }

    /// <summary>
    /// The reason the gate lives in the realize pass and not in <c>EmitOverlay</c>: a layer was named
    /// <c>ov{i}</c> by its index in the QUEUE, so declining to queue a closed one would have renumbered
    /// every layer after it, and closing the toast above a dialog would have moved the dialog to a new
    /// identity — and identity is what carries its focus, its pressed state and its presence across
    /// the rebuild. A layer is its overlay's position now, which the queue cannot renumber; the test
    /// stays as the pin that closing one layer leaves the other its identity.
    /// </summary>
    [Fact]
    public void ClosingTheLayerAboveAnotherDoesNotRenameIt()
    {
        var page = new TwoLayers();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        var both = host.RenderFrame(new DisplayListBuilder());
        both.OverlayRoots.Should().HaveCount(2);
        var dialogLayer = both.OverlayRoots[1].Element;

        page.CloseTheDrawer();
        var drawerGone = host.RenderFrame(new DisplayListBuilder());

        drawerGone.OverlayRoots.Should().ContainSingle()
            .Which.Element.Should().BeSameAs(dialogLayer,
                "the surviving layer keeps the identity it had while the other was open");

        // THE PAIRING, at the place it is observable: the semantics walk reads OverlayLayers[i]
        // beside OverlayRoots[i], so a list that shrinks out of step announces the closed layer's
        // name over the open layer's contents — or, when the closed one is first, announces nothing
        // at all, because the gate that reads Modal and Open finds the wrong node.
        host.Semantics().Should()
            .ContainSingle(node => node.Role == SemanticRole.Dialog)
            .Which.Should().Match<SemanticNode>(dialog => dialog.Label == "Dialog" && dialog.Id == dialogLayer!.Id);
    }

    /// <summary>
    /// WEB PARITY, stated rather than assumed: <see cref="Overlay.Open"/> means nothing without
    /// <see cref="Overlay.Motion"/> — the layer is then shown and hidden by being built or not, which
    /// is what its own doc says and what <c>LowerOverlay</c> does (the hidden styles are inside
    /// <c>if (overlay.Motion is { } motion)</c>). A realizer that honoured the flag anyway would be
    /// the write-once promise broken in the quietest possible way.
    /// </summary>
    [Fact]
    public void WithoutMotionTheFlagChangesNothing()
    {
        var closed = Render(Page(new Overlay(Dialog("Confirm")) { Open = false, Label = "Confirm" }));
        var open = Render(Page(new Overlay(Dialog("Confirm")) { Open = true, Label = "Confirm" }));

        closed.Commands.Should().Be(open.Commands);
        closed.Frame.OverlayRoots.Should().ContainSingle();
        closed.Host.Semantics().Should().Contain(node => Positions.InALayer(closed.Frame, node.Id));
    }
}
