using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Surfaces;

/// <summary>
/// Card header compound component.
/// Typically contains CardTitle and CardDescription.
/// </summary>
public class CardHeader : StatelessComponent
{
    /// <summary>
    /// Additional CSS classes to apply to the header
    /// </summary>
    public string? AdditionalClass { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var cardTheme = theme?.Card;

        var headerClass = cardTheme?.Header ?? "eq-card-header";
        if (!string.IsNullOrEmpty(AdditionalClass))
        {
            headerClass = $"{headerClass} {AdditionalClass}";
        }
        if (!string.IsNullOrEmpty(ClassName))
        {
            headerClass = $"{headerClass} {ClassName}";
        }

        var box = new Box
        {
            ClassName = headerClass.Trim()
        };

        foreach (var child in Children)
        {
            box.Children.Add(child);
        }

        return box;
    }
}
