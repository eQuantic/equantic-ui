using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

public class Checkbox : InputComponent<bool>
{
    public bool Checked { get => Value; set => Value = value; }
    public bool Disabled { get; set; }

    public bool IsNative { get; set; } = true;
    public string? Name { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var checkboxTheme = theme?.Checkbox;
        
        var baseStyle = checkboxTheme?.Base ?? "";
        var checkedStyle = checkboxTheme?.Checked ?? "";
        var uncheckedStyle = checkboxTheme?.Unchecked ?? "";

        var stateStyle = Value ? checkedStyle : uncheckedStyle;

        if (IsNative)
        {
            var attrs = new Dictionary<string, string>
            {
                ["type"] = "checkbox",
                ["class"] = $"{baseStyle} {stateStyle} {ClassName}".Trim()
            };
    
            if (Value) attrs["checked"] = "true";
            if (Disabled) attrs["disabled"] = "true";
            if (Name != null) attrs["name"] = Name;
    
            var events = BuildEvents();
            if (OnChange != null) events["change"] = OnChange;
            
            return new DynamicElement
            {
                TagName = "input",
                CustomAttributes = attrs,
                CustomEvents = events
            };
        }
        else
        {
            // Rich Checkbox
            var rootStyle = checkboxTheme?.Root ?? "";
            var indicatorStyle = checkboxTheme?.Indicator ?? "";
            var state = Value ? "checked" : "unchecked";

            // Hidden input
            var inputAttrs = new Dictionary<string, string>
            {
                ["type"] = "checkbox",
                ["class"] = "sr-only",
                ["name"] = Name ?? ""
            };
            if (Value) inputAttrs["checked"] = "true";
            
            var richEvents = BuildEvents();
            if (OnChange != null) richEvents["change"] = OnChange;

            var input = new DynamicElement
            {
                TagName = "input",
                CustomAttributes = inputAttrs,
                CustomEvents = richEvents
            };

            var button = new DynamicElement
            {
                TagName = "button",
                CustomAttributes = new Dictionary<string, string>
                {
                    ["type"] = "button",
                    ["role"] = "checkbox",
                    ["aria-checked"] = Value.ToString().ToLower(),
                    ["data-state"] = state,
                    ["class"] = $"{rootStyle} {ClassName}".Trim()
                },
                // OnClick handling implies client-side toggle or server event.
                // Assuming wrapper handles click -> change event.
            };
            if (Disabled) button.CustomAttributes["disabled"] = "true";

            // Indicator
            if (Value)
            {
                var indicator = new DynamicElement
                {
                    TagName = "div",
                    CustomAttributes = new Dictionary<string, string> 
                    { 
                        ["class"] = indicatorStyle,
                        ["data-state"] = state
                    }
                };
                
                // SVG Checkmark
                var svg = new DynamicElement { TagName = "svg", CustomAttributes = new Dictionary<string, string> { 
                    ["xmlns"] = "http://www.w3.org/2000/svg",
                    ["width"] = "14",
                    ["height"] = "14",
                    ["viewBox"] = "0 0 24 24",
                    ["fill"] = "none",
                    ["stroke"] = "currentColor",
                    ["stroke-width"] = "3",
                    ["stroke-linecap"] = "round",
                    ["stroke-linejoin"] = "round",
                    ["class"] = "h-3.5 w-3.5"
                }};
                svg.Children.Add(new DynamicElement { TagName = "polyline", CustomAttributes = new Dictionary<string, string> { ["points"] = "20 6 9 17 4 12" } });
                
                indicator.Children.Add(svg);
                button.Children.Add(indicator);
            }

            // Wrapper label to handle click targeting the input
            var label = new DynamicElement
            {
                TagName = "label",
                CustomAttributes = new Dictionary<string, string> { ["class"] = "inline-flex items-center gap-2 cursor-pointer" },
                Children = { input, button }
            };
            
            // We wrap input outside button because button inside label triggers input? 
            // Standard pattern: <label> <input class=hidden> <div class=visual> </div> </label>
            // Here `button` IS the visual.
            // If I click the label, the input toggles. Use CSS to reflect state on button?
            // Shadcn uses `peer` on input to style sibling.
            // My styles use `data-[state=checked]`.
            // If I rely on `peer`, I don't need `button` to have data-state.
            // BUT Shadcn's Checkbox IS a button (primitive).
            // Let's stick to the button being the visual representation controlled by JS or State.
            // Since this is server-rendered primarily, `Value` determines styling.
            
            return label;
        }
    }
}
