using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Surfaces;

/// <summary>
/// Card footer compound component.
/// Typically contains action buttons or secondary information.
/// </summary>
public class CardFooter : StatelessComponent
{
    /// <summary>
    /// Additional CSS classes to apply to the footer
    /// </summary>
    public string? AdditionalClass { get; set; }

    public override IComponent Build(RenderContext context)
    {
        var theme = context.GetService<Core.Theme.IAppTheme>();
        var cardTheme = theme?.Card;

        var footerClass = cardTheme?.Footer ?? "eq-card-footer";
        if (!string.IsNullOrEmpty(AdditionalClass))
        {
            footerClass = $"{footerClass} {AdditionalClass}";
        }
        if (!string.IsNullOrEmpty(ClassName))
        {
            footerClass = $"{footerClass} {ClassName}";
        }

        var box = new Box
        {
            ClassName = footerClass.Trim()
        };

        foreach (var child in Children)
        {
            box.Children.Add(child);
        }

        return box;
    }
}
