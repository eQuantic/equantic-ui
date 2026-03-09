using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components.Display;

/// <summary>
/// Badge component for status indicators.
/// </summary>
public class Badge : StatelessComponent
{
    public string? Text { get; set; }
    public Variant Variant { get; set; } = Variant.Default;

    public Badge() { }
    public Badge(string text) => Text = text;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var badgeTheme = theme?.Badge;

        var baseStyle = badgeTheme?.Base ?? "eq-badge";
        var variantStyle = badgeTheme?.GetVariant(Variant) ?? $"eq-badge-{Variant.ToString().ToLowerInvariant()}";

        var element = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"{baseStyle} {variantStyle} {ClassName}".Trim()
            }
        };

        if (Children.Any())
        {
            foreach (var child in Children) element.Children.Add(child);
        }
        else if (Text != null)
        {
            element.Children.Add(new Text(Text));
        }

        return element;
    }
}
