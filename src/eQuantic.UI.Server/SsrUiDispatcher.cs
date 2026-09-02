using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server;

/// <summary>
/// <see cref="IUiDispatcher"/> during server rendering: the thread that asked IS the UI thread.
/// <para>
/// A request is rendered synchronously on the thread handling it, so there is no other thread to
/// marshal from and nothing for <c>SetState</c> to defer — it runs inline, which is what it did
/// before this capability existed. What this adds is an honest answer for a page that ASKS: posted
/// work runs immediately rather than being queued for a frame that will never come, because a
/// response is sent and the render is over.
/// </para>
/// <para>
/// Registered in the container only. The process-wide <see cref="UiDispatcher.Current"/> seam stays
/// unarmed on a server on purpose: it names one UI thread for a process, and a server has a thread
/// per request — arming it would be a claim that is false the moment two requests overlap.
/// </para>
/// </summary>
public sealed class SsrUiDispatcher : IUiDispatcher
{
    public bool IsOnUiThread => true;

    public void Post(Action work) => work();
}
