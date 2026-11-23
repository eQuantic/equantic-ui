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
    private readonly IUrlLauncher? _launcher;

    private Navigator(Action<string>? go, string location, IUrlLauncher? launcher)
    {
        _go = go;
        Location = location;
        _launcher = launcher;
    }

    /// <summary>The navigator of the tree <paramref name="context"/>'s component is in.</summary>
    public static Navigator Of(ComponentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new Navigator(context.Navigation, context.Route.Path, context.GetService<IUrlLauncher>());
    }

    /// <summary>
    /// The path of the page being shown, as the app writes it: <c>/charts</c>, with no query, no
    /// fragment and no language segment, so a page served at <c>/pt-BR/charts</c> is at <c>/charts</c>,
    /// the href its own links name (<c>UseCultureRoutes</c> adds the segment as a link is drawn). An
    /// app's layout builds around every page, and marks the destination it shows with this (the entry of
    /// a sidebar whose destination equals it). The server answers the path of the request it renders,
    /// the browser the path in the address, which a navigation updates before anything builds for it.
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

    /// <summary>
    /// Opens <paramref name="address"/> BESIDE the app, rather than navigating it there: an address an
    /// action answered with (a document a server prepared, a payment provider's page) opened from the
    /// handler that awaited it, without a second press on a link (#1787).
    /// <para>
    /// The browser opens it in a new tab, inside the time the press that started the handler still
    /// allows (browsers let a page open one for a few seconds after a person's press), and resolves an
    /// address relative to the page. A Photon shell hands it to the system (<see cref="IUrlLauncher"/>),
    /// under the same <see cref="OpenUrlPolicy"/>, so the address must be absolute there. Only
    /// <c>http</c> and <c>https</c> open in the browser: an address that runs script is never opened.
    /// </para>
    /// </summary>
    /// <returns>Whether it opened: false where the target opens nothing (a server's render), where the
    /// browser blocked the tab because the press was too long ago, or where the scheme is refused.</returns>
    /// <exception cref="ArgumentException">The address is empty, or relative where nothing can resolve it.</exception>
    public ValueTask<bool> OpenAsync(string address)
    {
        ArgumentException.ThrowIfNullOrEmpty(address);
        if (_launcher is null) return ValueTask.FromResult(false);
        // A path from the root is relative to a page, though .NET on macOS and Linux reads `/print/1` as an
        // absolute file URI.
        if (address.StartsWith('/') || !Uri.TryCreate(address, UriKind.Absolute, out var url))
            throw new ArgumentException(
                $"'{address}' is relative, and an app outside a browser has no page to resolve it against. Hand it an absolute address.",
                nameof(address));
        return _launcher.OpenUrlAsync(url);
    }

    /// <summary>
    /// Hands the file at <paramref name="address"/> to the person as a download named
    /// <paramref name="fileName"/>, from the handler that awaited the address (#1787).
    /// <para>
    /// The browser downloads it under that name when the address is the page's own origin, and opens
    /// it in a new tab otherwise, since a browser ignores the name of another origin's file and would
    /// navigate the page away instead. A Photon shell hands the address to the system, whose browser
    /// downloads it as the server names it. A file the app holds in memory is
    /// <see cref="IFileSaver"/>'s.
    /// </para>
    /// </summary>
    /// <returns>Whether the target took it, as <see cref="OpenAsync"/> answers.</returns>
    /// <exception cref="ArgumentException">The address or the name is empty, or the address is relative
    /// where nothing can resolve it.</exception>
    public ValueTask<bool> DownloadAsync(string address, string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        return OpenAsync(address);
    }
}
