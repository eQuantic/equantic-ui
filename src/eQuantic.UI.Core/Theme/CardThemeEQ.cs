using eQuantic.UI.Core.Styling;
using eQuantic.UI.Core.Theme.Types;

namespace eQuantic.UI.Core.Theme;

/// <summary>
/// Built-in card theme using EQ styling classes.
/// Provides default styling for card components without external dependencies.
/// </summary>
public class CardThemeEQ : ICardTheme
{
    public string Container => (string)EQ.Card.Base;
    public string Header => (string)EQ.Card.Header;
    public string Body => (string)EQ.Card.Body;
    public string Footer => (string)EQ.Card.Footer;
    public string Title => "eq-text-lg eq-font-semibold eq-text-primary";
    public string Description => "eq-text-sm eq-text-secondary";

    public Dictionary<string, string> Shadows => new()
    {
        ["none"] = "eq-shadow-none",
        ["small"] = "eq-shadow-sm",
        ["medium"] = "eq-shadow-md",
        ["large"] = "eq-shadow-lg",
        ["xlarge"] = "eq-shadow-xl"
    };

    public string GetShadowInfo(string shadow)
    {
        return Shadows.TryGetValue(shadow.ToLowerInvariant(), out var value)
            ? value
            : "eq-shadow-md";  // Default to medium shadow
    }

    public string GetVariant(CardVariant variant)
    {
        return variant switch
        {
            CardVariant.Outline => "eq-bg-transparent eq-border eq-border-gray-200",
            CardVariant.Elevated => "eq-bg-surface eq-shadow-lg",
            CardVariant.Subtle => "eq-bg-surface-secondary eq-shadow-sm",
            CardVariant.Ghost => "eq-bg-transparent eq-border-none eq-shadow-none",
            CardVariant.Default => "",  // Use base styles
            _ => ""
        };
    }
}
