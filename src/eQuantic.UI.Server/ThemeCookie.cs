using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>The cookie the browser's theme controller writes, and the only reason the server can
/// paint a visitor's chosen mode on the first byte.</summary>
public static class ThemeCookie
{
    public const string Name = "eq-theme";

    /// <summary>
    /// The mode this request should paint in: the visitor's remembered choice, or
    /// <paramref name="declared"/> when they have not made one (null there means "follow the OS",
    /// which the server cannot resolve and must leave to CSS).
    /// <para>
    /// An unrecognised cookie value is ignored rather than trusted — it is user-supplied text, and
    /// the only two answers this can have are light and dark.
    /// </para>
    /// </summary>
    public static eQuantic.UI.Primitives.ThemeMode? Resolve(
        HttpContext? context, eQuantic.UI.Primitives.ThemeMode? declared, string? cookieName = null) =>
        context?.Request.Cookies[cookieName ?? Name] switch
        {
            "dark" => eQuantic.UI.Primitives.ThemeMode.Dark,
            "light" => eQuantic.UI.Primitives.ThemeMode.Light,
            _ => declared,
        };
}
