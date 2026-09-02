namespace eQuantic.UI.Primitives;

/// <summary>
/// The thread a UI may be touched from, and the way onto it.
/// <para>
/// A screen is built, laid out and drawn on ONE thread — the platform's main thread on every native
/// target, and the only thread there is in a browser. Everything else an app does happens elsewhere:
/// a scanner walking a disk, a download, a database read. When one of those finishes it wants to
/// show what it found, and <see cref="UiComponent.SetState"/> is how — but a mutation from a worker
/// thread lands in the component's fields while the render thread is reading that very tree, which
/// is a data race with no error message: a frame drawn from half-old state, a collection enumerated
/// while it grows, a crash that reproduces once a week.
/// </para>
/// <para>
/// So <c>SetState</c> asks this first. Off the UI thread, the whole mutation is POSTED and runs on
/// the next frame, before anything is built; on the UI thread it runs inline, exactly as it always
/// did. An app does not opt in and cannot forget: the safety belongs to the framework, not to the
/// discipline of whoever wrote the background work.
/// </para>
/// <para>
/// Where there is no other thread there is nothing to marshal, and the honest answer is to register
/// nothing: on the web <c>SetState</c> stays inline because JavaScript has one thread, and during
/// server rendering because a request is rendered on the thread that asked. Both still offer this
/// capability for a page that wants to <see cref="Post"/> work — it simply reports itself already
/// on the UI thread.
/// </para>
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Whether the CALLING thread is the one that builds and draws. False is the only
    /// answer that costs anything: it is what makes <c>SetState</c> marshal instead of mutate.</summary>
    bool IsOnUiThread { get; }

    /// <summary>
    /// Runs <paramref name="work"/> on the UI thread, soon — the next frame, not this instant, and
    /// never inline from another thread. Posting from the UI thread itself is legal and still
    /// defers, which is what makes a queue drained during a frame safe to post into.
    /// <para>
    /// Fire-and-forget by design: there is no handle to wait on, because a worker thread that
    /// BLOCKS on the UI thread is the other half of every deadlock this exists to prevent.
    /// </para>
    /// </summary>
    void Post(Action work);
}

/// <summary>
/// The dispatcher in force for this PROCESS, and the reason it is not the ambient
/// <see cref="CapabilityScope"/>: that one is <c>AsyncLocal</c>, which flows into tasks started
/// from a render but is invisible to a thread the platform hands back from somewhere else — a
/// P/Invoke callback, a pool thread older than the app's first frame. Those are exactly the threads
/// this protects against, so looking it up there must not depend on how the thread was born.
/// <para>
/// A native app arms this once (one container, one surface, one UI thread — the same reasoning that
/// arms <c>CapabilityScope</c> for the process rather than per render), and registers the SAME
/// instance in its service collection, so a component can also take an <see cref="IUiDispatcher"/>
/// through its constructor and get the one that is actually running.
/// </para>
/// </summary>
public static class UiDispatcher
{
    /// <summary>The process's dispatcher, or null where nothing needs marshalling (web, SSR).
    /// Null is not a degraded mode: it is what "there is only one thread here" looks like.</summary>
    public static IUiDispatcher? Current { get; set; }
}
