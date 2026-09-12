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
    /// <para>
    /// Following is read at start on every target, and Windows additionally re-reads it when the
    /// user changes the setting while the app is open (<c>WM_SETTINGCHANGE</c>). macOS does NOT yet
    /// notice a change mid-session — the appearance is read once — so a Mac user switching to dark
    /// with the app open sees it on the next launch. iOS and Android follow their own trait change.
    /// </para>
    /// <para>
    /// An app that paints its OWN surfaces and leaves this alone is the trap here, and it fails
    /// silently and totally rather than partially: every component resolves its foreground for the
    /// mode this says, so dark panels under a light mode compute dark-on-dark and a whole pane goes
    /// unreadable while remaining present in the tree and in the accessibility count. Painting your
    /// own background is not a supported halfway house — own the palette by providing an
    /// <see cref="IAppTheme"/>, which is what resolves the foregrounds too.
    /// </para>
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

    /// <summary>
    /// Refuse to exit zero if any component was CONTAINED — <c>--Photon:StrictRender true</c>. Off
    /// by default, because containment is the right behaviour in a shipped app and an exit code is
    /// not the place to tell a user about it. On for a self-test or a CI gate, where the opposite
    /// is true.
    /// <para>
    /// The gap this closes: a boundary turns a loud failure into a quiet one ON PURPOSE, and every
    /// automated signal sides with the quiet version. An app whose entire title bar threw on every
    /// frame presented its frames, exited zero, and reported MORE accessibility elements than a
    /// healthy one, because the containment surface has text of its own. A consumer had "frames
    /// presented and exit 0" written down as the check that catches a black window, and it could
    /// not. The contained names print either way; this decides whether the exit code says so.
    /// </para>
    /// </summary>
    public bool StrictRender { get; set; }

    /// <summary>
    /// Render ONE settled frame headlessly (reference backend) to this PNG path and exit — no
    /// window, no GPU. What a CI screenshot step or a fidelity pass against a design handoff
    /// calls: `--Photon:ScreenshotPath out.png` (+ `--Photon:Mode Dark` for the other palette).
    /// <para>
    /// A LAYOUT AND COLOUR check, not a behaviour one. A fixed number of frames is built against a
    /// synthetic clock and the image is written: no real time passes, so anything the app started
    /// asynchronously — a tool, a query, a file read — has not arrived and never will in that
    /// image. A panel that says "loading…" in every screenshot is usually this and not a bug in the
    /// panel. <see cref="MaxFrames"/> sits next to this property and does NOT apply to it.
    /// </para>
    /// </summary>
    public string? ScreenshotPath { get; set; }
}
