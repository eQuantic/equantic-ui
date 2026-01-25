using System;
using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Events;

namespace eQuantic.UI.Components;

/// <summary>
/// Base class for all input components
/// </summary>
/// <typeparam name="TValue">The type of the input value</typeparam>
public abstract class InputComponent<TValue> : StatelessComponent
{
    /// <summary>
    /// Current value
    /// </summary>
    public TValue? Value { get; set; }

    /// <summary>
    /// Change event handler
    /// </summary>
    public Action<TValue>? OnChange { get; set; }

    /// <summary>
    /// Input event handler
    /// </summary>
    public Action<TValue>? OnInput { get; set; }
}

/// <summary>
/// Text input component
/// </summary>
public class TextInput : InputComponent<string>
{
    public string Type { get; set; } = "text";
    public string? Placeholder { get; set; }
    public bool Disabled { get; set; }
    public bool ReadOnly { get; set; }
    public bool Required { get; set; }
    public string? Name { get; set; }
    public int? MaxLength { get; set; }
    public string? AutoComplete { get; set; }
    
    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var inputTheme = theme?.Input;
        var baseStyle = inputTheme?.Base ?? "";

        var attrs = new Dictionary<string, string>
        {
            ["type"] = Type,
            ["class"] = $"{baseStyle} {ClassName}"
        };

        if (Value != null) attrs["value"] = Value;
        if (Placeholder != null) attrs["placeholder"] = Placeholder;
        if (Disabled) attrs["disabled"] = "true";
        if (ReadOnly) attrs["readonly"] = "true";
        if (Required) attrs["required"] = "true";
        if (Name != null) attrs["name"] = Name;
        if (MaxLength.HasValue) attrs["maxlength"] = MaxLength.Value.ToString();
        if (AutoComplete != null) attrs["autocomplete"] = AutoComplete;
        
        var events = BuildEvents();
        
        if (OnChange != null) events["change"] = OnChange;
        if (OnInput != null) events["input"] = OnInput;

        return new DynamicElement
        {
            TagName = "input",
            CustomAttributes = attrs,
            CustomEvents = events
        };
    }
}

/// <summary>
/// Text area component for multiline input
/// </summary>
public class TextArea : InputComponent<string>
{
    public string? Placeholder { get; set; }
    public int? Rows { get; set; }
    public bool Disabled { get; set; }
    public bool ReadOnly { get; set; }
    public string? Name { get; set; }
    
    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var inputTheme = theme?.Input;
        var baseStyle = inputTheme?.Base ?? "";

        var attrs = new Dictionary<string, string>
        {
            ["class"] = $"{baseStyle} {ClassName}"
        };
        
        if (Placeholder != null) attrs["placeholder"] = Placeholder;
        if (Rows.HasValue) attrs["rows"] = Rows.Value.ToString();
        if (Disabled) attrs["disabled"] = "true";
        if (ReadOnly) attrs["readonly"] = "true";
        if (Name != null) attrs["name"] = Name;
        
        var events = BuildEvents();
        if (OnChange != null) events["change"] = OnChange;
        if (OnInput != null) events["input"] = OnInput;
        
        var element = new DynamicElement
        {
            TagName = "textarea",
            CustomAttributes = attrs,
            CustomEvents = events
        };
        
        if (Value != null)
        {
            element.Children.Add(new Text(Value));
        }
        
        return element;
    }
}
