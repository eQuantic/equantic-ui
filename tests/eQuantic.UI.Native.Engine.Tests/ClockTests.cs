using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Hosting;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Time as a capability (<see cref="IClock"/>): the first thing a screen can react to that nobody
/// caused. The pair that makes it safe is <c>OnMount</c> subscribing and <c>OnUnmount</c> disposing,
/// and the pair is what these tests are actually about — a timer that outlives its component is a
/// leak that keeps the dead component alive from the timer's side.
/// </summary>
public class ClockTests
{
    /// <summary>A clock a test can advance by hand. The realizations own real time; a COMPONENT's
    /// contract is about subscribing and disposing, and that is testable without waiting.</summary>
    private sealed class FakeClock : IClock
    {
        private readonly List<Action> _ticks = [];

        internal int Subscriptions => _ticks.Count;

        public IDisposable Every(TimeSpan interval, Action onTick)
        {
            _ticks.Add(onTick);
            return new Handle(() => _ticks.Remove(onTick));
        }

        internal void Tick()
        {
            foreach (var tick in _ticks.ToArray()) tick();
        }

        private sealed class Handle(Action stop) : IDisposable
        {
            public void Dispose() => stop();
        }
    }

    /// <summary>
    /// The same carousel, composed by whoever draws it rather than handed anything: it asks for the
    /// clock where it needs it. A section in the middle of a tree has no constructor the router
    /// fills, so this is the shape that actually occurs — and until GetService existed on the
    /// component, it had to ask inside Build (the one method the framework calls repeatedly) behind
    /// a run-once flag.
    /// </summary>
    private sealed class TickingSection : Primitives.StatefulComponent
    {
        private IDisposable? _subscription;
        private int _step;

        internal int Step => _step;

        protected override void OnMount() =>
            _subscription = GetService<IClock>()
                ?.Every(TimeSpan.FromMilliseconds(1700), () => SetState(() => _step++));

        protected override void OnUnmount() => _subscription?.Dispose();

        public override VisualNode Build(ComponentContext context) =>
            new Primitives.Text($"step {_step}", TypeRole.BodyM, context.Theme.TextPrimary);
    }

    /// <summary>OnMount is where a subscription belongs, and it has no context — so the capability
    /// has to be reachable from the component itself, through whatever the host armed.</summary>
    [Fact]
    public void ASectionResolvesItsCapability_InOnMount()
    {
        var clock = new FakeClock();
        CapabilityScope.Current = type => type == typeof(IClock) ? clock : null;
        try
        {
            var section = new TickingSection();
            section.NotifyMounted();
            clock.Subscriptions.Should().Be(1, "the hook is where the subscription starts");

            clock.Tick();
            clock.Tick();
            section.Step.Should().Be(2);

            section.NotifyUnmounted();
            clock.Subscriptions.Should().Be(0, "and OnUnmount still owns letting go");
        }
        finally
        {
            CapabilityScope.Current = null;
        }
    }

    /// <summary>A target without the capability answers null, and a component that asked for one
    /// simply does not tick — the contract's own answer rather than a crash.</summary>
    [Fact]
    public void ASectionWithNoCapability_MountsAnyway()
    {
        CapabilityScope.Current = null;
        var section = new TickingSection();

        var mount = () => section.NotifyMounted();

        mount.Should().NotThrow();
        section.Step.Should().Be(0);
    }

    /// <summary>A carousel, in miniature: it subscribes when it enters the tree, advances on every
    /// tick, and lets go when it leaves.</summary>
    private sealed class Ticking(IClock clock) : Primitives.StatefulComponent
    {
        private IDisposable? _subscription;
        private int _step;

        internal int Step => _step;

        protected override void OnMount() =>
            _subscription = clock.Every(TimeSpan.FromMilliseconds(1700),
                () => SetState(() => _step = (_step + 1) % 4));

        protected override void OnUnmount() => _subscription?.Dispose();

        public override VisualNode Build(ComponentContext context) =>
            new Primitives.Text($"step {_step}", TypeRole.BodyM, context.Theme.TextPrimary);
    }

    private static PhotonHost Open(VisualNode root)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 200, 60);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    [Fact]
    public void ATick_AdvancesTheComponent_AndTheNextFrameShowsIt()
    {
        var clock = new FakeClock();
        var page = new Ticking(clock);
        var host = Open(page);

        clock.Subscriptions.Should().Be(1, "OnMount subscribed once the component entered the tree");
        page.Step.Should().Be(0, "nothing has ticked yet, and the first paint is the initial state");

        clock.Tick();
        page.Step.Should().Be(1);
        host.NeedsRender.Should().BeTrue("a tick's SetState asks for the frame that shows it");

        host.RenderFrame(new DisplayListBuilder());
        clock.Tick();
        clock.Tick();
        page.Step.Should().Be(3);
    }

    /// <summary>The half that is easy to forget and impossible to see: leaving the tree lets the
    /// timer go. Without it every navigation leaves a ticking component behind.</summary>
    [Fact]
    public void LeavingTheTree_LetsTheTimerGo()
    {
        var clock = new FakeClock();
        var page = new Ticking(clock);
        Open(page);

        page.NotifyUnmounted();

        clock.Subscriptions.Should().Be(0, "OnUnmount disposed what OnMount subscribed");
        clock.Tick();
        page.Step.Should().Be(0, "a disposed subscription does not deliver");
    }

    /// <summary>
    /// The REAL native realization, which is .NET's own timer and not a frame-loop invention: it
    /// fires on its own thread, and disposing stops it. Generous windows — this asserts that the
    /// wiring is real, never how punctual a thread pool is.
    /// </summary>
    [Fact]
    public void ThePhotonClock_Fires_AndDisposingStopsIt()
    {
        var clock = new PhotonClock();
        using var fired = new ManualResetEventSlim();
        var ticks = 0;

        var subscription = clock.Every(TimeSpan.FromMilliseconds(20), () =>
        {
            Interlocked.Increment(ref ticks);
            fired.Set();
        });

        fired.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue("the timer fires without anyone pumping it");

        subscription.Dispose();
        var afterDispose = Volatile.Read(ref ticks);
        Thread.Sleep(120);
        Volatile.Read(ref ticks).Should().Be(afterDispose, "disposing stops it for good");
    }
}
