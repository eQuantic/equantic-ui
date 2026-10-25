namespace eQuantic.UI.Server;

/// <summary>How the theme is remembered — see <see cref="UIOptions.UseThemeCookie"/>.</summary>
public sealed class ThemeCookieOptions
{
    public string Name { get; init; } = "eq-theme";
    public int Days { get; init; } = 365;
}
