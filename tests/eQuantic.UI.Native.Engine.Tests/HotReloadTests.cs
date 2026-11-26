using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What `dotnet watch` needs from a running Photon app, exercised by calling the SAME handlers the
/// runtime calls. The edit itself cannot be simulated here (a metadata delta is the runtime's own
/// business) — what can is everything on our side of the contract: the wake-up, the cache drop,
/// and the state that must survive.
/// </summary>
public class HotReloadTests
{
    /// <summary>Counts every rasterization, so a cache hit and a cache drop are both visible.</summary>
    private sealed class CountingRasterizer : Framework.ITextRasterizer
    {
        public int Calls;

        public Framework.TextRaster? Rasterize(string content, TypeStyle style, float typeScale,
            float maxWidth, int maxLines, float scale, TextAlignment align)
        {
            if (string.IsNullOrEmpty(content)) return null;
            Calls++;
            var width = Math.Max(1, (int)Math.Min(content.Length * 8 * scale, maxWidth * scale));
            var height = Math.Max(1, (int)(style.LineHeight * scale));
            return new Framework.TextRaster(width, height, new byte[width * height]);
        }
    }

    private sealed class Counter : Primitives.StatefulComponent
    {
        public int Count;

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
            column.Add(new Text($"Count: {Count}", TypeRole.Title));
            column.Add(new Button("Bump", onPressed: () => SetState(() => Count++)));
            return column;
        }
    }

    [Fact]
    public void AnAppliedEditWakesEveryLiveHost()
    {
        var one = new PhotonHost(new Counter(), PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        var two = new PhotonHost(new Counter(), PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        one.RenderFrame(new DisplayListBuilder());
        two.RenderFrame(new DisplayListBuilder());
        one.NeedsRender.Should().BeFalse("a still tree presents nothing");

        // The runtime's own sequence: caches first, then the update notification.
        PhotonHotReload.ClearCache(null);
        PhotonHotReload.UpdateApplication(null);

        one.NeedsRender.Should().BeTrue("the loop polls this — within a tick, the new code draws");
        two.NeedsRender.Should().BeTrue("every window of the app, not just the one in front");
    }

    /// <summary>
    /// What failed <see cref="AnAppliedEditWakesEveryLiveHost"/> once under a load near 300 (#1200): work an earlier test
    /// left running, posted to the thread that draws after its test ended, told every live host its tree had changed
    /// (#1239). A post is the process's now and marks no host's tree, so a still host asks for no frame whatever is
    /// posted around it, while the frame the work needs is still wanted until it runs.
    /// </summary>
    [Fact]
    public void AStillHost_AsksForNoFrame_WhileLateWorkIsPostedAroundIt()
    {
        var one = new PhotonHost(new Counter(), PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        var two = new PhotonHost(new Counter(), PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        one.RenderFrame(new DisplayListBuilder());
        two.RenderFrame(new DisplayListBuilder());

        // As an earlier test's async work finishing late, on a thread of its own.
        var ran = false;
        var late = new Thread(() => PhotonDispatcher.Shared.Post(() => ran = true));
        late.Start();
        late.Join();

        one.NeedsRender.Should().BeFalse("a still tree presents nothing, whatever another thread posted");
        two.NeedsRender.Should().BeFalse("nor does the other window's");
        one.WantsFrame(16).Should().BeTrue("the posted work still gets the frame it runs in");

        one.RenderFrame(new DisplayListBuilder(), 16);
        // Whether the queue is empty after the frame is the process's to say, and late work of another test could
        // fill it again: the case reads its own work instead.
        ran.Should().BeTrue("the frame ran the posted work at its top");
        two.NeedsRender.Should().BeFalse("and the other window's tree never changed");
    }

    [Fact]
    public void TheContentCachesAreDroppedOnce_OnTheRenderThread()
    {
        var rasterizer = new CountingRasterizer();
        var host = new PhotonHost(new Counter(), PhotonTheme.Instance, ThemeMode.Light, 400, 300)
        {
            TextRasterizer = rasterizer,
        };

        host.RenderFrame(new DisplayListBuilder());
        var first = rasterizer.Calls;
        first.Should().BeGreaterThan(0);

        host.RenderFrame(new DisplayListBuilder());
        rasterizer.Calls.Should().Be(first, "an unchanged frame is served from the cache");

        PhotonHotReload.ClearCache(null);
        PhotonHotReload.UpdateApplication(null);
        host.RenderFrame(new DisplayListBuilder());
        rasterizer.Calls.Should().Be(first * 2,
            "the same content re-rasters fresh exactly once after an edit — a patched rasterizer "
            + "must not keep serving yesterday's pixels");

        host.RenderFrame(new DisplayListBuilder());
        rasterizer.Calls.Should().Be(first * 2, "and the frame after that is cached again");
    }

    /// <summary>
    /// The whole point of the architecture: an edit replaces METHOD BODIES, and state lives in a
    /// path-keyed store rather than in the objects the edit touches — so nothing here needs to do
    /// anything for state to survive, and this test says so on purpose.
    /// </summary>
    [Fact]
    public void StateSurvivesAReload_BecauseNothingHadToMoveIt()
    {
        var counter = new Counter();
        var host = new PhotonHost(counter, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        var frame = host.RenderFrame(new DisplayListBuilder());

        // Three clicks of real state.
        for (var i = 0; i < 3; i++)
        {
            var button = frame.HitRegions.Last();
            host.PressDown(button.Bounds.Center.X, button.Bounds.Center.Y);
            host.PressUp(button.Bounds.Center.X, button.Bounds.Center.Y);
            frame = host.RenderFrame(new DisplayListBuilder());
        }
        counter.Count.Should().Be(3);

        PhotonHotReload.ClearCache(null);
        PhotonHotReload.UpdateApplication(null);
        host.RenderFrame(new DisplayListBuilder());

        counter.Count.Should().Be(3, "the reload redrew the same retained tree");
    }

    [Fact]
    public void ADeadHostCostsNothing_AndCrashesNothing()
    {
        void OpenAndAbandon()
        {
            var host = new PhotonHost(new Counter(), PhotonTheme.Instance, ThemeMode.Light, 100, 100);
            host.RenderFrame(new DisplayListBuilder());
        }
        OpenAndAbandon();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // The registry holds weak references; a collected host is pruned, not invoked.
        var act = () =>
        {
            PhotonHotReload.ClearCache(null);
            PhotonHotReload.UpdateApplication(null);
        };
        act.Should().NotThrow();
    }
}
