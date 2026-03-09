using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

/// <summary>
/// Checkbox input component with support for both controlled and uncontrolled modes.
///
/// Controlled mode (manages checked state externally):
/// <code>
/// new Checkbox { Checked = isChecked, OnChange = HandleChange }
/// </code>
///
/// Uncontrolled mode (manages checked state internally):
/// <code>
/// new Checkbox { DefaultChecked = true }
/// </code>
/// </summary>
public class Checkbox : InputComponent<bool>
{
    /// <summary>
    /// Checked state (controlled mode). Alias for Value property.
    /// </summary>
    public bool Checked { get => Value; set => Value = value; }

    /// <summary>
    /// Default checked state for uncontrolled mode (initial state only)
    /// </summary>
    public bool DefaultChecked { get; set; }

    /// <summary>
    /// Disable the checkbox
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Use native HTML checkbox element (default: true)
    /// </summary>
    public bool IsNative { get; set; } = true;

    /// <summary>
    /// Form field name attribute
    /// </summary>
    public string? Name { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var checkboxTheme = theme?.Checkbox;
        
        var baseStyle = checkboxTheme?.Base ?? "eq-checkbox";
        var checkedStyle = checkboxTheme?.Checked ?? "eq-checkbox-checked";
        var uncheckedStyle = checkboxTheme?.Unchecked ?? "eq-checkbox-unchecked";

        // Controlled mode: Use Value (Checked)
        // Uncontrolled mode: Use DefaultChecked
        var isChecked = Value || DefaultChecked;
        var stateStyle = isChecked ? checkedStyle : uncheckedStyle;

        if (IsNative)
        {
            var events = new Dictionary<string, Delegate>();
            if (OnChange != null) events["change"] = OnChange;
            
            return new Box
            {
                As = "input",
                Type = "checkbox",
                ClassName = $"{baseStyle} {stateStyle} {ClassName}".Trim(),
                Checked = isChecked,
                Disabled = Disabled,
                Name = Name,
                CustomEvents = events
            };
        }
        else
        {
            // Rich Checkbox
            var rootStyle = checkboxTheme?.Root ?? "eq-checkbox-root";
            var indicatorStyle = checkboxTheme?.Indicator ?? "eq-checkbox-indicator";
            var state = isChecked ? "checked" : "unchecked";

            // Hidden input
            var richEvents = new Dictionary<string, Delegate>();
            if (OnChange != null) richEvents["change"] = OnChange;

            var input = new Box
            {
                As = "input",
                Type = "checkbox",
                ClassName = "eq-sr-only",
                Name = Name ?? "",
                Checked = isChecked,
                CustomEvents = richEvents
            };

            var button = new Box
            {
                As = "button",
                Type = "button",
                Role = "checkbox",
                ClassName = $"{rootStyle} {ClassName}".Trim(),
                Disabled = Disabled,
                DataAttributes = new Dictionary<string, string> { ["state"] = state },
                AriaChecked = isChecked ? "true" : "false"
            };

            // Indicator
            if (isChecked)
            {
                var indicator = new Box
                {
                    As = "div",
                    ClassName = indicatorStyle,
                    DataAttributes = new Dictionary<string, string> { ["state"] = state }
                };
                
                // SVG Checkmark
                var svg = new Box { 
                    As = "svg", 
                    ClassName = "eq-checkbox-indicator",
                    CustomAttributes = new Dictionary<string, string> {
                        ["xmlns"] = "http://www.w3.org/2000/svg",
                        ["width"] = "14",
                        ["height"] = "14",
                        ["viewBox"] = "0 0 24 24",
                        ["fill"] = "none",
                        ["stroke"] = "currentColor",
                        ["stroke-width"] = "3",
                        ["stroke-linecap"] = "round",
                        ["stroke-linejoin"] = "round"
                    }
                };
                svg.Children.Add(new Box { As = "polyline", CustomAttributes = new Dictionary<string, string> { ["points"] = "20 6 9 17 4 12" } });
                
                indicator.Children.Add(svg);
                button.Children.Add(indicator);
            }

            // Wrapper label to handle click targeting the input
            var label = new Box
            {
                As = "label",
                ClassName = "eq-checkbox-label",
                Children = { input, button }
            };
            
            return label;
        }
    }
}
