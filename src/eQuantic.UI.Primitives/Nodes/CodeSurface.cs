namespace eQuantic.UI.Primitives;

/// <summary>
/// An EDITABLE code surface: the drawing its child does, plus a caret, a selection, and a keyboard.
/// <para>
/// The child is whatever renders the lines — a <c>CodeBlock</c>, in practice — and this node adds
/// nothing to the picture except the two marks that say where you are. Everything a key or a click
/// MEANS is in the controller, which both targets share, so a realizer only has to do arithmetic:
/// with a monospaced face, a (line, column) is <c>(top + line × lineHeight, left + column × columnWidth)</c>
/// and nothing has to be measured per keystroke.
/// </para>
/// <para>
/// The controller is a live object the composing component owns, so it survives the rebuild each
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

    public CodeSurface(VisualNode child, CodeEditorController editor)
        : base(child)
    {
        Editor = editor;
    }


    /// <summary>The document, the selection, and every command that changes either.</summary>
    public CodeEditorController Editor { get; init; }

    /// <summary>Where the first line's top sits inside this surface, and how tall each line is.</summary>
    public float ContentTop { get; init; }
    public float LineHeight { get; init; } = 18;

    /// <summary>Where column 0 sits (past the gutter), and the advance of ONE character. With a
    /// fixed pitch these two numbers place every caret in the document.</summary>
    public float ContentLeft { get; init; }
    public float ColumnWidth { get; init; } = 8;

    /// <summary>Raised after any command that changed the document or the selection — the composing
    /// component's cue to SetState, because the controller mutates outside the tree.</summary>
    public Action? OnChanged { get; init; }

    /// <summary>Accessible name (role: textbox, multiline).</summary>
    public string? Label { get; init; }

    /// <summary>Takes the caret when it first appears — a panel that opens ready to type.</summary>
    public bool Autofocus { get; init; }

    /// <summary>
    /// The two marks' ink, carried HERE rather than decided by each realizer: an editor on an
    /// inverse slab writes with an ink of its own, and a caret painted from the page's theme is
    /// invisible on exactly the surface people type into. Null falls back to the theme
    /// (<c>TextPrimary</c> for the caret, <c>FocusRing</c> for the band).
    /// </summary>
    public ColorToken? CaretColor { get; init; }
    public ColorToken? SelectionColor { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
