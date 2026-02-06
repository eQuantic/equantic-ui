using System.Collections.Generic;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components.Feedback;

/// <summary>
/// Alert component for user feedback.
/// </summary>
public class Alert : StatelessComponent
{
    public Variant Variant { get; set; } = Variant.Default;

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var alertTheme = theme?.Alert;

        var baseStyle = alertTheme?.Base ?? "";
        var variantStyle = alertTheme?.GetVariant(Variant) ?? "";

        var element = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["role"] = "alert",
                ["class"] = $"{baseStyle} {variantStyle} {ClassName}".Trim()
            }
        };

        foreach(var child in Children) element.Children.Add(child);
        return element;
    }
}

public class AlertTitle : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var style = theme?.Alert.Title ?? "";

        var element = new DynamicElement
        {
            TagName = "h5",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"{style} {ClassName}".Trim()
            }
        };
        foreach(var child in Children) element.Children.Add(child);
        return element;
    }
}

public class AlertDescription : StatelessComponent
{
    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var style = theme?.Alert.Description ?? "";

        var element = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"{style} {ClassName}".Trim()
            }
        };
        foreach(var child in Children) element.Children.Add(child);
        return element;
    }
}
