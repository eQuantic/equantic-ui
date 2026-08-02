using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Hosting;

/// <summary>
/// What the app is, as opposed to what it does: its theme, the name on the window, and the surface
/// a desktop stands in with. Bound from configuration under <c>Photon</c>, so appsettings.json,
/// environment variables and command-line arguments all reach it the way a .NET developer expects.
/// </summary>
public sealed class PhotonOptions
{
    /// <summary>The configuration section these are bound from.</summary>
    public const string SectionName = "Photon";

    /// <summary>The design system the whole tree resolves against.</summary>
    public IAppTheme Theme { get; set; } = PhotonTheme.Instance;

    /// <summary>
    /// Null FOLLOWS the system's light/dark setting, which is what an app should do. A value pins
    /// it, which is what a screenshot wants.
    /// </summary>
    public ThemeMode? Mode { get; set; }

    /// <summary>The window's title. A phone has no window and ignores it.</summary>
    public string Title { get; set; } = "eQuantic";

    /// <summary>
    /// The surface a DESKTOP stands in with. A device reports its own size and pays no attention to
    /// these — which is why a phone app can be iterated on at the geometry it was drawn for.
    /// </summary>
    public float Width { get; set; } = 390;

    public float Height { get; set; } = 844;

    /// <summary>Stops after this many presented frames. Zero runs until the app is closed.</summary>
    public int MaxFrames { get; set; }
}
