using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Display;

/// <summary>
/// Badge component for status indicators.
/// </summary>
public class Badge : StatelessComponent
{
    public string? Text { get; set; }
    public string Variant { get; set; } = "default";

    public Badge() { }
    public Badge(string text) => Text = text;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var badgeTheme = theme?.Badge;

        var baseStyle = badgeTheme?.Base ?? "";
        var variantStyle = badgeTheme?.GetVariant(this.Variant) ?? "";
        
        var element = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"{baseStyle} {variantStyle} {this.ClassName}".Trim()
            }
        };

        if (this.Children.Any())
        {
            foreach (var child in this.Children) element.Children.Add(child);
        }
        else if (this.Text != null)
        {
            element.Children.Add(new Text(this.Text));
        }

        return element;
    }
}
