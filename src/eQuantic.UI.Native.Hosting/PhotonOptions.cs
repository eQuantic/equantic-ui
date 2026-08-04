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

    /// <summary>
    /// How much of the window the system draws. <see cref="WindowChrome.Unified"/> gives the whole
    /// window to the app and reports the strip the window controls sit in as a top safe-area inset
    /// — the same channel a phone reports its notch through, so the tree that handles one handles
    /// the other.
    /// </summary>
    public WindowChrome Chrome { get; set; } = WindowChrome.Standard;

    /// <summary>Whether the user may resize the window. A device ignores it.</summary>
    public bool Resizable { get; set; } = true;

    /// <summary>The smallest the window may be dragged to. Zero leaves the shell's own floor.</summary>
    public float MinWidth { get; set; }

    public float MinHeight { get; set; }

    /// <summary>
    /// Whether a wheel or drag GLIDES to where it was sent instead of jumping there. On by default
    /// — a jump reads as a redraw rather than as movement, and the eye loses its place. Reduce
    /// Motion turns it off whatever this says.
    /// </summary>
    public bool SmoothScroll { get; set; } = true;

    /// <summary>
    /// What this app declared it needs of the device, and why — the same sentences the system shows.
    /// Useful for the screen an app puts up BEFORE asking, which is the difference between a prompt
    /// that gets granted and one that gets dismissed.
    /// </summary>
    public IReadOnlyDictionary<DeviceCapability, string> Declared { get; set; } =
        new Dictionary<DeviceCapability, string>();

    /// <summary>Stops after this many presented frames. Zero runs until the app is closed.</summary>
    public int MaxFrames { get; set; }
}
