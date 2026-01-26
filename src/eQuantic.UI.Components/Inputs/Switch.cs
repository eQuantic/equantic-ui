using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

/// <summary>
/// Toggle switch component (wraps a checkbox)
/// </summary>
public class Switch : Checkbox
{
    public string? Label { get; set; }
    public string? Name { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var switchTheme = theme?.Switch;

        var state = Value ? "checked" : "unchecked";

        var rootStyle = switchTheme?.Root ?? "";
        var thumbStyle = switchTheme?.Thumb ?? "";
        
        // Hidden input for form submission
        var inputAttrs = new Dictionary<string, string>
        {
            ["type"] = "checkbox",
            ["class"] = "sr-only", 
            ["name"] = Name ?? ""
        };
        if (Value) inputAttrs["checked"] = "true";
        if (Disabled) inputAttrs["disabled"] = "true";
        
        var events = BuildEvents();
        if (OnChange != null) events["change"] = OnChange;

        var inputElement = new DynamicElement
        {
            TagName = "input",
            CustomAttributes = inputAttrs,
            CustomEvents = events
        };
        
        var thumbElement = new DynamicElement
        {
            TagName = "span",
            CustomAttributes = new Dictionary<string, string> 
            { 
                ["class"] = thumbStyle,
                ["data-state"] = state
            }
        };

        var container = new DynamicElement
        {
            TagName = "label", // Use label to trigger input click
            CustomAttributes = new Dictionary<string, string> 
            { 
                ["class"] = $"{rootStyle} {ClassName}".Trim(),
                ["data-state"] = state
            }
        };
        
        container.Children.Add(inputElement);
        container.Children.Add(thumbElement);
        
        if (Label != null)
        {
            container.Children.Add(new Text(Label));
        }

        return container;
    }
}
