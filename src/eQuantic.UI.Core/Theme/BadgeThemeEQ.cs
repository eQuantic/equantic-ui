using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

public class BadgeThemeEQ : IBadgeTheme
{
    public string Base => "eq-badge";

    public string GetVariant(Variant variant)
    {
        return variant switch
        {
            Variant.Primary => "eq-badge-primary",
            Variant.Secondary => "eq-badge-secondary",
            Variant.Success => "eq-badge-success",
            Variant.Warning => "eq-badge-warning",
            Variant.Destructive => "eq-badge-destructive",
            Variant.Info => "eq-badge-info",
            _ => "eq-badge-default"
        };
    }
}
