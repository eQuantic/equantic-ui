using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server.Rendering;

/// <summary>
/// Where the app's layout places the page (<see cref="UIOptions.UseLayout{TLayout}"/>): the node the
/// layout is handed as its body, which builds the page. The server renders a page under a layout as
/// <c>new TLayout(new PageSlot(page))</c>.
/// <para>
/// A slot rather than the page itself, because of what the browser does with it. There the layout is
/// built ONCE, around one slot, and a navigation hands the slot the next page: the layout's position
/// keeps its instance and builds again, and the page is replaced under it. Handing the layout the page
/// would hand it a new configuration on every navigation, which a stateful layout learns only through
/// <c>AdoptConfig</c>, and one that did not override it would show its first page for ever.
/// <em>How does Flutter solve it?</em> go_router's <c>ShellRoute</c> hands the shell a nested
/// navigator, which stays, and the pages change inside it.
/// </para>
/// <para>
/// The server builds the same slot so the two trees have the same positions: every component's server
/// data is keyed by where it stands (#1477), and the slot is a step of the path to everything the page
/// builds. The browser's twin is the runtime's <c>PageSlot</c>, which carries this type's identity.
/// </para>
/// </summary>
internal sealed class PageSlot(VisualNode page) : StatelessComponent
{
    /// <summary>The page this slot shows.</summary>
    public VisualNode Page { get; } = page;

    /// <inheritdoc />
    public override VisualNode Build(ComponentContext context) => Page;
}
