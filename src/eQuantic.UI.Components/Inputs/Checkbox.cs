using System;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

public class Checkbox : InputComponent<bool>
{
    public bool Checked { get => Value; set => Value = value; }
    public bool Disabled { get; set; }

    public override HtmlNode Render()
    {
        var inputAttrs = BuildAttributes();
        inputAttrs["type"] = "checkbox";
        if (Value) inputAttrs["checked"] = "true";
        if (Disabled) inputAttrs["disabled"] = "true";

        var events = BuildEvents();

        return new HtmlNode
        {
            Tag = "input",
            Attributes = inputAttrs,
            Events = events
        };
    }
}
