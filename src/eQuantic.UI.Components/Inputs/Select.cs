using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

/// <summary>
/// A select input component. Used natively or as a rich compound component.
///
/// Native usage (legacy):
/// <code>
/// new Select { IsNative = true, Options = new() { new SelectOption { Value="1", Label="One" } } }
/// </code>
///
/// Rich usage (Compound):
/// <code>
/// new Select {
///     Children = {
///         new SelectTrigger { Children = { new SelectValue { Placeholder = "Select a fruit" } } },
///         new SelectContent {
///             Children = {
///                 new SelectGroup {
///                     Children = {
///                         new SelectLabel { Text = "Fruits" },
///                         new SelectItem { Value = "apple", Text = "Apple" },
///                         new SelectItem { Value = "banana", Text = "Banana" },
///                     }
///                 }
///             }
///         }
///     }
/// }
/// </code>
/// </summary>
public class Select : InputComponent<string>
{
    public string? Name { get; set; }
    public bool Multiple { get; set; }
    public bool Disabled { get; set; }
    public bool Required { get; set; }
    public List<SelectOption> Options { get; set; } = new();
    public string? DefaultValue { get; set; }
    public bool IsNative { get; set; } = false;
    
    /// <summary>Controlled open state for rich select</summary>
    public bool IsOpen { get; set; }
    
    /// <summary>Side for rich dropdown</summary>
    public Overlays.Side Side { get; set; } = Overlays.Side.Bottom;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var selectTheme = theme?.Select;

        if (IsNative || Options.Any())
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
                if (IsSelectedOld(opt)) optAttrs["selected"] = "selected";

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
            // Rich Compound Component Implementation
            var id = Id ?? $"select-{Guid.NewGuid():N}";
            
            // Pass context to children
            foreach (var child in Children)
            {
                if (child is ISelectChild selectChild)
                {
                    selectChild.SelectParent = this;
                    selectChild.SelectTheme = selectTheme;
                }
            }

            var container = new DynamicElement 
            { 
                TagName = "div", 
                CustomAttributes = new Dictionary<string, string> 
                { 
                    ["class"] = $"eq-select-container relative {ClassName}".Trim(),
                    ["data-state"] = IsOpen ? "open" : "closed"
                } 
            };

            // Hidden Native Select for Form Submission
            var hiddenAttrs = new Dictionary<string, string> 
            { 
                ["class"] = "eq-sr-only eq-select-hidden",
                ["aria-hidden"] = "true",
                ["tabindex"] = "-1"
            };
            if (Name != null) hiddenAttrs["name"] = Name;
            if (Multiple) hiddenAttrs["multiple"] = "true";
            if (Required) hiddenAttrs["required"] = "true";
            if (Disabled) hiddenAttrs["disabled"] = "true";
            
            var hiddenSelect = new DynamicElement { TagName = "select", CustomAttributes = hiddenAttrs };
            
            // Collect all option values from children to build hidden select
            var allItems = FindAllChildren<SelectItem>(Children);
            foreach (var item in allItems)
            {
                if (item.Value != null)
                {
                    var optAttrs = new Dictionary<string, string> { ["value"] = item.Value };
                    if (IsValueSelected(item.Value)) optAttrs["selected"] = "selected";
                    hiddenSelect.Children.Add(new DynamicElement { TagName = "option", CustomAttributes = optAttrs, Children = { new Text(item.Text ?? item.Value) } });
                }
            }
            container.Children.Add(hiddenSelect);

            foreach (var child in Children)
            {
                container.Children.Add(child);
            }

            return container;
        }
    }

    private bool IsSelectedOld(SelectOption opt)
    {
        if (Multiple) return false;
        if (Value != null) return opt.Value == Value;
        if (DefaultValue != null) return opt.Value == DefaultValue;
        return opt.Selected;
    }

    public bool IsValueSelected(string itemValue)
    {
        if (Value != null) return itemValue == Value;
        if (DefaultValue != null) return itemValue == DefaultValue;
        return false;
    }
    
    private List<T> FindAllChildren<T>(IEnumerable<IComponent> children) where T : class
    {
        var result = new List<T>();
        foreach (var child in children)
        {
            if (child is T tChild) result.Add(tChild);
            if (child is StatelessComponent sc) result.AddRange(FindAllChildren<T>(sc.Children));
            if (child is DynamicElement de) result.AddRange(FindAllChildren<T>(de.Children));
        }
        return result;
    }
}

public interface ISelectChild : IComponent
{
    Select? SelectParent { get; set; }
    Core.Theme.ISelectTheme? SelectTheme { get; set; }
}

public abstract class SelectSubComponent : StatelessComponent, ISelectChild
{
    public Select? SelectParent { get; set; }
    public Core.Theme.ISelectTheme? SelectTheme { get; set; }

