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
/// This does not guess. It reports the mode the app DECLARED (<see cref="UIOptions.UseInitialThemeMode"/>,
/// Light unless set), and <see cref="Apply"/> is deliberately inert: there is no document to stamp
/// on the server, and the client controller takes the switch over the moment the page hydrates.
/// Remembering a per-request change here would be worse than useless — this is a singleton, so it
/// would leak one visitor's choice into the next visitor's first paint.
/// </para>
/// </summary>
public sealed class SsrThemeController(ThemeMode initial = ThemeMode.Light) : IThemeController
{
    public ThemeMode Mode { get; } = initial;

    /// <summary>Intentionally does nothing — see the type's remarks.</summary>
    public void Apply(ThemeMode mode)
    {
    }
}
