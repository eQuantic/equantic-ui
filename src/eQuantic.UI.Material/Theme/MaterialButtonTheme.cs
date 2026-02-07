using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Button theme implementation
/// </summary>
public class MaterialButtonTheme : IButtonTheme
{
    public string Base => "md-button";

    public string GetVariant(Variant variant)
    {
        return variant switch
        {
            Variant.Primary => "md-button--filled",
            Variant.Secondary => "md-button--tonal",
            Variant.Ghost => "md-button--text",
            Variant.Destructive => "md-button--filled md-button--error",
            Variant.Outline => "md-button--outlined",
            Variant.Link => "md-button--text",
            Variant.Default => "md-button--elevated",
            _ => "md-button--filled"
        };
    }

    public string GetSize(Size size)
    {
        return size switch
        {
            Size.Small => "md-button--small",
            Size.Medium => "",
            Size.Large => "md-button--large",
            _ => ""
        };
    }
}
