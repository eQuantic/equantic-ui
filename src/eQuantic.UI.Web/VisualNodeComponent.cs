
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>
/// The Core⇄Shared bridge (unification slice 1, docs/SHARED-COMPONENTS-PLAN.md): lets a Core page
/// compose WRITE-ONCE components — <c>new VisualNodeComponent(new Card(...))</c> — anywhere an
/// <see cref="IComponent"/> fits. Server-side it lowers the abstract subtree through
/// <see cref="WebRealizer"/> (SSR); client-side the transpiled call resolves to the runtime's mirror
/// class, which lowers with the ambient theme — the two productions are the hydration-parity pair
/// the cross-pinned suites guarantee. The DOM stays MODE-FREE (light-dark()), so SSR needs no theme
/// mode — only the token source, defaulting to <see cref="PhotonTheme.Instance"/>.
/// </summary>
[Primitives.RuntimeProvided]
public sealed class VisualNodeComponent : HtmlElement
{
    private readonly IAppTheme _theme;
    private readonly float _typeScale;
    private readonly Density _density;

    /// <param name="node">The write-once subtree this component lowers.</param>
    /// <param name="theme">The token source; <see cref="PhotonTheme.Instance"/> when none is given.</param>
    /// <param name="typeScale">The type scale the subtree is built at.</param>
    /// <param name="density">The density the subtree is built at: the request's, which the browser
    /// reports (#623), so the client hydrates markup built for the pointer it has.</param>
    public VisualNodeComponent(VisualNode node, IAppTheme? theme = null, float typeScale = 1f,
        Density density = Density.Comfortable)
    {
        Node = node;
        _theme = theme ?? PhotonTheme.Instance;
        _typeScale = typeScale;
        _density = density;
    }

    /// <summary>The wrapped abstract subtree — hosts unwrap it (e.g. the SSR pipeline probing the
    /// actual page instance for <c>IHandleMetadata</c>).</summary>
    public VisualNode Node { get; }

    /// <summary>The atomic rules this component's markup references (docs/STYLE-SEMANTICS-PLAN.md §2)
    /// — populated by <see cref="Render"/>; the SSR pipeline injects it as a style tag so the client
    /// hydrates against the exact rules the markup names. When the SSR pipeline armed an ambient
    /// sink (<see cref="StyleSink.Ambient"/>), rules flow there instead — one rule set per PAGE,
    /// shared by every bridge the page composes.</summary>
    public StyleSink Styles { get; } = new();

    public override HtmlNode Render() =>
        WebRealizer.Lower(Node, _theme, _typeScale, StyleSink.Ambient ?? Styles, _density).Render();
}
