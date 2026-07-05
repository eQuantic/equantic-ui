using System;
using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Web.Components.Inputs;

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

    /// <summary>Selected values when <see cref="Multiple"/> is enabled (controlled).</summary>
    public IReadOnlyList<string>? Values { get; set; }

    /// <summary>Default selected values for an uncontrolled multiple select.</summary>
    public IReadOnlyList<string>? DefaultValues { get; set; }

    /// <summary>Change handler for a multiple select; receives all currently selected values.</summary>
    public Action<IReadOnlyList<string>>? OnValuesChange { get; set; }
    
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
            var events = new Dictionary<string, Delegate>();
            // A multiple select emits all selected values (the runtime passes a string[]),
            // so it uses OnValuesChange; a single select uses OnChange (a single string).
            if (Multiple)
            {
                if (OnValuesChange != null) events["change"] = OnValuesChange;
            }
            else if (OnChange != null)
            {
                events["change"] = OnChange;
            }

            var selectElement = new Box
            {
                As = "select",
                ClassName = $"{baseStyle} {ClassName}".Trim(),
                Name = Name,
                Multiple = Multiple,
                Disabled = Disabled,
                Required = Required,
                CustomEvents = events
            };

            foreach (var opt in Options)
            {
                var optionBox = new Box
                {
                    As = "option",
                    Value = opt.Value,
                    Disabled = opt.Disabled,
                    Selected = IsSelectedOld(opt),
                    Children = { new Text(opt.Label) }
                };
                selectElement.Children.Add(optionBox);
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

            var container = new Box 
            { 
                As = "div", 
                ClassName = $"eq-select-container relative {ClassName}".Trim(),
                DataAttributes = new Dictionary<string, string> { ["state"] = IsOpen ? "open" : "closed" }
            };

            // Hidden Native Select for Form Submission
            var hiddenSelect = new Box 
            { 
                As = "select", 
                ClassName = "eq-sr-only eq-select-hidden",
                AriaHidden = true,
                TabIndex = -1,
                Name = Name,
                Multiple = Multiple,
                Required = Required,
                Disabled = Disabled
            };
            
            // Collect all option values from children to build hidden select
            var allItems = FindAllChildren<SelectItem>(Children);
            foreach (var item in allItems)
            {
                if (item.Value != null)
                {
                    var optBox = new Box { As = "option", Value = item.Value, Selected = IsValueSelected(item.Value), Children = { new Text(item.Text ?? item.Value) } };
                    hiddenSelect.Children.Add(optBox);
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
        if (Multiple) return IsValueInSet(opt.Value);
        if (Value != null) return opt.Value == Value;
        if (DefaultValue != null) return opt.Value == DefaultValue;
        return opt.Selected;
    }

    public bool IsValueSelected(string itemValue)
    {
        if (Multiple) return IsValueInSet(itemValue);
        if (Value != null) return itemValue == Value;
        if (DefaultValue != null) return itemValue == DefaultValue;
        return false;
    }

    /// <summary>Whether a value is part of the multiple-select selection (controlled or default).</summary>
    private bool IsValueInSet(string value)
    {
        if (Values != null) return Values.Contains(value);
        if (DefaultValues != null) return DefaultValues.Contains(value);
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
        
        var btn = new Box 
        { 
            As = "button", 
            Type = "button",
            ClassName = triggerStyle,
            Role = "combobox",
            AriaControls = $"{SelectParent?.Id}-content",
            AriaExpanded = isOpen,
            AriaHasPopup = "listbox",
            DataAttributes = new Dictionary<string, string> { ["state"] = isOpen ? "open" : "closed" },
            Disabled = SelectParent?.Disabled == true
        };
        if (SelectParent?.Disabled == true) btn.DataAttributes["disabled"] = "true";
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

        return new Box 
        { 
            As = "span", 
            ClassName = "eq-select-value",
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

        var content = new Box
        {
            As = "div",
            Id = $"{SelectParent?.Id}-content",
            ClassName = contentStyle,
            Role = "listbox",
            TabIndex = -1,
            DataAttributes = new Dictionary<string, string>
            {
                ["state"] = "open",
                ["side"] = side
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

        var item = new Box 
        { 
            As = "div", 
            ClassName = itemStyle,
            Role = "option",
            AriaSelected = isSelected,
            TabIndex = Disabled ? -1 : 0,
            DataAttributes = new Dictionary<string, string>
            {
                ["value"] = Value ?? "",
                ["state"] = isSelected ? "checked" : "unchecked"
            }
        };
        
        if (Disabled)
        {
            item.AriaDisabled = true;
            item.DataAttributes["disabled"] = "true";
        }
        
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
        
        var group = new Box 
        { 
            As = "div", 
            ClassName = groupStyle,
            Role = "group"
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
        var label = new Box 
        { 
            As = "div", 
            ClassName = labelStyle
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
        return new Box 
        { 
            As = "div", 
            ClassName = sepStyle,
            Role = "separator"
        };
    }
}
