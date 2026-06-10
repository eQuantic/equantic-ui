using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Size theme implementation
/// </summary>
public class MaterialSizeTheme : ISizeTheme
{
    public string GetFontSize(SizeVariant size)
    {
        return size switch
        {
            SizeVariant.Small => "md-label-small",
            SizeVariant.Medium => "md-body-medium",
            SizeVariant.Large => "md-body-large",
            _ => "md-body-medium"
        };
    }

    public string GetPadding(SizeVariant size)
    {
        return size switch
        {
            SizeVariant.Small => "p-2",
            SizeVariant.Medium => "p-4",
            SizeVariant.Large => "p-6",
            _ => "p-4"
        };
    }

    public string GetRadius(SizeVariant size)
    {
        return size switch
        {
            SizeVariant.Small => "rounded-[var(--md-sys-shape-corner-small)]",
            SizeVariant.Medium => "rounded-[var(--md-sys-shape-corner-medium)]",
            SizeVariant.Large => "rounded-[var(--md-sys-shape-corner-large)]",
            _ => "rounded-[var(--md-sys-shape-corner-medium)]"
        };
    }
}
