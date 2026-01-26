using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

public class Select : InputComponent<string>
{
    public string? Name { get; set; }
    public bool Multiple { get; set; }
    public bool Disabled { get; set; }
    public bool Required { get; set; }
    public List<SelectOption> Options { get; set; } = new();

    public bool IsNative { get; set; } = true; // Default to native for now to ensure stability

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var selectTheme = theme?.Select;

        if (IsNative)
        {
            var baseStyle = selectTheme?.Base ?? "";
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
            var triggerStyle = selectTheme?.Trigger ?? "";
            var contentStyle = selectTheme?.Content ?? "";
            var itemStyle = selectTheme?.Item ?? "";

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
        return Value != null && opt.Value == Value || opt.Selected;
    }
}
