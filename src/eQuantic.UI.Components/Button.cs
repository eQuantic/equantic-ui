using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components;

/// <summary>
/// Button component
/// </summary>
public class Button : StatelessComponent
{
    public string Type { get; set; } = "button";
    public bool Disabled { get; set; }
    public string? Text { get; set; }
    public Variant Variant { get; set; } = Variant.Primary;
    public Size Size { get; set; } = Size.Medium;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var buttonTheme = theme?.Button;

        var attrs = new Dictionary<string, string>
        {
            ["type"] = this.Type,
            ["class"] = StyleBuilder.Create(buttonTheme?.Base)
                            .Add(buttonTheme?.GetVariant(Variant))
                            .Add(buttonTheme?.GetSize(Size))
                            .Add(ClassName)
                            .Build()
        };

        if (this.Disabled) attrs["disabled"] = "true";

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
