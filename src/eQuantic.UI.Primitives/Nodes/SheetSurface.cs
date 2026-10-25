namespace eQuantic.UI.Primitives;

/// <summary>
/// An EDITABLE SPREADSHEET surface: the grid its child draws, plus Excel's keyboard, pointer, text input
/// and clipboard. The child is whatever renders the visible cells (the shared Spreadsheet component, in
/// practice), and it draws the selection band, the active-cell ring and the cell's editor too, so this
/// node adds nothing to the picture but the entry's carets, which the realizer paints and blinks. Everything a key or a press MEANS belongs to the <see cref="Model"/>,
/// which both targets share: a realizer hands it the platform's events (<see cref="ISheetSurfaceModel"/>).
/// <para>
/// The model is a live object the composing component owns, surviving the rebuild every keystroke
/// causes, which is exactly why the surface carries it rather than a copy of its state. Where the
/// window's first cell is, and which cell that is, is the model's too: the component hands it over as
/// it builds.
/// </para>
/// </summary>
public sealed class SheetSurface : SingleChildNode
{
    public override string NodeKind => "sheetSurface";

    public SheetSurface(VisualNode child, ISheetSurfaceModel model)
        : base(child)
    {
        Model = model;
    }

    /// <summary>The document, the selection, and every command that changes either, as much of it
    /// as a realizer is allowed to see.</summary>
    public ISheetSurfaceModel Model { get; init; }

    /// <summary>Raised after any input that changed the document or the selection: the composing
    /// component's cue to SetState, because the model mutates outside the tree.</summary>
    public Action? OnChanged { get; init; }

    /// <summary>Accessible name (role: grid).</summary>
    public string? Label { get; init; }

    /// <summary>
    /// The entry's caret ink, carried HERE as a code surface carries its own: the realizer paints the
    /// carets over what the child drew, and only the component knows the ink the cell's text is in. Null
    /// falls back to the theme's <c>TextPrimary</c>.
    /// </summary>
    public ColorToken? CaretColor { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
