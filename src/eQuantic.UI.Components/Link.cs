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

    /// <inheritdoc />
    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        attrs["href"] = Href;
        if (Target != null) attrs["target"] = Target;
        if (Target == "_blank") attrs["rel"] = "noopener noreferrer";

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