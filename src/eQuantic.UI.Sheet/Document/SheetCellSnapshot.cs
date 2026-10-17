namespace eQuantic.UI.Sheet;

/// <summary>One cell's address and value, captured for undo.</summary>
public readonly record struct SheetCellSnapshot(CellRef Cell, string Value);
