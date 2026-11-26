using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Photon's side of the threading contract: work posted from anywhere runs at the top of a frame,
/// on the thread that draws, and an idle window wakes up to run it.
/// </summary>
public class UiDispatcherTests : IDisposable
{
    private sealed class Screen : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("x", TypeRole.BodyM);
    }

    private static PhotonHost Mount()
    {
        var host = new PhotonHost(new Screen(), PhotonTheme.Instance, ThemeMode.Light, 300, 200);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    // The dispatcher is a PROCESS seam (one UI thread per process), so a test that binds it has to
    // put back what it found — xUnit runs classes in parallel.
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public void Dispose() => UiDispatcher.Current = _outer;

    [Fact]
    public void PostedWork_RunsOnTheRenderThread_AtTheTopOfTheNextFrame()
    {
        var host = Mount();
        int? ranOn = null;
        var renderThread = Environment.CurrentManagedThreadId;

        var worker = new Thread(() => PhotonDispatcher.Shared.Post(() => ranOn = Environment.CurrentManagedThreadId));
        worker.Start();
        worker.Join();

        ranOn.Should().BeNull("posting queues; it never runs the work on the poster's thread");

        host.RenderFrame(new DisplayListBuilder(), 16);

        ranOn.Should().Be(renderThread, "the frame drained it where the tree is built");
    }

    [Fact]
    public void PostingWakesAnIdleWindow_WithoutTellingItsTreeItChanged()
    {
        var host = Mount();
        host.RenderFrame(new DisplayListBuilder(), 16);
        host.WantsFrame(16).Should().BeFalse("a settled screen asks for no frames");

        var worker = new Thread(() => PhotonDispatcher.Shared.Post(() => { }));
        worker.Start();
        worker.Join();

        host.WantsFrame(16).Should().BeTrue(
            "otherwise a scanner's results appear whenever something else next needs a frame");
        // #1239: the work is the process's. It told every live host its own tree had changed, so a host that
        // owed nothing read as owing a frame, and skipped scheduling what it did owe.
        host.NeedsRender.Should().BeFalse("nothing of this host's tree changed");

        host.RenderFrame(new DisplayListBuilder(), 32);
        host.WantsFrame(32).Should().BeFalse("the frame ran the work, and nothing is waiting any more");
    }

    [Fact]
    public void TheRenderThreadIsTheUiThread_AndOthersAreNot()
    {
        Mount();

        PhotonDispatcher.Shared.IsOnUiThread.Should().BeTrue();

        var offThread = true;
        var worker = new Thread(() => offThread = PhotonDispatcher.Shared.IsOnUiThread);
        worker.Start();
        worker.Join();
        offThread.Should().BeFalse();
    }

    [Fact]
    public void WorkPostedByWork_BelongsToTheNextFrame()
    {
        // A component that reschedules itself must not be able to hold one frame open forever.
        var host = Mount();
        var runs = 0;
        void Reschedule()
        {
            runs++;
            if (runs < 3) PhotonDispatcher.Shared.Post(Reschedule);
        }

        PhotonDispatcher.Shared.Post(Reschedule);

        host.RenderFrame(new DisplayListBuilder(), 16);
        runs.Should().Be(1, "this frame drains what was queued when it started, and no more");

        host.RenderFrame(new DisplayListBuilder(), 32);
        runs.Should().Be(2);
    }
}

/// <summary>
/// The two things a PROCESS-STATIC queue must not do: hold dead hosts alive, and pay for its own
/// size every frame.
/// </summary>
public class PhotonDispatcherLifetimeTests
{
    private sealed class Screen : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("x", TypeRole.BodyM);
    }

    [Fact]
    public void AHostThatIsGone_IsCollectable_AndPostingStillWorks()
    {
        // Being reachable from a static must not be a lifetime: an app that opens and closes windows would
        // otherwise keep every one of them, with its whole tree, until the process ends.
        static WeakReference MountAndForget()
        {
            var host = new PhotonHost(new Screen(), PhotonTheme.Instance, ThemeMode.Light, 100, 100);
            host.RenderFrame(new DisplayListBuilder());
            return new WeakReference(host);
        }

        var reference = MountAndForget();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        reference.IsAlive.Should().BeFalse("the dispatcher holds no host, and PhotonHotReload holds them weakly");

        // And posting after the collection reaches nothing that is gone.
        PhotonDispatcher.Shared.Post(() => { });
        PhotonDispatcher.Shared.Drain();
    }

    [Fact]
    public void DrainDoesNotWalkTheQueueToMeasureIt()
    {
        // The guarantee is behavioural, so it is tested behaviourally: everything queued when the
        // frame started runs, nothing queued during it does, and neither depends on a count.
        var host = new PhotonHost(new Screen(), PhotonTheme.Instance, ThemeMode.Light, 100, 100);
        host.RenderFrame(new DisplayListBuilder());

        var ran = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            var index = i;
            PhotonDispatcher.Shared.Post(() =>
            {
                ran.Add(index);
                if (index == 0) PhotonDispatcher.Shared.Post(() => ran.Add(99));
            });
        }

        host.RenderFrame(new DisplayListBuilder(), 16);

        ran.Should().Equal(0, 1, 2, 3, 4);

        host.RenderFrame(new DisplayListBuilder(), 32);
        ran.Should().Equal(0, 1, 2, 3, 4, 99);
    }
}

public class PhotonDispatcherFaultTests
{
    private sealed class Screen : StatelessComponent
    {
        public override VisualNode Build(ComponentContext context) => new Text("x", TypeRole.BodyM);
    }

    [Fact]
    public void AFaultingItem_SurfacesItsFault_AndDoesNotStarveTheNextFrame()
    {
        var host = new PhotonHost(new Screen(), PhotonTheme.Instance, ThemeMode.Light, 100, 100);
        host.RenderFrame(new DisplayListBuilder());
        var ran = new List<string>();

        PhotonDispatcher.Shared.Post(() => throw new InvalidOperationException("boom"));
        PhotonDispatcher.Shared.Post(() => ran.Add("after the fault"));

        var act = () => host.RenderFrame(new DisplayListBuilder(), 16);
        act.Should().Throw<InvalidOperationException>().WithMessage("boom");

        // The item behind the fault was not lost and nothing is wedged: the next frame runs it.
        host.RenderFrame(new DisplayListBuilder(), 32);
        ran.Should().Equal("after the fault");

        PhotonDispatcher.Shared.Post(() => ran.Add("later"));
        host.RenderFrame(new DisplayListBuilder(), 48);
        ran.Should().Equal("after the fault", "later");
    }
}
