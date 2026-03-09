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

        var baseStyle = alertTheme?.Base ?? "eq-alert";
        var variantStyle = alertTheme?.GetVariant(Variant) ?? $"eq-alert-{Variant.ToString().ToLowerInvariant()}";

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
    /// <summary>
    /// The title text to display
    /// </summary>
    public string? Text { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var style = theme?.Alert.Title ?? "eq-alert-title";

        var element = new DynamicElement
        {
            TagName = "h5",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"{style} {ClassName}".Trim()
            }
        };

        if (!string.IsNullOrEmpty(Text))
        {
            element.Children.Add(new Text(Text));
        }

        foreach(var child in Children) element.Children.Add(child);
        return element;
    }
}

public class AlertDescription : StatelessComponent
{
    /// <summary>
    /// The description text to display
    /// </summary>
    public string? Text { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var style = theme?.Alert.Description ?? "eq-alert-description";

        var element = new DynamicElement
        {
            TagName = "div",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = $"{style} {ClassName}".Trim()
            }
        };

        if (!string.IsNullOrEmpty(Text))
        {
            element.Children.Add(new Text(Text));
        }

        foreach(var child in Children) element.Children.Add(child);
        return element;
    }
}
