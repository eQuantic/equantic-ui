using System.Diagnostics;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The performance harness (plan W7): the numbers the definition of done PROMISES — "120 Hz with
/// zero steady-state allocations, all frame budgets met" — measured instead of assumed. What runs
/// here is the C# half of a frame (realize: layout + token resolve + emit); the GPU half is the
/// backends' and is covered by parity, not by CI timing.
/// <para>
/// The ceilings are REGRESSION rulers, not aspirations: pinned a margin above what the engine does
/// TODAY, so a change that doubles per-frame allocation or command count fails loudly, while the
/// ratchet can tighten as pooling/arena work lands. The printed numbers are the baseline record.
/// Time asserts are deliberately an order of magnitude loose — CI machines are shared, and a flaky
/// perf test teaches everyone to ignore it.
/// </para>
/// </summary>
public class PerfHarnessTests
{
    private readonly ITestOutputHelper _output;

    public PerfHarnessTests(ITestOutputHelper output) => _output = output;

    /// <summary>A dashboard-shaped scene: 24 cards of text + buttons over a moving strip — dense
    /// enough that a structural regression moves the numbers, small enough to stay fast.</summary>
    private static VisualNode DenseScene(IAppTheme theme)
    {
        var page = new Column(gap: Space.S3) { Width = SizeValue.Fill };

        // The motion strip is what keeps NeedsRender true — the steady state being measured.
        page.Add(new LoopMotion(
            new Box(new BoxStyle
            {
                Width = SizeValue.Fixed(120),
                Height = SizeValue.Fixed(8),
                Background = theme.SurfaceHighlight,
                CornerRadius = new CornerRadii(4),
            }),
            LoopEffect.SlideX, -0.35f, 1.05f, 1200));

        for (var r = 0; r < 6; r++)
        {
            var row = new Row(gap: Space.S3) { Width = SizeValue.Fill };
            for (var c = 0; c < 4; c++)
            {
                var card = new Column(gap: Space.S2);
                card.Add(new Text($"Card {r}-{c}", TypeRole.Title, theme.TextPrimary, maxLines: 1));
                card.Add(new Text("Two lines of body copy that wrap and truncate the way a real card's copy does.",
                    TypeRole.BodyM, theme.TextSecondary, maxLines: 2));
                card.Add(new Button($"Open {r}-{c}", Variant.Outline, SizeVariant.Small));
                row.Add(new Flexible(new Box(new BoxStyle
                {
                    Width = SizeValue.Fill,
                    Background = theme.Surface,
                    CornerRadius = new CornerRadii(8),
                    Padding = EdgeInsets.All(Space.S3),
                }, card), 1));
            }
            page.Add(row);
        }
        return page;
    }

    private static PhotonHost Open()
    {
        var host = new PhotonHost(DenseScene(PhotonTheme.Instance), PhotonTheme.Instance,
            ThemeMode.Light, 1280, 900);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    // ---- the rulers ------------------------------------------------------------------------------

    /// <summary>Managed bytes allocated per steady-state frame (motion running, no interaction).
    /// Baselines: 183 KB on 2026-08-07 morning; 106 KB same day after the path-string cache and
    /// the reused-builder loop (the shells' real pattern, measured here). What remains is the
    /// LayoutNode tree — pooling it needs an explicit RealizeResult lifetime first (a double
    /// buffer quietly rewrote retained trees under 155 tests). The ratchet tightens as that
    /// lands; it never loosens casually.</summary>
    private const long AllocationCeilingBytesPerFrame = 152 * 1024;

    /// <summary>Draw commands the dense scene emits — the batching ruler (M1 spoke of a static
    /// dashboard under 30 DRAW CALLS after batching; commands are the pre-batch upper bound).
    /// Baseline 2026-08-07: 146.</summary>
    private const int CommandCeiling = 200;

    /// <summary>An order of magnitude above the 8.33 ms frame budget — a regression alarm that
    /// survives a noisy CI box, not a benchmark. Baseline 2026-08-07 (M-series): p50 0.23 ms,
    /// p95 0.32 ms.</summary>
    private const double RealizeP95CeilingMs = 33.0;

    [Fact]
    public void SteadyMotion_AllocationsPerFrame_StayUnderTheCeiling()
    {
        var host = Open();
        host.LastFrame!.HasActiveMotion.Should().BeTrue("the loop strip is what makes this steady state");

        // Warm-up: caches fill, tiered JIT settles the hot path.
        for (var frame = 0; frame < 10; frame++)
            host.RenderFrame(new DisplayListBuilder(), frame * 8f);

        const int measured = 60;
        // The shells reuse ONE builder and Reset it per frame — the harness measures that path.
        var builder = new DisplayListBuilder();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < measured; frame++)
        {
            builder.Reset();
            host.RenderFrame(builder, 80f + frame * 8f);
        }
        var perFrame = (GC.GetAllocatedBytesForCurrentThread() - before) / measured;

        _output.WriteLine($"steady-state allocation: {perFrame / 1024.0:F1} KB/frame " +
                          $"(ceiling {AllocationCeilingBytesPerFrame / 1024.0:F0} KB)");
        perFrame.Should().BeLessThan(AllocationCeilingBytesPerFrame,
            "a frame that allocates more than the pinned baseline has regressed — " +
            "tighten the ceiling as pooling lands, never loosen it casually");
    }

    [Fact]
    public void DenseScene_CommandCount_IsPinned()
    {
        var host = Open();
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 16f);
        var commands = builder.Build().Count;

        _output.WriteLine($"dense scene: {commands} draw commands (ceiling {CommandCeiling})");
        commands.Should().BeGreaterThan(0);
        commands.Should().BeLessThan(CommandCeiling,
            "the dense scene's command count is the batching ruler — a jump means something " +
            "started emitting per-node work it used to share");
    }

    [Fact]
    public void DenseScene_RealizeTime_StaysInsideTheAlarm()
    {
        var host = Open();
        for (var frame = 0; frame < 10; frame++)
            host.RenderFrame(new DisplayListBuilder(), frame * 8f);

        const int measured = 30;
        var times = new double[measured];
        for (var frame = 0; frame < measured; frame++)
        {
            var clock = Stopwatch.StartNew();
            host.RenderFrame(new DisplayListBuilder(), 80f + frame * 8f);
            times[frame] = clock.Elapsed.TotalMilliseconds;
        }
        Array.Sort(times);
        var p50 = times[measured / 2];
        var p95 = times[(int)(measured * 0.95)];

        _output.WriteLine($"realize: p50 {p50:F2} ms, p95 {p95:F2} ms " +
                          $"(budget 8.33 ms; alarm {RealizeP95CeilingMs} ms)");
        p95.Should().BeLessThan(RealizeP95CeilingMs,
            "an order of magnitude over the frame budget is a regression whatever the machine");
    }
}
