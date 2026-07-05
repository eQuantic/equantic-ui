using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The animation system's native half (spec §06, slice 1): loop motion is a PURE function of the
/// frame clock — the realizer translates the subtree by the sampled offset (transform-only), the
/// clipping Box confines it, and the frame reports active motion so the host keeps scheduling
/// frames. Reduce Motion renders at rest and stops the loop. Deterministic by construction: every
/// test pins an exact t.
/// </summary>
public class LoopMotionTests
{
    private static readonly BoxStyle SegmentStyle = new()
    {
        Width = 60,
        Height = 4,
        Background = PhotonTheme.Instance.Colors(Variant.Primary).Base,
    };

    private static DisplayList RenderAt(VisualNode root, float timeMs, bool reducedMotion = false)
    {
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(root, 200, 40, PhotonTheme.Instance, ThemeMode.Light, builder,
            timeMs: timeMs, reducedMotion: reducedMotion);
        return builder.Build();
    }

    [Theory]
    [InlineData(0f, -21f)]      // phase 0   → fromX (-0.35 × 60)
    [InlineData(600f, 21f)]     // phase 0.5 → midpoint (0.35 × 60)
    [InlineData(1200f, -21f)]   // full period wraps back to fromX
    [InlineData(1800f, 21f)]    // wraps across periods
    public void Offset_IsAPureFunctionOfTheClock(float timeMs, float expectedX)
    {
        var root = new LoopMotion(new Box(SegmentStyle), LoopEffect.SlideX, -0.35f, 1.05f, 1200);

        var commands = RenderAt(root, timeMs).Commands.ToArray();

        // Shapes stay in layout space; the loop offset rides the baked command transform (M31 = tx).
        var fill = commands.Single(c => c.Kind == DrawCommandKind.FillRRect);
        fill.Transform.M31.Should().BeApproximately(expectedX, 0.01f,
            "the translate offset is fromX + (toX - fromX) · phase, scaled by the node's own width");
    }

    [Fact]
    public void ReducedMotion_RendersAtRest_AndReportsNoActiveMotion()
    {
        var root = new LoopMotion(new Box(SegmentStyle), LoopEffect.SlideX, -0.35f, 1.05f, 1200);

        var builder = new DisplayListBuilder();
        var frame = PhotonRealizer.Realize(root, 200, 40, PhotonTheme.Instance, ThemeMode.Light, builder,
            timeMs: 600, reducedMotion: true);

        frame.HasActiveMotion.Should().BeFalse("Reduce Motion statically replaces movement (spec §06)");
        var fill = builder.Build().Commands.ToArray().Single(c => c.Kind == DrawCommandKind.FillRRect);
        fill.Transform.Should().Be(Matrix2D.Identity, "the child renders at its natural laid-out position");
    }

    [Fact]
    public void Frame_ReportsActiveMotion_OnlyWhenALoopIsRunning()
    {
        var animated = new LoopMotion(new Box(SegmentStyle), LoopEffect.SlideX, -0.35f, 1.05f, 1200);
        var still = new Box(SegmentStyle);

        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(animated, 200, 40, PhotonTheme.Instance, ThemeMode.Light, builder, timeMs: 0)
            .HasActiveMotion.Should().BeTrue();

        builder = new DisplayListBuilder();
        PhotonRealizer.Realize(still, 200, 40, PhotonTheme.Instance, ThemeMode.Light, builder, timeMs: 0)
            .HasActiveMotion.Should().BeFalse();
    }

    [Fact]
    public void ClippingBox_ConfinesChildren_ButNeverItsOwnChrome()
    {
        var track = new Box(new BoxStyle
        {
            Width = 200,
            Height = 4,
            Background = PhotonTheme.Instance.SurfaceSubtle,
            CornerRadius = new CornerRadii(Radius.Full),
            Clip = true,
        }, new Box(SegmentStyle));

        var fills = RenderAt(track, timeMs: 0).Commands.ToArray()
            .Where(c => c.Kind == DrawCommandKind.FillRRect).ToList();
        fills.Should().HaveCount(2);
        fills[0].Clip.Should().BeNull("the track's own chrome IS the clip shape — it never clips itself");
        fills[1].Clip.Should().NotBeNull("children confine to the track's rrect");
        fills[1].Clip!.Value.Rect.Width.Should().Be(200);
    }

    [Fact]
    public void PhotonHost_KeepsSchedulingFrames_WhileMotionRuns()
    {
        var host = new PhotonHost(new ProgressBar(), PhotonTheme.Instance, ThemeMode.Light, 240, 8);

        host.RenderFrame(new DisplayListBuilder(), timeMs: 0);
        host.NeedsRender.Should().BeTrue("an indeterminate bar animates — the loop stays hot");

        host.ReducedMotion = true;
        host.RenderFrame(new DisplayListBuilder(), timeMs: 16);
        host.NeedsRender.Should().BeFalse("Reduce Motion parks the loop");

        var determinate = new PhotonHost(new ProgressBar(0.4f), PhotonTheme.Instance, ThemeMode.Light, 240, 8);
        determinate.RenderFrame(new DisplayListBuilder(), timeMs: 0);
        determinate.NeedsRender.Should().BeFalse("a determinate bar is a still frame");
    }

    /// <summary>Animated frames golden-test like any other because they are pure functions of t —
    /// half period puts the segment dead center, clipped by the Radius.Full track.</summary>
    [Theory]
    [InlineData(ThemeMode.Light, "loop-indeterminate-light")]
    [InlineData(ThemeMode.Dark, "loop-indeterminate-dark")]
    public void IndeterminateProgressBar_GoldenAtHalfPeriod(ThemeMode mode, string golden)
    {
        var column = new Column(gap: Space.S2) { Padding = EdgeInsets.All(Space.S2) };
        column.Add(new ProgressBar());
        column.Add(new ProgressBar(variant: Variant.Success) { Prominent = true });

        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(240, 40);
        var host = new PhotonHost(column, PhotonTheme.Instance, mode, 240, 40);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs: 600);
        backend.Render(builder.Build(), surface);

        GoldenImage.Match(surface, golden);
    }

    [Fact]
    public void IndeterminateProgressBar_SweepsTheSegmentAcrossTheClippedTrack()
    {
        var bar = new ProgressBar();

        // t=600 (half period): the layer offset is 0.35 × 240 = 84; the 30% segment spans 72dp.
        var builder = new DisplayListBuilder();
        var frame = PhotonRealizer.Realize(bar, 240, 8, PhotonTheme.Instance, ThemeMode.Light, builder, timeMs: 600);
        frame.HasActiveMotion.Should().BeTrue();

        var clipped = builder.Build().Commands.ToArray()
            .Where(c => c.Kind == DrawCommandKind.FillRRect && c.Clip is not null)
            .ToList();

        clipped.Should().HaveCount(1, "inside the track only the sweeping segment fills");
        clipped[0].Transform.M31.Should().BeApproximately(84f, 0.5f, "half period sweeps 35% of the track");
        clipped[0].Shape.Rect.Width.Should().BeApproximately(72f, 0.5f, "the segment is 30% of the track");
        clipped[0].Clip!.Value.Rect.Width.Should().Be(240, "the track clips the sweep to its rrect");
    }
}
