using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

public class Radio : HtmlElement
{
    public string? Name { get; set; }
    public string? Value { get; set; }
    public bool Checked { get; set; }
    public bool Disabled { get; set; }
    public string? Label { get; set; }
    
    public Action<string>? OnChange { get; set; }

    public override HtmlNode Render()
    {
        var inputAttrs = BuildAttributes();
        inputAttrs["type"] = "radio";
        if (Name != null) inputAttrs["name"] = Name;
        if (Value != null) inputAttrs["value"] = Value;
        if (Checked) inputAttrs["checked"] = "true";
        if (Disabled) inputAttrs["disabled"] = "true";
        
        // Filter out container-specific styles from input if wrapped
        // For simplicity, we render a label wrapping the input
        
        var inputNode = new HtmlNode
        {
            Tag = "input",
            Attributes = inputAttrs,
            Events = BuildEvents()
        };

        if (Label != null)
        {
            return new HtmlNode
            {
                Tag = "label",
                Attributes = new Dictionary<string, string?> { ["class"] = "radio-label" },
                Children = { 
                    inputNode,
                    HtmlNode.Text(" " + Label)
                }
            };
        }

        return inputNode;
    }
}
