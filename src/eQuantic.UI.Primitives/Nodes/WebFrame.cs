namespace eQuantic.UI.Primitives;

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
/// Both of its constructor arguments are arguments and not properties BECAUSE they are not
/// optional. <see cref="WebContent"/> makes the address-or-document choice a value rather than
/// two nullable strings with a precedence between them, and <see cref="Title"/> is what assistive
/// tech announces the frame as — a screen reader cannot describe a page it cannot enter, so a
/// frame without one is not a frame anybody can use. It was an <c>init</c> property defaulting to
/// the empty string while its own doc called it mandatory.
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

    public WebFrame(WebContent content, string title)
    {
        Content = content;
        Title = title;
    }

    /// <summary>The address to load, or the whole document inline — exactly one of the two.</summary>
    public WebContent Content { get; }

    /// <summary>What assistive tech announces the frame as.</summary>
    public string Title { get; }

    /// <summary>What the document may do. Scripts only, until told otherwise.</summary>
    public WebSandbox Sandbox { get; init; } = WebSandbox.Scripts;

    /// <summary>
    /// The extent, defaulting to FILL — a document has none of its own to be asked for.
    /// <para>
    /// These two are the reason the <c>UI.WebFrame</c> factory carries no tail for them: a tail
    /// parameter's default is written in the factory's signature, C# has no constant expression
    /// for <see cref="SizeValue.Fill"/>, and a <c>= default</c> would quietly hand back a HUG
    /// frame from the factory where <c>new</c> gives a filling one. The factory surface's whole
    /// promise is that named arguments carry between the two forms unchanged, so the two that
    /// cannot keep it stay reachable only through the initializer.
    /// </para>
    /// </summary>
    public SizeValue Width { get; init; } = SizeValue.Fill;
    public SizeValue Height { get; init; } = SizeValue.Fill;

    /// <summary>Radius clips via rrect, exactly as <see cref="Image"/> does.</summary>
    public CornerRadii CornerRadius { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
