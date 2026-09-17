using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web;

/// <summary>A lowered element: a generic <see cref="HtmlElement"/> with an explicit tag — the web
/// realizer's only output shape (mirrors the web SDK's generic div container).</summary>
internal sealed class RealizedElement : HtmlElement, IPseudoStyled, IAdaptiveGated, IScrolledStyled
{
    /// <summary>Spec S6: the size-class gate this element carries (fixed class + media blob).</summary>
    public string? AdaptiveGate { get; set; }

    public RealizedElement(string tag) => Tag = tag;

    public string Tag { get; }

    /// <summary>Spec S5: hover/focus diff declarations, converted to pseudo-variant atomic rules by
    /// the atomizer pass (pseudo-classes need the ATOMIC pipeline — inline styles can't express them).</summary>
    public List<(string Pseudo, string Prop, string Value)> PseudoDeclarations { get; } = new();

    /// <summary>Pinned.ScrolledStyle: declarations gated by the root's <c>eq-scrolled</c> class
    /// (the runtime scroll listener) — converted by the atomizer like pseudo variants.</summary>
    public List<(string Prop, string Value)> ScrolledDeclarations { get; } = new();

    /// <summary>Attributes emitted VERBATIM (no data- prefix) — SVG needs viewBox/fill/d as-is.</summary>
    public Dictionary<string, string>? RawAttributes { get; set; }

    public override HtmlNode Render()
    {
        var children = Children.Select(c => c.Render()).ToList();
        if (!string.IsNullOrEmpty(InnerHtml))
            children.Insert(0, HtmlNode.Text(InnerHtml));

        var attributes = BuildAttributes();
        if (RawAttributes != null)
        {
            foreach (var raw in RawAttributes) attributes[raw.Key] = raw.Value;
        }

        return new HtmlNode
        {
            Tag = Tag,
            Key = Key,
            Attributes = attributes,
            Events = BuildEvents(),
            Children = children,
        };
    }
}
