namespace eQuantic.UI.Sheet;

/// <summary>
/// One undoable step, stored as its own INVERSE: enough to put the sheet back exactly. Snapshots
/// are per-populated-cell, never per-grid — a sparse document undoes sparsely.
/// </summary>
public sealed class SheetEdit
{
    public SheetEditKind Kind { get; init; }

    /// <summary>SetCells: what each touched cell held BEFORE and holds AFTER.</summary>
    public List<SheetCellSnapshot> Before { get; init; } = new();
    public List<SheetCellSnapshot> After { get; init; } = new();

    /// <summary>Structure edits: the axis index the band starts at and how many lines it spans.</summary>
    public int At { get; init; }
    public int Count { get; init; }

    /// <summary>DeleteRows/DeleteCols: the removed band's populated cells, for restore.</summary>
    public List<SheetCellSnapshot> Removed { get; init; } = new();

    /// <summary>Resize edits: the size the line had and the size it has.</summary>
    public float OldSize { get; init; }
    public float NewSize { get; init; }

    /// <summary>Where the selection stood when the edit happened / after it — undo restores the
    /// cursor to where the user was, which is half of what makes undo feel safe. Settable because
    /// a paste only knows its landing rectangle after walking the grid.</summary>
    public SheetRange SelectionBefore { get; set; }
    public SheetRange SelectionAfter { get; set; }
}
