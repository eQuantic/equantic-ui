using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Text theme implementation
/// </summary>
public class MaterialTextTheme : ITextTheme
{
    public string Base => "md-body-medium";

    public string GetVariant(Variant variant)
    {
        return variant switch
        {
            Variant.Primary => "text-[var(--md-sys-color-primary)]",
            Variant.Secondary => "text-[var(--md-sys-color-secondary)]",
            Variant.Destructive => "text-[var(--md-sys-color-error)]",
            _ => ""
        };
    }

    public string GetHeading(int level)
    {
        return level switch
        {
            1 => "md-display-large",
            2 => "md-display-medium",
            3 => "md-headline-large",
            4 => "md-headline-medium",
            5 => "md-title-large",
            6 => "md-title-medium",
            _ => "md-body-large"
        };
    }
}
