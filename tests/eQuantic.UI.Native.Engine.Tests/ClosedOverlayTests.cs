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
/// told it was a dialog, since the group announcement is the one thing that WAS gated on Open.
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
        closed.Frame.HitRegions.Should().NotContain(region => region.Path.StartsWith("ov"));
        closed.Frame.FocusStops.Should().NotContain(stop => stop.Path.StartsWith("ov"));
        closed.Host.Semantics().Should().NotContain(node => node.Path.StartsWith("ov"));
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
        open.Frame.HitRegions.Should().Contain(region => region.Path.StartsWith("ov"));
        open.Frame.FocusStops.Should().Contain(stop => stop.Path.StartsWith("ov"));
        open.Host.Semantics().Should().Contain(node => node.Path.StartsWith("ov"));
    }

    /// <summary>
    /// The reason the gate lives in the realize pass and not in <c>EmitOverlay</c>: a layer's paths
    /// are <c>ov{i}</c> by its index in the QUEUE, so declining to queue a closed one renumbers every
    /// layer after it. Closing the toast above a dialog would have moved the dialog to a new identity
    /// — and identity is what carries its focus, its pressed state and its presence snapshots across
    /// the rebuild. The index stays; only the work goes.
    /// </summary>
    [Fact]
    public void ClosingTheLayerAboveAnotherDoesNotRenameIt()
    {
        var both = Render(Page(
            new Overlay(Dialog("Drawer")) { Open = true, Motion = Fade, Label = "Drawer" },
            new Overlay(Dialog("Dialog")) { Open = true, Motion = Fade, Label = "Dialog" }));

        both.Frame.OverlayRoots.Select(root => root.Path).Should().Equal(["ov0", "ov1"]);

        var drawerGone = Render(Page(
            new Overlay(Dialog("Drawer")) { Open = false, Motion = Fade, Label = "Drawer" },
            new Overlay(Dialog("Dialog")) { Open = true, Motion = Fade, Label = "Dialog" }));

        drawerGone.Frame.OverlayRoots.Select(root => root.Path).Should().Equal(["ov1"],
            "the surviving layer keeps the identity it had while the other was open");

        // THE PAIRING, at the place it is observable: the semantics walk reads OverlayLayers[i]
        // beside OverlayRoots[i], so a list that shrinks out of step announces the closed layer's
        // name over the open layer's contents — or, when the closed one is first, announces nothing
        // at all, because the gate that reads Modal and Open finds the wrong node.
        drawerGone.Host.Semantics().Should()
            .ContainSingle(node => node.Role == SemanticRole.Group)
            .Which.Should().Match<SemanticNode>(group => group.Label == "Dialog" && group.Path == "ov1");
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
        closed.Host.Semantics().Should().Contain(node => node.Path.StartsWith("ov"));
    }
}
