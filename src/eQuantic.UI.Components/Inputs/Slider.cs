using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

public class Slider : InputComponent<double>
{
    public new double Min { get; set; } = 0;
    public new double Max { get; set; } = 100;
    public new double Step { get; set; } = 1;

    public override IComponent Build(RenderContext context)
    {
        var events = new Dictionary<string, Delegate>();
        if (OnChange != null) events["change"] = OnChange;

        return new Box
        {
            As = "input",
            Type = "range",
            Min = Min,
            Max = Max,
            Step = Step,
            ClassName = ClassName,
            Value = Value.ToString(),
            CustomEvents = events
        };
    }
}
