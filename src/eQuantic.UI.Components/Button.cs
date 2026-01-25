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
        var buttonTheme = theme?.Button;

        var baseStyle = buttonTheme?.Base ?? "";
        var variantStyle = "";
        if (buttonTheme?.Variants != null && buttonTheme.Variants.TryGetValue(Variant?.ToLower() ?? "primary", out var v))
        {
            variantStyle = v;
        }

        var attrs = new Dictionary<string, string>
        {
            ["type"] = Type,
            ["class"] = $"{baseStyle} {variantStyle} {ClassName}"
        };

        if (Disabled) attrs["disabled"] = "true";

        // We need to pass children to the DynamicElement
        // inherited Children property contains the input children.

        var element = new DynamicElement
        {
            TagName = "button",
            CustomAttributes = attrs,
            CustomEvents = BuildEvents()
        };

        if (Children.Any())
        {
            foreach (var child in Children)
            {
                element.Children.Add(child);
            }
        }
        else if (Text != null)
        {
            element.Children.Add(new Text(Text));
        }

        return element;
    }
}
