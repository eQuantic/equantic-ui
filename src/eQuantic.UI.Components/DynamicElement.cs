using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

public class DynamicElement : HtmlElement
{
    public string TagName { get; set; } = "div";
    public Dictionary<string, string> CustomAttributes { get; set; } = new();
    public Dictionary<string, Delegate> CustomEvents { get; set; } = new();

    public override HtmlNode Render()
    {
        return new HtmlNode
        {
            Tag = TagName,
            Attributes = CustomAttributes,
            Events = CustomEvents,
            Children = Children.Select(c => c.Render()).ToList()
        };
    }
}