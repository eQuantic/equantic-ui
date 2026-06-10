using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

public class DynamicElement : HtmlElement
{
    public string? InnerText { get; set; }

    /// <summary>
    /// The HTML tag name to render. Inherited As feature conceptually mapped to TagName for backwards compatibility.
    /// </summary>
    public string TagName { get; set; } = "div";

    /// <summary>
    /// Arbitrary attributes keyed by name. <see cref="DynamicElement"/> is the escape hatch for custom
    /// tags and web components (e.g. <c>&lt;dotlottie-player&gt;</c>, raw <c>&lt;script&gt;</c>), so it
    /// carries attributes the strongly-typed <see cref="HtmlElement"/> properties don't model. These are
    /// merged over the typed attributes at render time (custom keys win on a clash).
    /// </summary>
    public Dictionary<string, string>? CustomAttributes { get; set; }

    public override HtmlNode Render()
    {
        var children = Children.Select(c => c.Render()).ToList();

        if (!string.IsNullOrEmpty(InnerText))
        {
            var isRaw = TagName.Equals("script", StringComparison.OrdinalIgnoreCase) ||
                       TagName.Equals("style", StringComparison.OrdinalIgnoreCase);

            children.Insert(0, new HtmlNode
            {
                Tag = isRaw ? "#raw" : "#text",
                TextContent = InnerText
            });
        }

        var attributes = BuildAttributes();
        if (CustomAttributes != null)
        {
            foreach (var (key, value) in CustomAttributes)
            {
                attributes[key] = value;
            }
        }

        return new HtmlNode
        {
            Tag = TagName,
            Attributes = attributes,
            Events = BuildEvents(),
            Children = children
        };
    }
}