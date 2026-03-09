using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

/// <summary>
/// Select input component with support for both controlled and uncontrolled modes.
///
/// Controlled mode (manages value externally):
/// <code>
/// new Select { Value = selectedValue, OnChange = HandleChange }
/// </code>
///
/// Uncontrolled mode (manages value internally):
/// <code>
/// new Select { DefaultValue = "option1" }
/// </code>
/// </summary>
public class Select : InputComponent<string>
{
    /// <summary>
    /// Form field name attribute
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Allow multiple selections
    /// </summary>
    public bool Multiple { get; set; }

    /// <summary>
    /// Disable the select input
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Mark as required field
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Available options to select from
    /// </summary>
    public List<SelectOption> Options { get; set; } = new();

    /// <summary>
    /// Default value for uncontrolled mode (initial value only)
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Use native HTML select element (default: true)
    /// </summary>
    public bool IsNative { get; set; } = true;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var selectTheme = theme?.Select;

        if (IsNative)
        {
            var baseStyle = selectTheme?.Base ?? "eq-select";
            var attrs = new Dictionary<string, string>
            {
                ["class"] = $"{baseStyle} {ClassName}".Trim()
            };

            if (Name != null) attrs["name"] = Name;
            if (Multiple) attrs["multiple"] = "true";
            if (Disabled) attrs["disabled"] = "true";
            if (Required) attrs["required"] = "true";

            var events = BuildEvents();
            if (OnChange != null) events["change"] = OnChange;

            var selectElement = new DynamicElement
            {
                TagName = "select",
                CustomAttributes = attrs,
                CustomEvents = events
            };

            foreach (var opt in Options)
            {
                var optAttrs = new Dictionary<string, string> { ["value"] = opt.Value };
                if (opt.Disabled) optAttrs["disabled"] = "true";
                if (IsSelected(opt)) optAttrs["selected"] = "selected";

                selectElement.Children.Add(new DynamicElement
                {
                    TagName = "option",
                    InnerText = opt.Label,
                    CustomAttributes = optAttrs
                });
            }
            return selectElement;
        }
        else
        {
            // Rich UI implementation
            // Note: This requires a corresponding JS controller for interactivity (open/close)
            // For now we render the structure.
            var triggerStyle = selectTheme?.Trigger ?? "eq-select-trigger";
            var contentStyle = selectTheme?.Content ?? "eq-select-content";
            var itemStyle = selectTheme?.Item ?? "eq-select-item";

            var container = new DynamicElement { TagName = "div", CustomAttributes = new Dictionary<string, string> { ["class"] = $"relative {ClassName}".Trim() } };

            // Hidden Native Select for Form Submission
            var hiddenSelect = new DynamicElement { TagName = "select", CustomAttributes = new Dictionary<string, string> { ["class"] = "hidden", ["name"] = Name ?? "" } };
            if (Multiple) hiddenSelect.CustomAttributes["multiple"] = "true";
            foreach (var opt in Options)
            {
                 var optAttrs = new Dictionary<string, string> { ["value"] = opt.Value };
                 if (IsSelected(opt)) optAttrs["selected"] = "selected";
                 hiddenSelect.Children.Add(new DynamicElement { TagName = "option", InnerText = opt.Label, CustomAttributes = optAttrs });
            }
            container.Children.Add(hiddenSelect);

            // Trigger
            var selectedLabel = Options.FirstOrDefault(o => IsSelected(o))?.Label ?? "Select...";
            var trigger = new DynamicElement
            {
                TagName = "button",
                CustomAttributes = new Dictionary<string, string>
                {
                    ["type"] = "button",
                    ["class"] = triggerStyle,
                    ["aria-haspopup"] = "listbox",
                    ["aria-expanded"] = "false",
                    ["data-state"] = "closed"
                },
                Children = { new Text(selectedLabel) }
            };
            container.Children.Add(trigger);

            // Content (Dropdown) - Hidden by default
            var content = new DynamicElement
            {
                TagName = "div",
                CustomAttributes = new Dictionary<string, string>
                {
                    ["class"] = $"{contentStyle} hidden",
                    ["role"] = "listbox"
                }
            };

            foreach(var opt in Options)
            {
                content.Children.Add(new DynamicElement
                {
                    TagName = "div",
                    CustomAttributes = new Dictionary<string, string>
                    {
                        ["class"] = itemStyle,
                        ["role"] = "option",
                        ["data-value"] = opt.Value,
                        ["data-state"] = IsSelected(opt) ? "checked" : "unchecked"
                    },
                    Children = { new Text(opt.Label) }
                });
            }
            container.Children.Add(content);

            return container;
        }
    }

    private bool IsSelected(SelectOption opt)
    {
        if (Multiple) return false; // Basic check

        // Controlled mode: Use Value prop
        if (Value != null)
        {
            return opt.Value == Value;
        }

        // Uncontrolled mode: Use DefaultValue or opt.Selected
        if (DefaultValue != null)
        {
            return opt.Value == DefaultValue;
        }

        return opt.Selected;
    }
}
