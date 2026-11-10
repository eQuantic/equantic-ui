namespace eQuantic.UI.Primitives;

/// <summary>
/// Where the app's icon is drawn, which <see cref="IAppIcon.Build"/> is told so that a brand with two
/// cuts of its mark draws the right one (#1240).
/// </summary>
public enum AppIconUse
{
    /// <summary>
    /// What a launcher shows: a home screen, a dock, a taskbar, an install prompt. The whole square,
    /// opaque, since every platform masks it and composites it against whatever it is showing.
    /// </summary>
    Launcher,

    /// <summary>
    /// The icon drawn SMALL on a surface that is not its own: a browser's tab, beside the page's title.
    /// Usually another cut of the same mark, transparent and with wider strokes, because at sixteen
    /// pixels on someone else's background the launcher's full square reads as a smudge. Its
    /// transparency is kept.
    /// </summary>
    Small,
}
