using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

/// <summary>
/// Toggle switch component (wraps a checkbox)
/// </summary>
public class Switch : Checkbox
{
    public string? Label { get; set; }

    public override IComponent Build(RenderContext context)
    {
        // Don't call base.Build because we want different structure
        var inputAttrs = new Dictionary<string, string>
        {
            ["type"] = "checkbox",
            ["class"] = "switch-input" // Specific class for switch input
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
        
        var sliderElement = new DynamicElement
        {
            TagName = "span",
            CustomAttributes = new Dictionary<string, string> { ["class"] = "slider" }
        };

        var container = new DynamicElement
        {
            TagName = "label",
            CustomAttributes = new Dictionary<string, string> { ["class"] = "switch " + ClassName }
        };
        
        container.Children.Add(inputElement);
        container.Children.Add(sliderElement);
        
        if (Label != null)
        {
            container.Children.Add(new Text(" " + Label));
        }

        return container;
    }
}
