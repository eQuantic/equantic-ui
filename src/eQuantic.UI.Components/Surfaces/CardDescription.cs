using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Surfaces;

/// <summary>
/// Card description compound component.
/// Renders a secondary text description within a CardHeader.
/// </summary>
public class CardDescription : StatelessComponent
{
    /// <summary>
    /// The description text to display
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// HTML tag to use (default: p)
    /// </summary>
    public string Tag { get; set; } = "p";

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<eQuantic.UI.Core.Theme.IAppTheme>();
        var cardTheme = theme?.Card;

        var descClass = cardTheme?.Description ?? "";
        if (!string.IsNullOrEmpty(ClassName))
        {
            descClass = $"{descClass} {ClassName}";
        }

        var element = new DynamicElement
        {
            TagName = Tag,
            CustomAttributes = new Dictionary<string, string>
            {
                ["class"] = descClass.Trim()
            }
        };

        // Add text if provided
        if (!string.IsNullOrEmpty(Text))
        {
            element.Children.Add(new Text(Text));
        }

        // Add children components
        foreach (var child in this.Children)
        {
            element.Children.Add(child);
        }

        return element;
    }
}
