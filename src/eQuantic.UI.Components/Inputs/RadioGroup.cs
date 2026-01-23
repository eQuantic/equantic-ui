using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;
using eQuantic.UI.Components.Layout;

namespace eQuantic.UI.Components.Inputs;

public class RadioGroup : HtmlElement
{
    public string Name { get; set; } = Guid.NewGuid().ToString("N");
    public string? Value { get; set; }
    public List<RadioOption> Options { get; set; } = new();
    public FlexDirection Direction { get; set; } = FlexDirection.Column;
    
    public Action<string>? OnChange { get; set; }

    public override HtmlNode Render()
    {
        var attrs = BuildAttributes();
        
        // Container style
        var style = new HtmlStyle 
        { 
            Display = eQuantic.UI.Core.Display.Flex, 
            FlexDirection = Direction,
            Gap = "0.5rem"
        };
        
        var existingStyle = attrs.TryGetValue("style", out var s) ? s + "; " : "";
        attrs["style"] = existingStyle + style.ToCssString();

        var children = new List<HtmlNode>();

        if (Options.Any())
        {
            foreach (var opt in Options)
            {
                children.Add(new Radio
                {
                    Name = Name,
                    Value = opt.Value,
                    Label = opt.Label,
                    Disabled = opt.Disabled,
                    Checked = Value == opt.Value,
                    OnChange = OnChange
                }.Render());
            }
        }
        else
        {
            // If explicit children are used, we might need to inject Name/Checked state
            // This is harder in static render. For now assuming Options usage is primary.
            children.AddRange(Children.Select(c => c.Render()));
        }

        return new HtmlNode
        {
            Tag = "div",
            Attributes = attrs,
            Children = children
        };
    }
}
