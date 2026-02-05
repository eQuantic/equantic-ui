using eQuantic.UI.Core.Styling;

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
}
