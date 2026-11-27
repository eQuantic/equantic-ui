using System.Collections.Concurrent;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Photon's <see cref="IUiDispatcher"/>: a queue drained at the top of every frame, on the thread
/// that draws.
/// <para>
/// Every native shell arrives at the same place — macOS through a run-loop timer, iOS through its
/// display link, Android through the <c>Choreographer</c> — and all three call
/// <c>PhotonHost.RenderFrame</c> on the platform's main thread. So the framework needs ONE
/// dispatcher rather than three: work posted from anywhere is enqueued, and the host runs it before
/// it builds, which is the only moment in a frame when nothing is being read.
/// </para>
/// <para>
/// STATIC, like <c>PhotonHotReload</c> and for the same reason: which thread draws is a fact about
/// the process, and the threads this protects against are precisely the ones no ambient scope
/// reaches (a P/Invoke callback, a pool thread older than the first frame).
/// </para>
/// </summary>
public sealed class PhotonDispatcher : IUiDispatcher
{
    /// <summary>The process's dispatcher. Hosts drain it; the hosting layer hands it to
    /// <see cref="UiDispatcher.Current"/> and to the container as the same instance.</summary>
    public static readonly PhotonDispatcher Shared = new();

    // Not readonly: Drain SWAPS it for an empty one, so posts during a frame land in the next.
    private ConcurrentQueue<Action> _queue = new();
    private int _uiThreadId;

    /// <summary>
    /// Whether work is waiting for a frame. A queue nobody drains is a queue that runs whenever something
    /// else happens to need a frame, a scanner's results appearing on the next mouse move, so every shell
    /// asks this beside its own host's state, through <c>PhotonHost.WantsFrame</c>, and the next frame of
    /// any host drains it.
    /// <para>
    /// It is the PROCESS's, never a host's (#1239). A post used to mark every live host as changed, so
    /// an editor's fold answer, posted from a timer's thread, told every other window, and every test's
    /// host in the same process, that its own tree had changed. A host's <c>NeedsRender</c> says what
    /// its own tree asked for, and the frame the work's <c>SetState</c> needs is asked by the position it
    /// marks, from its own host.
    /// </para>
    /// </summary>
    public bool HasWork => !Volatile.Read(ref _queue).IsEmpty;

    /// <summary>
    /// Declares the CALLING thread the one that draws. The host says this from inside its own frame
    /// rather than the app saying it at startup, because the thread that constructs a host is not
    /// always the thread that renders it (a shell may build on one and present on another), and the
    /// only thread whose identity matters is the one that actually got here.
    /// </summary>
    public void BindToCurrentThread() => _uiThreadId = Environment.CurrentManagedThreadId;

    /// <summary>False until a frame has run: before the first one there is no UI thread yet, and
    /// answering "yes" to every thread would let the race through on the very first state change.</summary>
    public bool IsOnUiThread =>
        _uiThreadId != 0 && Environment.CurrentManagedThreadId == _uiThreadId;

    /// <summary>Queues <paramref name="work"/> for the top of the next frame, which every shell asks for
    /// while it waits (<see cref="HasWork"/>). The frame builds what the work marks: what it changes it says
    /// with a <c>SetState</c>, as on the web.</summary>
    public void Post(Action work) => _queue.Enqueue(work);

    /// <summary>
    /// Runs what was posted, on this thread. Drains only what was queued WHEN IT STARTED: work
    /// posted by the work itself belongs to the next frame, so a component that reschedules itself
    /// cannot spin one frame forever.
    /// </summary>
    public void Drain()
    {
        if (_queue.IsEmpty) return;

        // SWAP, not sentinel: this frame's work is whatever was queued when it started, and taking
        // the whole queue out and putting an empty one in says exactly that — a post made by the
        // work itself goes into the new queue and runs next frame. It also costs nothing per item
        // (ConcurrentQueue.Count walks the segments), and it has no marker to get wedged behind.
        var frame = Interlocked.Exchange(ref _queue, new ConcurrentQueue<Action>());
        try
        {
            while (frame.TryDequeue(out var work)) work();
        }
        catch
        {
            // A faulting item must not take the rest of the frame's work with it, and must not
            // leave anything stranded in a queue nobody reads again: what it had not reached goes
            // back to the FRONT of the live queue, ahead of anything posted meanwhile, so order
            // among the survivors is kept. Then the fault surfaces where it always did.
            if (!frame.IsEmpty)
            {
                var live = Interlocked.Exchange(ref _queue, new ConcurrentQueue<Action>());
                while (frame.TryDequeue(out var remaining)) _queue.Enqueue(remaining);
                while (live.TryDequeue(out var posted)) _queue.Enqueue(posted);
            }
            throw;
        }
    }
}
