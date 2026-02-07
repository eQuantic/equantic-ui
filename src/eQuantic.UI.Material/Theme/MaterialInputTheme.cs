using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Input theme implementation
/// </summary>
public class MaterialInputTheme : IInputTheme
{
    public string Base => "md-text-field md-text-field--filled";

    public string GetVariant(Variant variant)
    {
        return variant switch
        {
            Variant.Destructive => "md-text-field--error",
            _ => ""
        };
    }

    public string GetSize(Size size)
    {
        // M3 text fields have standard sizing
        return "";
    }
}
