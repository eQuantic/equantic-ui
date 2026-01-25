using System;
using System.Collections.Generic;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Inputs;

public class Checkbox : InputComponent<bool>
{
    public bool Checked { get => Value; set => Value = value; }
    public bool Disabled { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var checkboxTheme = theme?.Checkbox;
        
        var baseStyle = checkboxTheme?.Base ?? "";
        var checkedStyle = checkboxTheme?.Checked ?? "";
        var uncheckedStyle = checkboxTheme?.Unchecked ?? "";

        var stateStyle = Value ? checkedStyle : uncheckedStyle;

        var attrs = new Dictionary<string, string>
        {
            ["type"] = "checkbox",
            ["class"] = $"{baseStyle} {stateStyle} {ClassName}"
        };

        if (Value) attrs["checked"] = "true";
        if (Disabled) attrs["disabled"] = "true";

        var events = BuildEvents();
        if (OnChange != null) events["change"] = OnChange;
        
        return new DynamicElement
        {
            TagName = "input",
            CustomAttributes = attrs,
            CustomEvents = events
        };
    }
}
