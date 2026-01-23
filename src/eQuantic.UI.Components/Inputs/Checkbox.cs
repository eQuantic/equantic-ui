using System;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

public class Checkbox : HtmlElement
{
    public bool Checked { get; set; }
    public Action<bool>? OnChange { get; set; }
    public bool Disabled { get; set; }

    public override HtmlNode Render()
    {
        var inputAttrs = BuildAttributes();
        inputAttrs["type"] = "checkbox";
        if (Checked) inputAttrs["checked"] = "true";
        if (Disabled) inputAttrs["disabled"] = "true";

        // Map change event to OnChange
        // The compiler/runtime needs to handle value extraction for "checked".
        var events = BuildEvents();

        return new HtmlNode
        {
            Tag = "input",
            Attributes = inputAttrs,
            Events = events
        };
    }
}
