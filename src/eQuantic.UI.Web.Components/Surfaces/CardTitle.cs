using eQuantic.UI.Core;

namespace eQuantic.UI.Web.Components.Surfaces;

/// <summary>
/// Card title compound component.
/// Renders the main heading within a CardHeader.
/// </summary>
public class CardTitle : StatelessComponent
{
    /// <summary>
    /// The title text to display
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// HTML tag to use (default: h3)
    /// </summary>
    public string Tag { get; set; } = "h3";

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var cardTheme = theme?.Card;

        var titleClass = cardTheme?.Title ?? "";
        if (!string.IsNullOrEmpty(ClassName))
        {
            titleClass = $"{titleClass} {ClassName}";
        }

        var element = new Box
        {
            As = Tag,
            ClassName = titleClass.Trim()
        };

        // Add text if provided
        if (!string.IsNullOrEmpty(Text))
        {
            element.Children.Add(new Text(Text));
        }

        // Add children components
        foreach (var child in Children)
        {
            element.Children.Add(child);
        }

        return element;
    }
}