    protected void PassContextToChildren()
    {
        foreach (var child in Children)
        {
            if (child is ISelectChild sc)
            {
                sc.SelectParent = SelectParent;
                sc.SelectTheme = SelectTheme;
            }
        }
    }
}

public class SelectTrigger : SelectSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        PassContextToChildren();
        var triggerStyle = SelectTheme?.Trigger ?? "eq-select-trigger";
        var isOpen = SelectParent?.IsOpen == true;
        
        var attrs = new Dictionary<string, string>
        {
            ["type"] = "button",
            ["class"] = triggerStyle,
            ["role"] = "combobox",
            ["aria-controls"] = $"{SelectParent?.Id}-content",
            ["aria-expanded"] = isOpen ? "true" : "false",
            ["aria-haspopup"] = "listbox",
            ["data-state"] = isOpen ? "open" : "closed"
        };
        
        if (SelectParent?.Disabled == true) 
        {
            attrs["disabled"] = "true";
            attrs["data-disabled"] = "true";
        }

        var btn = new DynamicElement { TagName = "button", CustomAttributes = attrs };
        foreach (var child in Children) btn.Children.Add(child);
        return btn;
    }
}

public class SelectValue : SelectSubComponent
{
    public string? Placeholder { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var text = Placeholder ?? "";
        
        // Find selected item text if available (naive server-render approach)
        if (SelectParent != null && SelectParent.Value != null)
        {
            // We'd ideally need the label. If we can't find it easily without deep search, 
            // we render the raw value, but JS will update it.
            text = SelectParent.Value;
        }

        return new DynamicElement 
        { 
            TagName = "span", 
            CustomAttributes = new Dictionary<string, string> { ["class"] = "eq-select-value" },
            Children = { new Text(text) }
        };
    }
}

public class SelectContent : SelectSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        PassContextToChildren();
        var contentStyle = SelectTheme?.Content ?? "eq-select-content";
        var isOpen = SelectParent?.IsOpen == true;
        var side = SelectParent?.Side.ToString().ToLowerInvariant() ?? "bottom";

        if (!isOpen) return new NullComponent();

        var content = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["id"] = $"{SelectParent?.Id}-content",
                ["class"] = contentStyle,
                ["role"] = "listbox",
                ["data-state"] = "open",
                ["data-side"] = side,
                ["tabindex"] = "-1"
            }
        };

        foreach (var child in Children) content.Children.Add(child);
        return content;
    }
}

public class SelectItem : SelectSubComponent
{
    public string? Value { get; set; }
    public string? Text { get; set; }
    public bool Disabled { get; set; }

    public override IComponent Build(RenderContext context)
    {
        PassContextToChildren();
        var itemStyle = SelectTheme?.Item ?? "eq-select-item";
        var isSelected = SelectParent?.IsValueSelected(Value ?? "") == true;

        var attrs = new Dictionary<string, string>
        {
            ["class"] = itemStyle,
            ["role"] = "option",
            ["aria-selected"] = isSelected ? "true" : "false",
            ["data-value"] = Value ?? "",
            ["data-state"] = isSelected ? "checked" : "unchecked",
            ["tabindex"] = Disabled ? "-1" : "0"
        };

        if (Disabled)
        {
            attrs["aria-disabled"] = "true";
            attrs["data-disabled"] = "true";
        }

        var item = new DynamicElement { TagName = "div", CustomAttributes = attrs };
        
        if (Children.Any())
        {
            foreach (var child in Children) item.Children.Add(child);
        }
        else if (Text != null)
        {
            item.Children.Add(new Text(Text));
        }

        return item;
    }
}

public class SelectGroup : SelectSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        PassContextToChildren();
        var groupStyle = SelectTheme?.Group ?? "eq-select-group";
        
        var group = new DynamicElement 
        { 
            TagName = "div", 
            CustomAttributes = new Dictionary<string, string> 
            { 
                ["class"] = groupStyle,
                ["role"] = "group"
            }
        };
        foreach (var child in Children) group.Children.Add(child);
        return group;
    }
}

public class SelectLabel : SelectSubComponent
{
    public string? Text { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var labelStyle = SelectTheme?.Label ?? "eq-select-label";
        var label = new DynamicElement 
        { 
            TagName = "div", 
            CustomAttributes = new Dictionary<string, string> { ["class"] = labelStyle } 
        };
        
        if (Children.Any()) foreach (var child in Children) label.Children.Add(child);
        else if (Text != null) label.Children.Add(new Text(Text));
        
        return label;
    }
}

public class SelectSeparator : SelectSubComponent
{
    public override IComponent Build(RenderContext context)
    {
        var sepStyle = SelectTheme?.Separator ?? "eq-select-separator";
        return new DynamicElement 
        { 
            TagName = "div", 
            CustomAttributes = new Dictionary<string, string> 
            { 
                ["class"] = sepStyle,
                ["role"] = "separator"
            } 
        };
    }
}
