namespace eQuantic.UI.Primitives;

/// <summary>
/// PROGRAMMATIC navigation — the imperative twin of <see cref="Link"/>, for the routes a component
/// decides to take instead of the user clicking them: a command palette opening its highlighted
/// row, a form redirecting after it saves, a menu action that leaves the page.
/// <para>
/// The vocabulary stays declarative wherever a link fits (a Link is focusable, middle-clickable and
/// crawlable — reach for this only when there is no anchor to click). A component asks for the
/// navigator of the tree it is in, <c>Navigator.Of(context).Go(destination)</c>: the host found from
/// its position (docs/ELEMENT-TREE-PLAN.md, 3.5), so two windows in one process each navigate their
/// own, where one static handler let the last host to start take every request. The web runtime
/// routes through the SPA router. With nowhere to go (a server's render, a context with no position)
/// the call is a no-op rather than a crash.
/// </para>
/// </summary>
public sealed class Navigator
{
    private readonly Action<string>? _go;

    private Navigator(Action<string>? go, string location)
    {
        _go = go;
        Location = location;
    }

    /// <summary>The navigator of the tree <paramref name="context"/>'s component is in.</summary>
    public static Navigator Of(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new Navigator(context.Navigation, context.Route.Path);
    }

    /// <summary>
    /// The path of the page being shown: <c>/charts</c>, with no query and no fragment. An app's layout
    /// builds around every page, and marks the destination it shows with this (the entry of a sidebar
    /// whose destination equals it). The server answers the path of the request it renders, the
    /// browser the path in the address, which a navigation updates before anything builds for it.
    /// </summary>
    public string Location { get; }

    /// <summary>
    /// Navigates to <paramref name="destination"/>.
    /// <para>
    /// The parameter was called <c>href</c>, which is the DOM's word in the assembly whose rule is
    /// that no name here would exist if the web did not. <c>destination</c> is what
    /// <see cref="Link"/> calls the same thing, and this is its imperative twin — a route is a
    /// route on a target with no anchors at all.
    /// </para>
    /// </summary>
    public void Go(string destination)
    {
        if (string.IsNullOrEmpty(destination)) return;
        _go?.Invoke(destination);
    }
}
