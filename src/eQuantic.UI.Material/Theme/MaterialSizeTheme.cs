using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Size theme implementation
/// </summary>
public class MaterialSizeTheme : ISizeTheme
{
    public string GetFontSize(Size size)
    {
        return size switch
        {
            Size.Small => "md-label-small",
            Size.Medium => "md-body-medium",
            Size.Large => "md-body-large",
            _ => "md-body-medium"
        };
    }

    public string GetPadding(Size size)
    {
        return size switch
        {
            Size.Small => "p-2",
            Size.Medium => "p-4",
            Size.Large => "p-6",
            _ => "p-4"
        };
    }

    public string GetRadius(Size size)
    {
        return size switch
        {
            Size.Small => "rounded-[var(--md-sys-shape-corner-small)]",
            Size.Medium => "rounded-[var(--md-sys-shape-corner-medium)]",
            Size.Large => "rounded-[var(--md-sys-shape-corner-large)]",
            _ => "rounded-[var(--md-sys-shape-corner-medium)]"
        };
    }
}
