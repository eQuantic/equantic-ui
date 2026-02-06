using System.Collections.Generic;
using System.Linq;
using eQuantic.UI.Core;
using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Components;

/// <summary>
/// Button component with support for variants, sizes, and theming.
///
/// Usage examples:
/// <code>
/// // Basic button
/// new Button { Text = "Click me" }
///
/// // With variant and size
/// new Button {
///     Text = "Submit",
///     Variant = Variant.Primary,
///     Size = Size.Large,
///     OnClick = HandleClick
/// }
///
/// // With children components
/// new Button {
///     Variant = Variant.Outline,
///     Children = {
///         new Icon { Name = "check" },
///         new Text("Confirm")
///     }
/// }
/// </code>
/// </summary>
public class Button : StatelessComponent
{
    /// <summary>
    /// HTML button type attribute (button, submit, reset)
    /// </summary>
    public string Type { get; set; } = "button";

    /// <summary>
    /// Disable the button (prevents interaction)
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Button text content (alternative to using Children)
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Visual variant (Primary, Secondary, Destructive, Outline, Ghost, Link, Success, Warning, Info)
    /// </summary>
    public Variant Variant { get; set; } = Variant.Primary;

    /// <summary>
    /// Size variant (Small, Medium, Large, XLarge)
    /// </summary>
    public Size Size { get; set; } = Size.Medium;

    /// <summary>
    /// Show loading spinner and disable interaction
    /// </summary>
    public bool Loading { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<IAppTheme>();
        var buttonTheme = theme?.Button;

        var classValue = StyleBuilder.Create(buttonTheme?.Base)
                            .Add(buttonTheme?.GetVariant(Variant))
                            .Add(buttonTheme?.GetSize(Size))
                            .Add(ClassName)
                            .Build();

        var attrs = new Dictionary<string, string>
        {
            ["type"] = Type,
            ["class"] = classValue
        };

        // Disable button when loading or explicitly disabled
        if (Disabled || Loading) attrs["disabled"] = "true";
        if (Loading) attrs["data-loading"] = "true";

        var element = new DynamicElement
        {
            TagName = "button",
            CustomAttributes = attrs,
            CustomEvents = BuildEvents()
        };

        // Add loading spinner if loading
        if (Loading)
        {
            element.Children.Add(CreateSpinner());
        }

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

    private IComponent CreateSpinner()
    {
        // Simple CSS-based spinner (single div with animation)
        return new DynamicElement
        {
            TagName = "span",
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = "eq-btn-spinner",
                ["aria-hidden"] = "true"
            }
        };
    }
}
