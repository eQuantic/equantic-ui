namespace eQuantic.UI.Primitives;

/// <summary>
/// What an embedded document is ALLOWED to do — the vocabulary's word for the iframe sandbox,
/// composed instead of spelled. <see cref="None"/> is the fully locked-down frame; every flag
/// hands one capability back.
/// </summary>
[Flags]
public enum WebSandbox
{
    /// <summary>Maximum isolation: no scripts, no origin, no forms, no popups.</summary>
    None = 0,

    /// <summary>The document may run script.</summary>
    Scripts = 1,

    /// <summary>The document keeps the host's origin — required for it to fetch same-origin
    /// resources (module imports, styles). Trust the content before granting it.</summary>
    SameOrigin = 2,

    /// <summary>The document may submit forms.</summary>
    Forms = 4,

    /// <summary>The document may open new windows.</summary>
    Popups = 8,
}

/// <summary>
/// An embedded web DOCUMENT — someone else's page by address, or a document handed over whole —
/// presented isolated from the tree around it. The playground's live preview, an embedded map, a
/// sandboxed demo: content that must render without being able to touch the app.
/// <para>
/// Isolation is the point, and it is TYPED: <see cref="Sandbox"/> starts at
/// <see cref="WebSandbox.Scripts"/> alone, and every further capability is granted by name. The
/// frame never dictates its own size — a document has no intrinsic extent the layout could ask
/// for — so it fills what the parent offers unless given explicit dp.
/// </para>
/// <para>
/// Web-only for now: the web realizer lowers it to a sandboxed <c>iframe</c>; Photon has no
/// web-content surface yet, so a native realizer treats it like any unknown node. When a native
/// webview lands it will realize THIS node — the app code does not change.
/// </para>
/// </summary>
public sealed class WebFrame : VisualNode
{
    public sealed override string NodeKind => "webFrame";

    /// <summary>An address to load — the embed-by-URL form. Ignored when
    /// <see cref="Document"/> is set.</summary>
    public string? Source { get; init; }

    /// <summary>The whole document, inline — the playground form: markup that exists only in
    /// memory, never at an address.</summary>
    public string? Document { get; init; }

    /// <summary>What the document may do. Scripts only, until told otherwise.</summary>
    public WebSandbox Sandbox { get; init; } = WebSandbox.Scripts;

    /// <summary>What assistive tech announces the frame as. Every embedded document needs one —
    /// a screen reader cannot describe a page it cannot enter.</summary>
    public string Title { get; init; } = "";

    public SizeValue Width { get; init; } = SizeValue.Fill;
    public SizeValue Height { get; init; } = SizeValue.Fill;

    /// <summary>Radius clips via rrect, exactly as <see cref="Image"/> does.</summary>
    public CornerRadii CornerRadius { get; init; }
}
