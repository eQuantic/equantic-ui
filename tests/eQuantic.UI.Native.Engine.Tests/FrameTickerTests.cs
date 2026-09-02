using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>The frame clock on Photon: fires before each frame with the frame's time, keeps frames
/// flowing while subscribed, and stops costing anything the moment the last subscriber leaves.</summary>
public class FrameTickerTests
{
    private sealed class Screen : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("x", TypeRole.BodyM);
    }

    private static PhotonHost Mount()
    {
        var host = new PhotonHost(new Screen(), PhotonTheme.Instance, ThemeMode.Light, 100, 100);
        host.RenderFrame(new DisplayListBuilder(), 0);
        return host;
    }

    [Fact]
    public void FiresBeforeEveryFrame_WithTheFramesTime_AndTheDeltaSinceItsLast()
    {
        var host = Mount();
        var ticks = new List<FrameTick>();
        using var subscription = PhotonFrameTicker.Shared.OnFrame(ticks.Add);

        host.RenderFrame(new DisplayListBuilder(), 100);
        host.RenderFrame(new DisplayListBuilder(), 116);
        host.RenderFrame(new DisplayListBuilder(), 133);

        ticks.Select(t => t.TimeMs).Should().Equal(100, 116, 133);
        ticks.Select(t => t.DeltaMs).Should().Equal(0, 16, 17);
    }

    [Fact]
    public void ASubscriptionKeepsFramesFlowing_AndDisposingLetsTheLoopIdle()
    {
        var host = Mount();
        host.RenderFrame(new DisplayListBuilder(), 16);
        host.NeedsRender.Should().BeFalse("a settled screen asks for nothing");

        var subscription = PhotonFrameTicker.Shared.OnFrame(_ => { });
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.NeedsRender.Should().BeTrue("someone moves every frame");

        subscription.Dispose();
        subscription.Dispose();   // twice is fine
        host.RenderFrame(new DisplayListBuilder(), 48);
        host.NeedsRender.Should().BeFalse("nobody does any more");
    }

    [Fact]
    public void DisposingInsideTheCallback_TakesEffectNextFrame_AndNeverBreaksTheDelivery()
    {
        var host = Mount();
        var count = 0;
        IDisposable? self = null;
        var other = 0;
        self = PhotonFrameTicker.Shared.OnFrame(_ => { count++; self!.Dispose(); });
        using var second = PhotonFrameTicker.Shared.OnFrame(_ => other++);

        host.RenderFrame(new DisplayListBuilder(), 100);
        host.RenderFrame(new DisplayListBuilder(), 116);

        count.Should().Be(1, "it left after its first tick");
        other.Should().Be(2, "and the one beside it was delivered both frames");
    }

    [Fact]
    public void SetStateInsideTheTick_IsDrawnByThatVeryFrame()
    {
        // The tick runs before the tree is built, on the UI thread: the state it sets is this
        // frame's state, not the next one's — the contract that makes per-frame motion coherent.
        var counter = new TickCounter();
        var host = new PhotonHost(counter, PhotonTheme.Instance, ThemeMode.Light, 100, 100);
        host.RenderFrame(new DisplayListBuilder(), 0);
        using var subscription = PhotonFrameTicker.Shared.OnFrame(t => counter.Advance(t.TimeMs));

        host.RenderFrame(new DisplayListBuilder(), 250);

        counter.LastBuiltWith.Should().Be(250, "the frame drew what its own tick set");
    }

    private sealed class TickCounter : StatefulComponent
    {
        private float _timeMs;
        public float LastBuiltWith;
        public void Advance(float t) => SetState(() => _timeMs = t);
        public override VisualNode Build(ComponentContext context)
        {
            LastBuiltWith = _timeMs;
            return new Text($"{_timeMs}", TypeRole.BodyM);
        }
    }
}
