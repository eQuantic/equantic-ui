using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

/// <summary>
/// A generic container rendering a simple div element.
/// Unlike Container, Box has no default styles (padding, margin, width).
/// </summary>
public class Box : HtmlElement
{
    /// <summary>
    /// The HTML tag to render. Defaults to 'div'.
    /// </summary>
    public string As { get; set; } = "div";

    /// <inheritdoc />
    public override HtmlNode Render()
    {
        var children = Children.Select(c => c.Render()).ToList();

        // Textual content (set e.g. by the Text component). Rendered as an
        // escaped #text node so user-provided strings are never treated as raw HTML.
        if (!string.IsNullOrEmpty(InnerHtml))
        {
            children.Insert(0, HtmlNode.Text(InnerHtml));
        }

        return new HtmlNode
        {
            Tag = As,
            Attributes = BuildAttributes(),
            Events = BuildEvents(),
            Children = children
        };
    }
}
