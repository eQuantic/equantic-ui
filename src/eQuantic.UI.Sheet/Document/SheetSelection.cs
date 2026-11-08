namespace eQuantic.UI.Sheet;

/// <summary>
/// Where a selection stands: every range it holds, the active one last as <c>SheetController.Ranges</c> lists
/// them, and the cell edits land on. An undoable step records one from before it and one from after it, so a
/// step taken over several ranges gives every one of them back, with the active cell where the person left it,
/// as Excel's undo does (#1100).
/// </summary>
/// <param name="Ranges">Every range, the active one last.</param>
/// <param name="Active">The active cell, inside the active range.</param>
public readonly record struct SheetSelection(IReadOnlyList<SheetRange> Ranges, CellRef Active);
