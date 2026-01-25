using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

public class Radio : InputComponent<string>
{
    public string? Name { get; set; }
    public bool Checked { get; set; }
    public bool Disabled { get; set; }
    public string? Label { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var inputAttrs = new Dictionary<string, string>
        {
            ["type"] = "radio",
            ["class"] = ClassName
        };

        if (Name != null) inputAttrs["name"] = Name;
        if (Value != null) inputAttrs["value"] = Value;
        if (Checked) inputAttrs["checked"] = "true";
        if (Disabled) inputAttrs["disabled"] = "true";

        var events = BuildEvents();
        if (OnChange != null) events["change"] = OnChange;

        var inputElement = new DynamicElement
        {
            TagName = "input",
            CustomAttributes = inputAttrs,
            CustomEvents = events
        };

        if (Label != null)
        {
            var labelElement = new DynamicElement
            {
                TagName = "label",
                CustomAttributes = new Dictionary<string, string> { ["class"] = "radio-label flex items-center gap-2" }
            };
            labelElement.Children.Add(inputElement);
            labelElement.Children.Add(new Text(Label));
            return labelElement;
        }

        return inputElement;
    }
}
