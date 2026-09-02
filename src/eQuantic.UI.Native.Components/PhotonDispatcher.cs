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

    private readonly ConcurrentQueue<Action> _queue = new();
    private int _uiThreadId;

    /// <summary>Raised when work arrives, so an IDLE window comes back to drain it — a queue nobody
    /// wakes is a queue that runs whenever something else happens to need a frame.</summary>
    public event Action? WorkPosted;

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

    public void Post(Action work)
    {
        _queue.Enqueue(work);
        WorkPosted?.Invoke();
    }

    /// <summary>
    /// Runs what was posted, on this thread. Drains only what was queued WHEN IT STARTED: work
    /// posted by the work itself belongs to the next frame, so a component that reschedules itself
    /// cannot spin one frame forever.
    /// </summary>
    public void Drain()
    {
        for (var remaining = _queue.Count; remaining > 0 && _queue.TryDequeue(out var work); remaining--)
            work();
    }
}
