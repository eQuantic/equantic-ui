using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

/// <summary>
/// Link component
/// </summary>
public class Link : HtmlElement
{
    /// <summary>
    /// Target URL
    /// </summary>
    public string Href { get; set; } = "#";

    /// <summary>
    /// Target attribute (_blank, _self, etc.)
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Link text
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Opt into SPA hover/focus prefetch: the client router warms the target page's bundle on
    /// pointer-enter/focus so the subsequent click navigates instantly. Default true. Emitted as a
    /// <c>data-prefetch</c> marker the router's delegated listener reads; the router still only prefetches
    /// same-origin links that match a known route, so external / new-tab links are ignored regardless.
    /// </summary>
    public bool Prefetch { get; set; } = true;

    /// <inheritdoc />
    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        attrs["href"] = Href;
        if (Target != null) attrs["target"] = Target;
        if (Target == "_blank") attrs["rel"] = "noopener noreferrer";
        // Mark for SPA prefetch unless opted out or opening in a new tab/window.
        if (Prefetch && Target != "_blank") attrs["data-prefetch"] = "true";

        var children = Children.Any()
            ? Children.Select(c => c.Render()).ToList()
            : Text != null
                ? new List<HtmlNode> { HtmlNode.Text(Text) }
                : new List<HtmlNode>();

        return new HtmlNode
        {
            Tag = "a",
            Attributes = attrs,
            Events = BuildEvents(),
            Children = children
        };
    }
}