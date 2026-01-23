using System;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

public class Slider : HtmlElement
{
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 100;
    public double Step { get; set; } = 1;
    public double Value { get; set; }
    
    public Action<double>? OnChange { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        attrs["type"] = "range";
        attrs["min"] = Min.ToString();
        attrs["max"] = Max.ToString();
        attrs["step"] = Step.ToString();
        attrs["value"] = Value.ToString();
        
        return new HtmlNode
        {
            Tag = "input",
            Attributes = attrs,
            Events = BuildEvents()
        };
    }
}
