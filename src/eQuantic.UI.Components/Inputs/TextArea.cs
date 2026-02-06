using System;
using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components.Inputs;

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
    public Size Size { get; set; } = Size.Medium;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var inputTheme = theme?.Input;
        var baseStyle = inputTheme?.Base ?? "";
        // Size might affect font size/padding even if height is determined by rows
        var sizeStyle = inputTheme?.GetSize(Size) ?? "";

        var attrs = new Dictionary<string, string>
        {
            ["class"] = $"{baseStyle} {sizeStyle} {ClassName}".Trim()
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
