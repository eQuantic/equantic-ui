using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

public class AlertThemeEQ : IAlertTheme
{
    public string Base => "eq-alert";
    public string Icon => "eq-alert-icon";
    public string Title => "eq-alert-title";
    public string Description => "eq-alert-description";

    public string GetVariant(Variant variant)
    {
        return variant switch
        {
            Variant.Primary => "eq-alert-primary",
            Variant.Secondary => "eq-alert-secondary",
            Variant.Success => "eq-alert-success",
            Variant.Warning => "eq-alert-warning",
            Variant.Destructive => "eq-alert-destructive",
            Variant.Info => "eq-alert-info",
            _ => "eq-alert-default"
        };
    }
}
