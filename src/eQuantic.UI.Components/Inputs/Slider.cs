using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

public class Slider : InputComponent<double>
{
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 100;
    public double Step { get; set; } = 1;

    public override IComponent Build(RenderContext context)
    {
        var attrs = new Dictionary<string, string>
        {
            ["type"] = "range",
            ["min"] = Min.ToString(),
            ["max"] = Max.ToString(),
            ["step"] = Step.ToString(),
            ["class"] = ClassName
        };
        
        attrs["value"] = Value.ToString();

        var events = BuildEvents();
        if (OnChange != null)
        {
             // Map OnChange logic if needed, or rely on bind
             events["change"] = OnChange;
             events["input"] = OnChange; // Sliders often update on input
        }

        return new DynamicElement
        {
            TagName = "input",
            CustomAttributes = attrs,
            CustomEvents = events
        };
    }
}
