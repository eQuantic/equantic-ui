using System;
using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components.Inputs;

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
    public Size Size { get; set; } = Size.Medium;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var inputTheme = theme?.Input;
        var baseStyle = inputTheme?.Base ?? "";
        var sizeStyle = inputTheme?.GetSize(Size) ?? "";

        var attrs = new Dictionary<string, string>
        {
            ["type"] = Type,
            ["class"] = $"{baseStyle} {sizeStyle} {ClassName}".Trim()
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
