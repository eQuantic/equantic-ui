using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Select/Menu theme implementation (stub)
/// </summary>
public class MaterialSelectTheme : ISelectTheme
{
    public string Base => "md-select";
    public string Trigger => "md-button md-button--outlined";
    public string Content => "md-menu";
    public string Item => "md-menu-item";
    public string Selected => "md-menu-item--selected";
    public string Label => "md-menu-label";
    public string Separator => "md-menu-divider";
    public string Group => "md-menu-group";
}
