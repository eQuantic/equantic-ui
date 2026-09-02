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

    // Hosts to wake when work arrives — a queue nobody wakes is a queue that runs whenever
    // something else happens to need a frame. WEAK, because this is a process static: an event with
    // a closure over `this` would pin every host the app ever built, for the life of the app.
    private readonly List<WeakReference<PhotonHost>> _waking = [];

    /// <summary>Wakes <paramref name="host"/> whenever work is posted, for as long as it lives —
    /// the same weak contract <c>PhotonHotReload.Register</c> makes, and for the same reason.</summary>
    public void WakeOnPost(PhotonHost host)
    {
        lock (_waking) _waking.Add(new WeakReference<PhotonHost>(host));
    }

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
        Wake();
    }

    /// <summary>Asks every live host for a frame, forgetting the ones that are gone — the sweep is
    /// nothing next to the frame it is asking for, and this is the only place a dead entry shows.</summary>
    private void Wake()
    {
        lock (_waking)
        {
            for (var i = _waking.Count - 1; i >= 0; i--)
            {
                if (_waking[i].TryGetTarget(out var host)) host.Invalidate();
                else _waking.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Runs what was posted, on this thread. Drains only what was queued WHEN IT STARTED: work
    /// posted by the work itself belongs to the next frame, so a component that reschedules itself
    /// cannot spin one frame forever.
    /// </summary>
    public void Drain()
    {
        // A SENTINEL marks where this frame's work ends, rather than a count: ConcurrentQueue.Count
        // walks the queue's segments, so capping by it paid a per-frame cost proportional to how
        // much had been posted — on the one thread that must not be doing arithmetic about itself.
        // The marker goes in first and stops the loop when it comes back out, so work posted BY
        // work still belongs to the next frame.
        if (_queue.IsEmpty) return;
        _queue.Enqueue(EndOfFrame);
        while (_queue.TryDequeue(out var work))
        {
            if (ReferenceEquals(work, EndOfFrame)) return;
            work();
        }
    }

    /// <summary>Not work: the marker <see cref="Drain"/> enqueues to find its own end. Identity is
    /// the whole point, so it is one instance, compared by reference and never invoked.</summary>
    private static readonly Action EndOfFrame = () => { };
}
