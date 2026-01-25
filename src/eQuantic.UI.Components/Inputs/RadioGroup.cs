using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;
using eQuantic.UI.Components.Layout;

namespace eQuantic.UI.Components.Inputs;

public class RadioGroup : InputComponent<string>
{
    public string Name { get; set; } = Guid.NewGuid().ToString("N");
    public List<RadioOption> Options { get; set; } = new();
    public FlexDirection Direction { get; set; } = FlexDirection.Column;

    public override IComponent Build(RenderContext context)
    {
        var attrs = BuildAttributes();
        
        // Container style
        var style = new HtmlStyle 
        { 
            Display = eQuantic.UI.Core.Display.Flex, 
            FlexDirection = Direction,
            Gap = "0.5rem"
        };
        
        if (attrs.TryGetValue("style", out var existing))
        {
            attrs["style"] = existing + "; " + style.ToCssString();
        }
        else
        {
            attrs["style"] = style.ToCssString();
        }

        var container = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = attrs
        };
        
        if (Options.Any())
        {
            foreach (var opt in Options)
            {
                container.Children.Add(new Radio
                {
                    Name = Name,
                    Value = opt.Value,
                    Label = opt.Label,
                    Disabled = opt.Disabled,
                    Checked = Value == opt.Value,
                    OnChange = OnChange
                });
            }
        }
        else
        {
            if (Children.Any())
            {
                foreach (var child in Children)
                {
                     container.Children.Add(child);
                }
            }
        }

        return container;
    }
}
