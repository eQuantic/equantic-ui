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
    public new string? Placeholder { get; set; }
    public new int? Rows { get; set; }
    public new bool Disabled { get; set; }
    public new bool ReadOnly { get; set; }
    public new string? Name { get; set; }
    public new SizeVariant Size { get; set; } = SizeVariant.Medium;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var inputTheme = theme?.Input;
        var baseStyle = inputTheme?.Base ?? "eq-textarea";
        // Size might affect font size/padding even if height is determined by rows
        var sizeStyle = inputTheme?.GetSize(Size) ?? $"eq-textarea-{Size.ToString().ToLowerInvariant()}";

        var events = BuildEvents();
        if (OnChange != null) events["change"] = OnChange;
        if (OnInput != null) events["input"] = OnInput;

        var element = new Box
        {
            As = "textarea",
            ClassName = $"{baseStyle} {sizeStyle} {ClassName}".Trim(),
            Placeholder = Placeholder,
            Rows = Rows,
            Disabled = Disabled,
            ReadOnly = ReadOnly,
            Name = Name,
            CustomEvents = events
        };

        if (Value != null)
        {
            element.Children.Add(new Text(Value));
        }

        return element;
    }
}
