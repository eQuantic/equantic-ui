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
            children.Insert(0, HtmlNode.Text(InnerText));
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