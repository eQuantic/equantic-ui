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
    public void PostingWakesAnIdleWindow()
    {
        var host = Mount();
        host.RenderFrame(new DisplayListBuilder(), 16);
        host.NeedsRender.Should().BeFalse("a settled screen asks for no frames");

        PhotonDispatcher.Shared.Post(() => { });

        host.NeedsRender.Should().BeTrue(
            "otherwise a scanner's results appear whenever something else next needs a frame");
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
