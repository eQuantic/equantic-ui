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
    public bool IsNative { get; set; } = false;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var switchTheme = theme?.Switch;
        var state = Value ? "checked" : "unchecked";

        if (IsNative)
        {
            var checkboxTheme = theme?.Checkbox;
            var baseStyle = checkboxTheme?.Base ?? "";
            var attrs = new Dictionary<string, string>
            {
                ["type"] = "checkbox",
                ["class"] = $"{baseStyle} {ClassName}".Trim()
            };
            if (Value) attrs["checked"] = "true";
            if (Disabled) attrs["disabled"] = "true";
            if (Name != null) attrs["name"] = Name;
            
            var events = BuildEvents();
            if (OnChange != null) events["change"] = OnChange;

            var input = new DynamicElement
            {
                TagName = "input",
                CustomAttributes = attrs,
                CustomEvents = events
            };
            
            if (Label != null)
            {
                 var label = new DynamicElement { TagName = "label", CustomAttributes = new Dictionary<string, string> { ["class"] = "flex items-center gap-2" } };
                 label.Children.Add(input);
                 label.Children.Add(new Text(Label));
                 return label;
            }
            return input;
        }

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
        
        var richEvents = BuildEvents();
        if (OnChange != null) richEvents["change"] = OnChange;

        var inputElement = new DynamicElement
        {
            TagName = "input",
            CustomAttributes = inputAttrs,
            CustomEvents = richEvents
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
