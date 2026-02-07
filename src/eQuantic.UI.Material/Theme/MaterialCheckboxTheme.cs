using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Checkbox theme implementation
/// </summary>
public class MaterialCheckboxTheme : ICheckboxTheme
{
    public string Base => "md-checkbox__container";
    public string Checked => ""; // Handled by CSS :checked pseudo-class
    public string Unchecked => "";
    public string Root => "md-checkbox";
    public string Indicator => "md-checkbox__checkmark";
}
