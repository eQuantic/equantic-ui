using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

public class DynamicElement : HtmlElement
{
    public string TagName { get; set; } = "div";
    public string? InnerText { get; set; }
    public Dictionary<string, string> CustomAttributes { get; set; } = new();
    public Dictionary<string, Delegate> CustomEvents { get; set; } = new();

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

        return new HtmlNode
        {
            Tag = TagName,
            Attributes = CustomAttributes,
            Events = CustomEvents,
            Children = children
        };
    }
}