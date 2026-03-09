using System;
using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components.Inputs;

/// <summary>
/// Text input component with support for both controlled and uncontrolled modes.
///
/// Controlled mode (manages value externally):
/// <code>
/// new TextInput { Value = inputValue, OnChange = HandleChange }
/// </code>
///
/// Uncontrolled mode (manages value internally):
/// <code>
/// new TextInput { DefaultValue = "initial text" }
/// </code>
/// </summary>
public class TextInput : InputComponent<string>
{
    /// <summary>
    /// HTML input type (text, email, password, etc.)
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>
    /// Placeholder text to display when empty
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// Disable the input
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Make the input read-only
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Mark as required field
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Form field name attribute
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Maximum character length
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Autocomplete hint for browsers
    /// </summary>
    public string? AutoComplete { get; set; }

    /// <summary>
    /// Input size variant
    /// </summary>
    public Size Size { get; set; } = Size.Medium;

    /// <summary>
    /// Default value for uncontrolled mode (initial value only)
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Component to render as an icon on the left side of the input
    /// </summary>
    public IComponent? LeftIcon { get; set; }

    /// <summary>
    /// Component to render as an icon on the right side of the input
    /// </summary>
    public IComponent? RightIcon { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var inputTheme = theme?.Input;
        var baseStyle = inputTheme?.Base ?? "eq-input";
        var sizeStyle = inputTheme?.GetSize(Size) ?? $"eq-input-{Size.ToString().ToLowerInvariant()}";

        var inputClass = $"{baseStyle} {sizeStyle} {ClassName}".Trim();
        
        if (LeftIcon != null) inputClass += " eq-input-with-icon-left";
        if (RightIcon != null) inputClass += " eq-input-with-icon-right";

        var attrs = new Dictionary<string, string>
        {
            ["type"] = Type,
            ["class"] = inputClass.Trim()
        };

        // Controlled mode: Use Value prop
        // Uncontrolled mode: Use DefaultValue
        var initialValue = Value ?? DefaultValue;
        if (initialValue != null) attrs["value"] = initialValue;
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

        var input = new DynamicElement
        {
            TagName = "input",
            CustomAttributes = attrs,
            CustomEvents = events
        };

        if (LeftIcon == null && RightIcon == null)
        {
            return input;
        }

        var wrapper = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string> { ["class"] = "eq-input-wrapper" }
        };

        if (LeftIcon != null)
        {
            var leftContainer = new DynamicElement { TagName = "div", CustomAttributes = new Dictionary<string, string> { ["class"] = "eq-input-icon-left" } };
            leftContainer.Children.Add(LeftIcon);
            wrapper.Children.Add(leftContainer);
        }

        wrapper.Children.Add(input);

        if (RightIcon != null)
        {
            var rightContainer = new DynamicElement { TagName = "div", CustomAttributes = new Dictionary<string, string> { ["class"] = "eq-input-icon-right" } };
            rightContainer.Children.Add(RightIcon);
            wrapper.Children.Add(rightContainer);
        }

        return wrapper;
    }
}
