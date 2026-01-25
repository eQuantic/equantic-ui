using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

/// <summary>
/// Button component
/// </summary>
public class Button : StatelessComponent
{
    public string Type { get; set; } = "button";
    public bool Disabled { get; set; }
    public string? Text { get; set; }
    public string Variant { get; set; } = "primary";

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var buttonTheme = theme != null ? theme.Button : null;

        var baseStyle = "";
        if (buttonTheme != null) baseStyle = buttonTheme.Base;
        
        var variantStyle = "";
        if (buttonTheme != null && buttonTheme.Variants != null && this.Variant != null)
        {
             var v = buttonTheme.Variants[this.Variant];
             if (v != null) variantStyle = v;
        }

        var attrs = new Dictionary<string, string>
        {
            ["type"] = this.Type,
            ["class"] = baseStyle + " " + variantStyle + " " + (this.ClassName != null ? this.ClassName : "")
        };

        if (this.Disabled) attrs["disabled"] = "true";

        // We need to pass children to the DynamicElement
        // inherited Children property contains the input children.

        var element = new DynamicElement
        {
            TagName = "button",
            CustomAttributes = attrs,
            CustomEvents = BuildEvents()
        };

        if (this.Children.Any())
        {
            foreach (var child in this.Children)
            {
                element.Children.Add(child);
            }
        }
        else if (this.Text != null)
        {
            element.Children.Add(new Text(this.Text));
        }

        return element;
    }
}
