using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

/// <summary>
/// A generic container rendering a simple div element.
/// Unlike Container, Box has no default styles (padding, margin, width).
/// </summary>
public class Box : HtmlElement
{
    /// <inheritdoc />
    public override HtmlNode Render()
    {
        return new HtmlNode
        {
            Tag = "div",
            Attributes = BuildAttributes(),
            Events = BuildEvents(),
            Children = Children.Select(c => c.Render()).ToList()
        };
    }
}
