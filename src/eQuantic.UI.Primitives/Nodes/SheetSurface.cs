namespace eQuantic.UI.Primitives;

/// <summary>
/// An EDITABLE SPREADSHEET surface: the grid its child draws, plus Excel's selection band, the
/// active-cell ring, and the keyboard. The child is whatever renders the visible cells — the
/// shared Spreadsheet component, in practice — and this node adds the marks and the input.
/// Everything a key or a click MEANS lives in the <see cref="SheetController"/>, which both
/// targets share; the realizer only does arithmetic: with per-line sizes, a (row, col) is a
/// prefix sum over the visible window.
/// <para>
/// The controller is a live object the composing component owns, surviving the rebuild every
/// keystroke causes. <see cref="FirstRow"/>/<see cref="FirstCol"/> say which cell the child's
/// top-left is (the virtualized window's origin), so the surface's marks line up with the pixels.
/// </para>
/// </summary>
public sealed class SheetSurface : SingleChildNode
{
    public override string NodeKind => "sheetSurface";

    public SheetSurface(VisualNode child, SheetController controller)
        : base(child)
    {
        Controller = controller;
    }

    public SheetController Controller { get; init; }

    /// <summary>The window's origin: the sheet row/col the child's first cell renders.</summary>
    public int FirstRow { get; init; }
    public int FirstCol { get; init; }

    /// <summary>Where the GRID starts inside the child (past the row/column headers).</summary>
    public float HeaderWidth { get; init; }
    public float HeaderHeight { get; init; }

    /// <summary>Raised after any command that changed the document or the selection — the
    /// composing component's cue to SetState.</summary>
    public Action? OnChanged { get; init; }

    /// <summary>Accessible name (role: grid).</summary>
    public string? Label { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
