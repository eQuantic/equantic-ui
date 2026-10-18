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
/// it first appears. The clock is pure per position (the first measure starts the entrance, and a
/// position that leaves takes it along, so a re-entry replays); the realizer paints mid-flight frames inside a
/// group-opacity layer with the SlideUp rise as a paint-only translate. Settled frames emit NO
/// layer — the goldens stayed byte-identical, which is that proof at the pixel level.
/// </summary>
public class PresenceMotionTests
{
    // ---- The clock: an entrance and an exit per position ----------------------------------------

    /// <summary>A presence's position and the owner that keeps it, as a host keeps its tree: a build
    /// that no longer holds the presence takes its position out, which is its departure.</summary>
    private sealed class Tree
    {
        private static readonly ComponentContext Context = new(PhotonTheme.Instance);
        private readonly BuildOwner _owner = BuildOwner.Structural();

        /// <summary>Builds a frame that holds the presence, and answers its position.</summary>
        public Element Shown()
        {
            var page = new Column(gap: 0);
            page.Add(new Presence(new Box(new BoxStyle { Width = 10, Height = 10 })));
            return _owner.Build(page, Context, WindowSizeClass.Compact, rebuildAll: true).ChildAt(0)!;
        }

        /// <summary>Builds a frame without it: its position leaves the tree.</summary>
        public void Gone() => _owner.Build(new Column(gap: 0), Context, WindowSizeClass.Compact, rebuildAll: true);
    }

    [Fact]
    public void Clock_FirstSighting_StartsTheEntrance_AndSettlesAtBase()
    {
        var clock = new PresenceClock();
        var tree = new Tree();

        clock.BeginFrame(0);
        clock.Progress(tree.Shown(), timeMs: 0, reducedMotion: false).Should().Be(0f, "the first frame starts at 0");
        clock.AnyActive.Should().BeTrue("an entrance is mid-flight");

        clock.BeginFrame(100);
        clock.Progress(tree.Shown(), 100, false).Should().Be(0.5f, "smoothstep midpoint at Base/2");

        clock.BeginFrame(200);
        clock.Progress(tree.Shown(), 200, false).Should().Be(1f, "settled at Motion.Base");
        clock.AnyActive.Should().BeFalse("nothing is mid-flight once settled");
    }

    [Fact]
    public void Clock_ReducedMotion_UsesTheShortCrossfadeClock()
    {
        var clock = new PresenceClock();
        var tree = new Tree();
        clock.BeginFrame(0);
        clock.Progress(tree.Shown(), 0, reducedMotion: true).Should().Be(0f);

        clock.BeginFrame(Motion.ReducedCrossfadeMs);
        clock.Progress(tree.Shown(), Motion.ReducedCrossfadeMs, reducedMotion: true)
            .Should().Be(1f, "the reduced clock settles at the crossfade duration (120ms)");
    }

    [Fact]
    public void Clock_ADepartedPresence_ReentersFromTheStart()
    {
        var clock = new PresenceClock();
        var tree = new Tree();

        clock.BeginFrame(0);
        clock.Progress(tree.Shown(), 0, false);

        // The presence left the tree: its position, and the entrance it kept, went with it.
        clock.BeginFrame(400);
        tree.Gone();

        // It returns much later — the entrance must REPLAY (start at 0), not resume settled.
        clock.BeginFrame(5000);
        clock.Progress(tree.Shown(), 5000, false).Should().Be(0f, "a re-entry starts a fresh entrance");
        clock.AnyActive.Should().BeTrue();
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
    public void Clock_Departure_SpawnsAnExit_ThatFallsOverFast_AndEvictsWhenDone()
    {
        var clock = new PresenceClock();
        var tree = new Tree();
        var commands = new[] { new DrawCommand { Kind = DrawCommandKind.FillRRect } };

        clock.BeginFrame(0);
        var at = tree.Shown();
        clock.Progress(at, 0, false);
        clock.Snapshot(at, PresenceMotion.SlideUp, commands);

        // The frame its position leaves in starts the exit, from the last snapshot.
        clock.BeginFrame(300);
        tree.Gone();
        var mid = clock.ActiveExits(350, reducedMotion: false);
        mid.Should().ContainSingle();
        mid[0].Alpha.Should().Be(0.5f, "smoothstep(50/Fast) — the layer alpha falls with the exit");
        mid[0].Drop.Should().Be(8f, "SlideUp reverses: eased × SlideDistance downward");
        mid[0].Commands.Should().BeSameAs(commands);
        clock.AnyActive.Should().BeTrue("the host keeps scheduling until the exit lands");

        // Past Fast the exit is done and evicted — nothing replays, nothing schedules.
        clock.BeginFrame(500);
        clock.ActiveExits(500, false).Should().BeEmpty();
        clock.AnyActive.Should().BeFalse();
    }

    [Fact]
    public void Clock_ReducedMotion_ExitIsACrossfade_NoDrop()
    {
        var clock = new PresenceClock();
        var tree = new Tree();
        clock.BeginFrame(0);
        var at = tree.Shown();
        clock.Progress(at, 0, true);
        clock.Snapshot(at, PresenceMotion.SlideUp, [new DrawCommand()]);

        clock.BeginFrame(200);
        tree.Gone();
        var exits = clock.ActiveExits(260, reducedMotion: true);
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
        departure.HitRegions.Should().NotContain(r => r.Node.Label == "Dismiss",
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
