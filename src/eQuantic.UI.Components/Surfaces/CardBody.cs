using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Surfaces;

/// <summary>
/// Card body/content compound component.
/// Contains the main content of the card.
/// </summary>
public class CardBody : StatelessComponent
{
    /// <summary>
    /// Additional CSS classes to apply to the body
    /// </summary>
    public string? AdditionalClass { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var cardTheme = theme?.Card;

        var bodyClass = cardTheme?.Body ?? "";
        if (!string.IsNullOrEmpty(AdditionalClass))
        {
            bodyClass = $"{bodyClass} {AdditionalClass}";
        }
        if (!string.IsNullOrEmpty(ClassName))
        {
            bodyClass = $"{bodyClass} {ClassName}";
        }

        var box = new Box
        {
            ClassName = bodyClass.Trim()
        };

        foreach (var child in Children)
        {
            box.Children.Add(child);
        }

        return box;
    }
}
