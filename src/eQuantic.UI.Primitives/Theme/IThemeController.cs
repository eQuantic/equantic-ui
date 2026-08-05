namespace eQuantic.UI.Primitives;

/// <summary>
/// The app's hand on the light/dark switch — a SERVICE, not a node, because the mode belongs to
/// the HOST: the window repaints, the browser stamps <c>data-theme</c>. A component that offers
/// the toggle asks for this the way it asks for a camera, and never learns which target answered.
/// Realized per head: the native runner attaches it to the window's host; the web registers a
/// controller that drives the same document flag SSR seeded.
/// </summary>
public interface IThemeController
{
    /// <summary>The mode the target is showing right now.</summary>
    ThemeMode Mode { get; }

    /// <summary>Applies the mode to the running target — a repaint on native, <c>data-theme</c>
    /// (and the atomic-class palette swap) on web.</summary>
    void Apply(ThemeMode mode);
}
