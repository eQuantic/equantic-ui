using eQuantic.UI.Core.Theme;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Tabs theme implementation
/// </summary>
public class MaterialTabsTheme : ITabsTheme
{
    public string List => "md-tabs__list";
    public string Trigger => "md-tabs__tab";
    public string Content => "md-tabs__panel";
    public string ActiveTrigger => "md-tabs__tab--active";
    public string InactiveTrigger => "md-tabs__tab--inactive";
}
