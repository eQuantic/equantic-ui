using eQuantic.UI.Core;
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
[RuntimeProvided]
public sealed class VisualNodeComponent : HtmlElement
{
    private readonly VisualNode _node;
    private readonly IAppTheme _theme;
    private readonly float _typeScale;

    public VisualNodeComponent(VisualNode node, IAppTheme? theme = null, float typeScale = 1f)
    {
        _node = node;
        _theme = theme ?? PhotonTheme.Instance;
        _typeScale = typeScale;
    }

    public override HtmlNode Render() => WebRealizer.Lower(_node, _theme, _typeScale).Render();
}
