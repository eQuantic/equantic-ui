using System.Collections.Generic;
using eQuantic.UI.Core.Theme;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Material.Theme;

/// <summary>
/// Material Design 3 Card theme implementation
/// </summary>
public class MaterialCardTheme : ICardTheme
{
    public string Container => "md-card";
    public string Header => "md-card__header";
    public string Body => "md-card__content";
    public string Footer => "md-card__actions";
    public string Title => "md-title-large";
    public string Description => "md-body-medium";

    public Dictionary<string, string> Shadows { get; } = new()
    {
        { "none", "" },
        { "sm", "md-card--elevated" },
        { "md", "md-card--elevated" },
        { "lg", "md-card--elevated" },
        { "xl", "md-card--elevated" }
    };

    public string GetShadowInfo(string shadow)
    {
        return Shadows.TryGetValue(shadow, out var shadowClass) ? shadowClass : "";
    }

    public string GetVariant(CardVariant variant)
    {
        return variant switch
        {
            CardVariant.Outline => "md-card--outlined",
            CardVariant.Default => "md-card--filled",
            CardVariant.Elevated => "md-card--elevated",
            CardVariant.Subtle => "md-card--filled",
            CardVariant.Ghost => "",
            _ => "md-card--elevated"
        };
    }
}
