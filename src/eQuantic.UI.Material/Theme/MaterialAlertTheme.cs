using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Alert/Snackbar theme implementation (stub)
/// </summary>
public class MaterialAlertTheme : IAlertTheme
{
    public string Base => "md-card md-card--filled";
    public string Icon => "md-icon-button";
    public string Title => "md-title-medium";
    public string Description => "md-body-medium";

    public string GetVariant(Variant variant)
    {
        return variant switch
        {
            Variant.Destructive => "border-l-4 border-[var(--md-sys-color-error)]",
            Variant.Primary => "border-l-4 border-[var(--md-sys-color-primary)]",
            _ => ""
        };
    }
}
