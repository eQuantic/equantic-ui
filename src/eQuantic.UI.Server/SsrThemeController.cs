using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server;

/// <summary>
/// The light/dark hand DURING SERVER RENDERING — the half of <see cref="IThemeController"/> the web
/// was missing.
/// <para>
/// The interface's own contract says the web "registers a controller that drives the same document
/// flag SSR seeded", and the client half does exactly that (<c>WebThemeController</c>). Nothing
/// answered on the server, though, so a component offering the toggle resolved nothing and had to
/// GUESS which mode it was drawing — and a guess in SSR is not a small thing: it decides the markup
/// the browser receives, so guessing wrong means the first paint is the wrong theme and hydration
/// corrects it in front of the reader.
/// </para>
/// <para>
/// This does not guess. It reports, in order: the mode the VISITOR last chose (a cookie the
/// browser's own controller writes on a toggle), then the one the app declared
/// (<see cref="UIOptions.UseInitialThemeMode"/>), then Light.
/// </para>
/// <para>
/// A cookie rather than localStorage because the requirement is not "remember" but "TELL THE
/// SERVER": localStorage remembers perfectly and the server cannot see a word of it, so the page
/// would arrive in the default mode and be corrected at hydration — the flash this removes.
/// </para>
/// <para>
/// <see cref="Apply"/> stays inert. There is no document to stamp on the server, the browser's
/// controller takes the switch over at hydration, and remembering a change HERE would be actively
/// wrong: this is a singleton, so it would hand one visitor's choice to the next visitor's first
/// paint.
/// </para>
/// </summary>
public sealed class SsrThemeController(
    Microsoft.AspNetCore.Http.IHttpContextAccessor? requests = null,
    ThemeMode? declared = null) : IThemeController
{
    /// <summary>
    /// What THIS request paints in: the visitor's remembered choice, else the app's declared
    /// default, else Light.
    /// <para>
    /// Read per call rather than captured once, because this is a singleton serving every request
    /// and a captured value would hand one visitor's choice to the next visitor's first paint. The
    /// cookie is written by the browser's own controller on a toggle — the server can read it,
    /// which is the whole reason it is a cookie and not localStorage.
    /// </para>
    /// </summary>
    public ThemeMode Mode => ThemeCookie.Resolve(requests?.HttpContext, declared) ?? ThemeMode.Light;

    /// <summary>Intentionally does nothing — see the type's remarks.</summary>
    public void Apply(ThemeMode mode)
    {
    }
}
