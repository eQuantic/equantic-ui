namespace eQuantic.UI.Primitives;

/// <summary>
/// PROGRAMMATIC navigation — the imperative twin of <see cref="Link"/>, for the routes a component
/// decides to take instead of the user clicking them: a command palette opening its highlighted
/// row, a form redirecting after it saves, a menu action that leaves the page.
/// <para>
/// The vocabulary stays declarative wherever a link fits (a Link is focusable, middle-clickable and
/// crawlable — reach for this only when there is no anchor to click). Each target installs its own
/// <see cref="Handler"/>: the web runtime routes through the SPA router (falling back to a full
/// document load), and <c>PhotonHost</c> installs its navigation seam. With no handler installed
/// the call is a no-op rather than a crash — an SSR pass has nowhere to navigate to.
/// </para>
/// </summary>
public static class Navigator
{
    /// <summary>The active target's navigation seam. One surface owns it (a native host, a browser
    /// document); hosts install it as they start.</summary>
    public static Action<string>? Handler { get; set; }

    /// <summary>Navigates to <paramref name="href"/> through the active seam.</summary>
    public static void Go(string href)
    {
        if (string.IsNullOrEmpty(href)) return;
        Handler?.Invoke(href);
    }
}
