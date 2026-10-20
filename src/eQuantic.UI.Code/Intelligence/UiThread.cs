using eQuantic.UI.Primitives;

namespace eQuantic.UI.Code;

/// <summary>
/// Where an editor's sessions apply what a provider answered. An answer completed on another thread is
/// POSTED to the UI thread, as <c>SetState</c> posts a mutation, because what it changes (a list, a card,
/// the problems) is what the render thread is reading; where there is one thread (the browser) or the
/// answer came back on the UI thread, it runs at once.
/// </summary>
internal static class UiThread
{
    internal static void Run(Action work)
    {
        if (UiDispatcher.Current is { IsOnUiThread: false } dispatcher) dispatcher.Post(work);
        else work();
    }
}
