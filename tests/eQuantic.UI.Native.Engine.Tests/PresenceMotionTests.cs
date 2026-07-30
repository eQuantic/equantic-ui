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
        store.EndFrame(0);

        store.BeginFrame();
        store.Progress("ov0", 100, false).Should().Be(0.5f, "smoothstep midpoint at Base/2");
        store.EndFrame(100);

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
        store.EndFrame(0);

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
        store.EndFrame(0);

        // The overlay left the tree: a frame passes without the path.
        store.BeginFrame();
        store.EndFrame(400);

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

    // ---- Exit: the departed subtree replays its snapshot ----------------------------------------

    [Fact]
    public void Store_Departure_SpawnsAnExit_ThatFallsOverFast_AndEvictsWhenDone()
    {
        var store = new PresenceStore();
        var commands = new[] { new DrawCommand { Kind = DrawCommandKind.FillRRect } };

        store.BeginFrame();
        store.Progress("ov0", 0, false);
        store.Snapshot("ov0", PresenceMotion.SlideUp, commands);
        store.EndFrame(0);

        // The frame WITHOUT the path spawns the exit from the last snapshot.
        store.BeginFrame();
        store.EndFrame(300);
        var mid = store.ActiveExits(350, reducedMotion: false);
        mid.Should().ContainSingle();
        mid[0].Alpha.Should().Be(0.5f, "smoothstep(50/Fast) — the layer alpha falls with the exit");
        mid[0].Drop.Should().Be(8f, "SlideUp reverses: eased × SlideDistance downward");
        mid[0].Commands.Should().BeSameAs(commands);
        store.AnyActive.Should().BeTrue("the host keeps scheduling until the exit lands");

        // Past Fast the exit is done and evicted — nothing replays, nothing schedules.
        store.BeginFrame();
        store.ActiveExits(500, false).Should().BeEmpty();
        store.AnyActive.Should().BeFalse();
    }

    [Fact]
    public void Store_ReducedMotion_ExitIsACrossfade_NoDrop()
    {
        var store = new PresenceStore();
        store.BeginFrame();
        store.Progress("ov0", 0, true);
        store.Snapshot("ov0", PresenceMotion.SlideUp, [new DrawCommand()]);
        store.EndFrame(0);

        store.BeginFrame();
        store.EndFrame(200);
        var exits = store.ActiveExits(260, reducedMotion: true);
        exits.Should().ContainSingle();
        exits[0].Alpha.Should().Be(0.5f, "smoothstep(60/ReducedCrossfadeMs)");
        exits[0].Drop.Should().Be(0f, "reduced motion drops the movement — fade only");
    }

    [Fact]
    public void Realizer_DepartedPresence_ReplaysAsPixelsOnly_ThenSettlesClean()
    {
        // A stateful page whose sheet leaves on dismiss — the REAL declarative-presence flow.
        var page = new DismissiblePage();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 360, 400);

        host.RenderFrame(new DisplayListBuilder());                    // sheet enters (t=0)
        var settled = host.RenderFrame(new DisplayListBuilder(), 1000); // settled

        // Dismiss through the scrim — the sheet leaves the TREE on the next frame.
        var scrim = settled.HitRegions[^1];
        host.PressDown(scrim.Bounds.Center.X, scrim.Bounds.Center.Y);
        host.PressUp(scrim.Bounds.Center.X, scrim.Bounds.Center.Y);

        // The first frame WITHOUT the subtree detects the departure — the exit starts HERE (alpha 1).
        var departure = host.RenderFrame(new DisplayListBuilder(), 1050);
        departure.HasActiveMotion.Should().BeTrue("the departure frame starts the exit clock");
        departure.HitRegions.Should().NotContain(r => r.Node.Label == "dismiss",
            "a departed subtree is pixels only — input passes through immediately");

        // 50ms into the exit: the replay paints inside a falling-alpha layer.
        var exiting = new DisplayListBuilder();
        var exitFrame = host.RenderFrame(exiting, 1100);
        exiting.Build().Commands.ToArray().Should().Contain(
            c => c.Kind == DrawCommandKind.BeginLayer && c.StrokeWidth == 0.5f,
            "the departed subtree replays inside a falling-alpha layer (smoothstep(50/Fast))");
        exitFrame.HasActiveMotion.Should().BeTrue();

        // Past Fast: the exit evicted — no layers, no replay, frame at rest.
        var rest = new DisplayListBuilder();
        var restFrame = host.RenderFrame(rest, 1300);
        rest.Build().Commands.ToArray().Should().NotContain(c => c.Kind == DrawCommandKind.BeginLayer);
        restFrame.HasActiveMotion.Should().BeFalse();
    }

    [Theory]
    [InlineData(ThemeMode.Light, "sheet-exiting-light")]
    [InlineData(ThemeMode.Dark, "sheet-exiting-dark")]
    public void BottomSheet_MidExit_RendersTheFallingSheet(ThemeMode mode, string golden)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(360, 400);
        var host = new PhotonHost(new DismissiblePage(), PhotonTheme.Instance, mode, 360, 400);

        host.RenderFrame(new DisplayListBuilder());                    // enter starts
        var settled = host.RenderFrame(new DisplayListBuilder(), 1000); // settled
        var scrim = settled.HitRegions[^1];
        host.PressDown(scrim.Bounds.Center.X, scrim.Bounds.Center.Y);
        host.PressUp(scrim.Bounds.Center.X, scrim.Bounds.Center.Y);     // dismissed
        host.RenderFrame(new DisplayListBuilder(), 1050);               // departure — exit starts

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 1100);                                // 50ms in: half-faded, dropping
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, golden);
    }

    private sealed class DismissiblePage : Primitives.StatefulComponent
    {
        private bool _open = true;

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: 0) { Width = SizeValue.Fill };
            column.Add(new Text("Page", TypeRole.BodyM));
            if (_open)
                column.Add(new BottomSheet(new Text("Sheet", TypeRole.BodyM),
                    onDismiss: () => SetState(() => _open = false)));
            return column;
        }
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
