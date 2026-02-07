using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Badge theme implementation (stub)
/// </summary>
public class MaterialBadgeTheme : IBadgeTheme
{
    public string Base => "md-label-small";

    public string GetVariant(Variant variant)
    {
        return variant switch
        {
            Variant.Primary => "bg-[var(--md-sys-color-primary)] text-[var(--md-sys-color-on-primary)]",
            Variant.Secondary => "bg-[var(--md-sys-color-secondary)] text-[var(--md-sys-color-on-secondary)]",
            Variant.Destructive => "bg-[var(--md-sys-color-error)] text-[var(--md-sys-color-on-error)]",
            _ => "bg-[var(--md-sys-color-surface-container)] text-[var(--md-sys-color-on-surface)]"
        };
    }
}
