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
        var events = new Dictionary<string, Delegate>();
        if (OnChange != null) events["change"] = OnChange;

        var inputElement = new Box
        {
            As = "input",
            Type = "radio",
            ClassName = ClassName,
            Name = Name,
            Value = Value,
            Checked = Checked,
            Disabled = Disabled,
            CustomEvents = events
        };

        if (Label != null)
        {
            var labelElement = new Box
            {
                As = "label",
                ClassName = "eq-checkbox-label",
                Children = { inputElement, new Text(Label) }
            };
            return labelElement;
        }

        return inputElement;
    }
}
