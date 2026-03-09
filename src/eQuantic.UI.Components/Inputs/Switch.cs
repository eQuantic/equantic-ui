using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

/// <summary>
/// Toggle switch component (wraps a checkbox)
/// </summary>
public class Switch : Checkbox
{
    public string? Label { get; set; }
    public Switch() => IsNative = false;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var switchTheme = theme?.Switch;
        var state = Value ? "checked" : "unchecked";

        if (IsNative)
        {
            var checkboxTheme = theme?.Checkbox;
            var baseStyle = checkboxTheme?.Base ?? "eq-checkbox";
            var events = BuildEvents();
            if (OnChange != null) events["change"] = OnChange;

            var input = new Box
            {
                As = "input",
                Type = "checkbox",
                ClassName = $"{baseStyle} {ClassName}".Trim(),
                Checked = Value,
                Disabled = Disabled,
                Name = Name,
                CustomEvents = events
            };
            
            if (Label != null)
            {
                 var label = new Box { As = "label", ClassName = "eq-switch-label", Children = { input, new Text(Label) } };
                 return label;
            }
            return input;
        }

        var rootStyle = switchTheme?.Root ?? "eq-switch-root";
        var thumbStyle = switchTheme?.Thumb ?? "eq-switch-thumb";
        
        // Hidden input for form submission
        var richEvents = BuildEvents();
        if (OnChange != null) richEvents["change"] = OnChange;

        var inputElement = new Box
        {
            As = "input",
            Type = "checkbox",
            Role = "switch",
            ClassName = "eq-sr-only",
            Name = Name ?? "",
            Checked = Value,
            Disabled = Disabled,
            AriaChecked = Value ? "true" : "false",
            CustomEvents = richEvents
        };
        
        var thumbElement = new Box
        {
            As = "span",
            ClassName = thumbStyle,
            DataAttributes = new Dictionary<string, string> { ["state"] = state }
        };

        var container = new Box
        {
            As = "label", // Use label to trigger input click
            ClassName = $"{rootStyle} {ClassName}".Trim(),
            DataAttributes = new Dictionary<string, string> { ["state"] = state },
            Children = { inputElement, thumbElement }
        };
        
        if (Label != null)
        {
            container.Children.Add(new Text(Label));
        }

        return container;
    }
}
