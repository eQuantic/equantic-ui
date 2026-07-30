using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Spec §06 — ENTER MOTION (the C2/C3/C4 fence): a <see cref="Presence"/> subtree animates in when
/// it first appears. The store is a pure clock per layout path (first sighting starts the entrance,
/// departed paths prune so a re-entry replays); the realizer paints mid-flight frames inside a
/// group-opacity layer with the SlideUp rise as a paint-only translate. Settled frames emit NO
/// layer — the goldens stayed byte-identical, which is that proof at the pixel level.
/// </summary>
public class PresenceMotionTests
{
    // ---- The store: a pure presence clock -------------------------------------------------------

    [Fact]
    public void Store_FirstSighting_StartsTheEntrance_AndSettlesAtBase()
    {
        var store = new PresenceStore();

        store.BeginFrame();
        store.Progress("ov0", timeMs: 0, reducedMotion: false).Should().Be(0f, "the first frame starts at 0");
        store.AnyActive.Should().BeTrue("an entrance is mid-flight");
        store.EndFrame();

        store.BeginFrame();
        store.Progress("ov0", 100, false).Should().Be(0.5f, "smoothstep midpoint at Base/2");
        store.EndFrame();

        store.BeginFrame();
        store.Progress("ov0", 200, false).Should().Be(1f, "settled at Motion.Base");
        store.AnyActive.Should().BeFalse("nothing is mid-flight once settled");
    }

    [Fact]
    public void Store_ReducedMotion_UsesTheShortCrossfadeClock()
    {
        var store = new PresenceStore();
        store.BeginFrame();
        store.Progress("ov0", 0, reducedMotion: true).Should().Be(0f);
        store.EndFrame();

        store.BeginFrame();
        store.Progress("ov0", Motion.ReducedCrossfadeMs, reducedMotion: true)
            .Should().Be(1f, "the reduced clock settles at the crossfade duration (120ms)");
    }

    [Fact]
    public void Store_DepartedPathPrunes_SoAReEntryReplaysTheEntrance()
    {
        var store = new PresenceStore();

        store.BeginFrame();
        store.Progress("ov0", 0, false);
        store.EndFrame();

        // The overlay left the tree: a frame passes without the path.
        store.BeginFrame();
        store.EndFrame();

        // It returns much later — the entrance must REPLAY (start at 0), not resume settled.
        store.BeginFrame();
        store.Progress("ov0", 5000, false).Should().Be(0f, "a re-entry starts a fresh entrance");
        store.AnyActive.Should().BeTrue();
    }

    // ---- The realizer: layer + rise on mid-flight frames ----------------------------------------

    private static VisualNode Page(VisualNode presence)
    {
        var column = new Column(gap: 0) { Width = SizeValue.Fill };
        column.Add(presence);
        return column;
    }

    [Fact]
    public void MidFlight_EmitsAGroupOpacityLayer_AndSettled_EmitsNone()
    {
        var box = new Box(new BoxStyle { Width = 40, Height = 40, Background = PhotonTheme.Instance.Surface });
        var host = new PhotonHost(Page(new Presence(box)), PhotonTheme.Instance, ThemeMode.Light, 200, 200);

        host.RenderFrame(new DisplayListBuilder()); // t=0 — the entrance starts
        var mid = new DisplayListBuilder();
        var frame = host.RenderFrame(mid, timeMs: 100);
        var layer = mid.Build().Commands.ToArray().Single(c => c.Kind == DrawCommandKind.BeginLayer);
        layer.StrokeWidth.Should().Be(0.5f, "smoothstep(100/200) — the layer alpha IS the progress");
        frame.HasActiveMotion.Should().BeTrue("the host keeps scheduling frames mid-entrance");

        var settled = new DisplayListBuilder();
        var restFrame = host.RenderFrame(settled, timeMs: 1000);
        settled.Build().Commands.ToArray().Should().NotContain(c => c.Kind == DrawCommandKind.BeginLayer,
            "a settled presence paints plainly — no layer cost at rest");
        restFrame.HasActiveMotion.Should().BeFalse();
    }

    [Fact]
    public void SlideUp_RidesAPaintOnlyRise_ThatReducedMotionDrops()
    {
        var box = new Box(new BoxStyle { Width = 40, Height = 40, Background = PhotonTheme.Instance.Surface });

        // Mid-flight at progress 0.5 → the fill sits (1-0.5) * SlideDistance = 8dp BELOW its slot.
        var host = new PhotonHost(Page(new Presence(box, PresenceMotion.SlideUp)),
            PhotonTheme.Instance, ThemeMode.Light, 200, 200);
        host.RenderFrame(new DisplayListBuilder());
        var mid = new DisplayListBuilder();
        host.RenderFrame(mid, timeMs: 100);
        var fill = mid.Build().Commands.ToArray().Last(c => c.Kind == DrawCommandKind.FillRRect);
        fill.Transform.Should().Be(Matrix2D.Translation(0, 8f), "(1 − progress) × SlideDistance, paint-only");

        // Reduce Motion: movement is REPLACED by the short crossfade — no rise, layer only.
        var reduced = new PhotonHost(Page(new Presence(box, PresenceMotion.SlideUp)),
            PhotonTheme.Instance, ThemeMode.Light, 200, 200) { ReducedMotion = true };
        reduced.RenderFrame(new DisplayListBuilder());
        var rMid = new DisplayListBuilder();
        reduced.RenderFrame(rMid, timeMs: 60);
        var commands = rMid.Build().Commands.ToArray();
        commands.Single(c => c.Kind == DrawCommandKind.BeginLayer).StrokeWidth
            .Should().Be(0.5f, "smoothstep(60/120) on the crossfade clock");
        commands.Last(c => c.Kind == DrawCommandKind.FillRRect).Transform
            .Should().Be(Matrix2D.Identity, "reduced motion drops the slide — fade only");
    }

    // ---- The entrance itself, as pixels ---------------------------------------------------------

    [Theory]
    [InlineData(ThemeMode.Light, "sheet-entering-light")]
    [InlineData(ThemeMode.Dark, "sheet-entering-dark")]
    public void BottomSheet_MidEntrance_RendersTheRisingSheet(ThemeMode mode, string golden)
    {
        var content = new Column(gap: Space.S2);
        content.Add(new Text("Share this card", TypeRole.Title));
        content.Add(new Text("Choose a destination.", TypeRole.BodyM));
        var page = new Column(gap: 0) { Width = SizeValue.Fill, Padding = EdgeInsets.All(Space.S4) };
        page.Add(new Text("Page behind", TypeRole.Heading));
        page.Add(new BottomSheet(content));

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(360, 400);
        var host = new PhotonHost(page, PhotonTheme.Instance, mode, 360, 400);
        host.RenderFrame(new DisplayListBuilder());               // entrance starts
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs: 100);                   // progress 0.5 — half-faded, half-risen
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, golden);
    }
}
