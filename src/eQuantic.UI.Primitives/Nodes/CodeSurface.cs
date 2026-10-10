namespace eQuantic.UI.Primitives;

/// <summary>
/// An EDITABLE code surface: the drawing its child does, plus the carets, the selection, and a
/// keyboard and pointer to move them with.
/// <para>
/// The child is whatever renders the lines — a <c>CodeBlock</c>, in practice — and this node adds
/// nothing to the picture except the marks that say where you are. Everything a key or a click
/// MEANS, and where every mark goes, belongs to the <see cref="Model"/>, which both targets share: a
/// realizer hands it the platform's events and paints the rectangles it answers
/// (<see cref="ICodeSurfaceModel"/>).
/// </para>
/// <para>
/// The model is a live object the composing component owns, so it survives the rebuild each
/// keystroke causes — which is exactly why the surface carries it rather than a copy of its state.
/// </para>
/// </summary>
public sealed class CodeSurface : SingleChildNode
{
    /// <summary>
    /// How long the caret holds each phase, in ms — 500 on, 500 off. Normative for BOTH targets: a
    /// window blinks it from the host's clock (which is also what paces its frames), a page from a
    /// CSS animation over twice this period.
    /// </summary>
    public const int CaretBlinkMs = 500;

    public override string NodeKind => "codeSurface";

    public CodeSurface(VisualNode child, ICodeSurfaceModel model)
        : base(child)
    {
        Model = model;
    }

    /// <summary>The document, the selection, and every command that changes either — as much of it
    /// as a realizer is allowed to see.</summary>
    public ICodeSurfaceModel Model { get; init; }

    /// <summary>Raised after any input that changed the document or the selection — the composing
    /// component's cue to SetState, because the model mutates outside the tree.</summary>
    public Action? OnChanged { get; init; }

    /// <summary>Accessible name (role: textbox, multiline).</summary>
    public string? Label { get; init; }

    /// <summary>Takes the caret when it first appears — a panel that opens ready to type.</summary>
    public bool Autofocus { get; init; }

    /// <summary>
    /// The caret's ink, carried HERE rather than decided by each realizer: an editor on an inverse
    /// slab writes with an ink of its own, and a caret painted from the page's theme is invisible on
    /// exactly the surface people type into. Null falls back to the theme's <c>TextPrimary</c>. (The
    /// selection needs no ink here: the component draws it, in the code's own layers.)
    /// </summary>
    public ColorToken? CaretColor { get; init; }

    /// <summary>
    /// What the surface OFFERS at its caret, a completion list: drawn over the code and its caret at
    /// <see cref="OptionsOrigin"/>, in the surface's own coordinates (the ones the model's rectangles
    /// are in), so it moves with the code as the code scrolls. The surface owns it, rather than a
    /// node beside it, because its INPUT is what assistive technology asks: on the web the input says
    /// it completes from a list and, while options show, names it and points at
    /// <see cref="HighlightedOption"/>. Null offers nothing.
    /// </summary>
    public VisualNode? Options { get; init; }

    /// <summary>Where <see cref="Options"/> stands: its top left, in the surface's coordinates.</summary>
    public Point OptionsOrigin { get; init; }

    /// <summary>
    /// Which option the keyboard is on, counted in tree order over the pressables inside
    /// <see cref="Options"/> whose role is <see cref="PressableRole.Option"/>, or -1. The web lowers it
    /// as the input's <c>aria-activedescendant</c>, and Photon announces that row selected.
    /// </summary>
    public int HighlightedOption { get; init; } = -1;

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
