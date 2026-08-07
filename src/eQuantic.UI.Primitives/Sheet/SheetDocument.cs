namespace eQuantic.UI.Primitives;

/// <summary>
/// The sheet's DATA: a sparse map of cell values plus per-row/per-column sizes. Values are strings
/// — what the user typed is the truth a v1 spreadsheet keeps (a formula engine, number formats and
/// cell styling are explicitly LATER; the fence is stated, not implied). Mutable, owned by the
/// controller; every mutation goes through it so undo can capture the inverse.
/// </summary>
public sealed class SheetDocument
{
    private readonly Dictionary<int, string> _cells = new();
    private readonly Dictionary<int, float> _rowHeights = new();
    private readonly Dictionary<int, float> _colWidths = new();

    public SheetDocument(int rows = 1000, int cols = 26)
    {
        Rows = Math.Max(1, rows);
        Cols = Math.Max(1, Math.Min(cols, CellRef.MaxCols));
    }

    /// <summary>The grid's LOGICAL extent — what scrolling and headers show. Cells outside it hold
    /// nothing; growing it is free (the map is sparse).</summary>
    public int Rows { get; internal set; }
    public int Cols { get; internal set; }

    public const float DefaultRowHeight = 28f;
    public const float DefaultColWidth = 96f;

    /// <summary>Cells that actually HOLD something — iteration for save, copy, and bounds.</summary>
    public IReadOnlyDictionary<int, string> Cells => _cells;

    public string GetCell(CellRef cell) =>
        _cells.TryGetValue(cell.Key, out var value) ? value : "";

    /// <summary>
    /// Writes a cell DIRECTLY — the LOADING path (an app filling the sheet from its source), which
    /// is deliberately not undoable. A user's edit goes through <see cref="SheetController.SetCell"/>,
    /// which records the inverse. An empty value REMOVES the entry — sparse means empty is absent.
    /// </summary>
    public void SetCell(CellRef cell, string value)
    {
        if (value.Length == 0) _cells.Remove(cell.Key);
        else _cells[cell.Key] = value;
    }

    public float RowHeight(int row) =>
        _rowHeights.TryGetValue(row, out var height) ? height : DefaultRowHeight;

    public float ColWidth(int col) =>
        _colWidths.TryGetValue(col, out var width) ? width : DefaultColWidth;

    internal void SetRowHeight(int row, float height)
    {
        if (Math.Abs(height - DefaultRowHeight) < 0.01f) _rowHeights.Remove(row);
        else _rowHeights[row] = Math.Max(12f, height);
    }

    internal void SetColWidth(int col, float width)
    {
        if (Math.Abs(width - DefaultColWidth) < 0.01f) _colWidths.Remove(col);
        else _colWidths[col] = Math.Max(24f, width);
    }

    public CellRef Clamp(CellRef cell) => new(
        Math.Clamp(cell.Row, 0, Rows - 1),
        Math.Clamp(cell.Col, 0, Cols - 1));

    /// <summary>
    /// Whether the cell holds a value — the building block of Excel's Ctrl+arrow "jump to the edge
    /// of the data block" motion.
    /// </summary>
    public bool HasValue(CellRef cell) => _cells.ContainsKey(cell.Key);

    /// <summary>
    /// Shifts every populated cell, row size and column size to make room for (or close over)
    /// inserted/deleted rows or columns. <paramref name="atRow"/>/<paramref name="atCol"/> pick the
    /// axis; positive <paramref name="delta"/> inserts, negative deletes. The REMOVED band's cells
    /// are returned so the edit can restore them on undo.
    /// </summary>
    internal List<SheetCellSnapshot> ShiftRows(int atRow, int delta)
    {
        var removed = new List<SheetCellSnapshot>();
        var moved = new List<SheetCellSnapshot>();
        foreach (var (key, value) in _cells)
        {
            var cell = CellRef.FromKey(key);
            if (cell.Row < atRow) continue;
            if (delta < 0 && cell.Row < atRow - delta)
            {
                removed.Add(new SheetCellSnapshot(cell, value));
                continue;
            }
            moved.Add(new SheetCellSnapshot(cell, value));
        }
        foreach (var snapshot in removed) _cells.Remove(snapshot.Cell.Key);
        foreach (var snapshot in moved) _cells.Remove(snapshot.Cell.Key);
        foreach (var snapshot in moved)
            _cells[new CellRef(snapshot.Cell.Row + delta, snapshot.Cell.Col).Key] = snapshot.Value;

        ShiftSizes(_rowHeights, atRow, delta);
        Rows = Math.Max(1, Rows + delta);
        return removed;
    }

    internal List<SheetCellSnapshot> ShiftCols(int atCol, int delta)
    {
        var removed = new List<SheetCellSnapshot>();
        var moved = new List<SheetCellSnapshot>();
        foreach (var (key, value) in _cells)
        {
            var cell = CellRef.FromKey(key);
            if (cell.Col < atCol) continue;
            if (delta < 0 && cell.Col < atCol - delta)
            {
                removed.Add(new SheetCellSnapshot(cell, value));
                continue;
            }
            moved.Add(new SheetCellSnapshot(cell, value));
        }
        foreach (var snapshot in removed) _cells.Remove(snapshot.Cell.Key);
        foreach (var snapshot in moved) _cells.Remove(snapshot.Cell.Key);
        foreach (var snapshot in moved)
            _cells[new CellRef(snapshot.Cell.Row, snapshot.Cell.Col + delta).Key] = snapshot.Value;

        ShiftSizes(_colWidths, atCol, delta);
        Cols = Math.Max(1, Math.Min(Cols + delta, CellRef.MaxCols));
        return removed;
    }

    private static void ShiftSizes(Dictionary<int, float> sizes, int at, int delta)
    {
        if (sizes.Count == 0) return;
        var entries = new List<KeyValuePair<int, float>>(sizes);
        foreach (var entry in entries)
        {
            if (entry.Key < at) continue;
            sizes.Remove(entry.Key);
        }
        foreach (var entry in entries)
        {
            if (entry.Key < at) continue;
            if (delta < 0 && entry.Key < at - delta) continue;   // the deleted band's sizes go with it
            sizes[entry.Key + delta] = entry.Value;
        }
    }
}

/// <summary>One cell's address and value, captured for undo.</summary>
public readonly record struct SheetCellSnapshot(CellRef Cell, string Value);
