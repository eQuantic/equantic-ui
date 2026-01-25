using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

public class Select : InputComponent<string>
{
    public string? Name { get; set; }
    public bool Multiple { get; set; }
    public bool Disabled { get; set; }
    public bool Required { get; set; }
    public List<SelectOption> Options { get; set; } = new();

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var inputTheme = theme?.Input;
        var baseStyle = inputTheme?.Base ?? "";

        var attrs = new Dictionary<string, string>
        {
            ["class"] = $"{baseStyle} {ClassName}"
        };
        
        if (Name != null) attrs["name"] = Name;
        if (Multiple) attrs["multiple"] = "true";
        if (Disabled) attrs["disabled"] = "true";
        if (Required) attrs["required"] = "true";
        
        var events = BuildEvents();
        if (OnChange != null)
        {
            events["change"] = OnChange;
        }

        var selectElement = new DynamicElement
        {
            TagName = "select",
            CustomAttributes = attrs,
            CustomEvents = events
        };

        foreach (var opt in Options)
        {
            var optAttrs = new Dictionary<string, string>
            {
                ["value"] = opt.Value
            };
            
            if (opt.Disabled) optAttrs["disabled"] = "true";
            
            bool isSelected = opt.Selected;
            if (!Multiple && Value != null && opt.Value == Value)
            {
                isSelected = true;
            }
            
            if (isSelected) optAttrs["selected"] = "selected";

            selectElement.Children.Add(new DynamicElement
            {
                TagName = "option",
                InnerText = opt.Label,
                CustomAttributes = optAttrs
            });
        }

        return selectElement;
    }
}
