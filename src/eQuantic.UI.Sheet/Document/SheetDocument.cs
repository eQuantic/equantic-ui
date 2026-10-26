namespace eQuantic.UI.Sheet;

/// <summary>
/// The sheet's DATA: a sparse map of cell values plus per-row/per-column sizes. Values are strings
/// — what the user typed is the truth a v1 spreadsheet keeps (a formula engine, number formats and
/// cell styling are explicitly LATER; the fence is stated, not implied). Mutable, owned by the
/// controller; every mutation goes through it so undo can capture the inverse.
/// </summary>
public sealed class SheetDocument
{
    private readonly Dictionary<long, string> _cells = new();
    private readonly Dictionary<int, float> _rowHeights = new();
    private readonly Dictionary<int, float> _colWidths = new();

    public SheetDocument(int rows = 1000, int cols = 26)
    {
        Rows = Math.Max(1, rows);
        Cols = Math.Max(1, Math.Min(cols, CellRef.MaxCols));
        RowSpans = new SheetSpans(this, SheetAxis.Rows);
        ColumnSpans = new SheetSpans(this, SheetAxis.Cols);
    }

    /// <summary>The rows as a table viewport reads them: where each starts and which one an offset
    /// falls in, a binary search over the rows sized differently.</summary>
    public SheetSpans RowSpans { get; }

    /// <summary>The columns, the same way.</summary>
    public SheetSpans ColumnSpans { get; }

    /// <summary>Counts every change of a size, or of the rows and columns that carry them, so the
    /// spans read the overrides again only when one happened.</summary>
    internal int SizeVersion { get; private set; }

    /// <summary>Counts every change of a cell's value and every insert or delete that moves values, so what is
    /// read from the values (the last cell in use, the status bar's sums) is read again only when one
    /// happened. A write that leaves the value as it was counts nothing.</summary>
    public int Version { get; private set; }

    internal IReadOnlyDictionary<int, float> RowHeights => _rowHeights;

    internal IReadOnlyDictionary<int, float> ColWidths => _colWidths;

    /// <summary>The grid's LOGICAL extent — what scrolling and headers show. Cells outside it hold
    /// nothing; growing it is free (the map is sparse).</summary>
    public int Rows { get; internal set; }
    public int Cols { get; internal set; }

    public const float DefaultRowHeight = 28f;
    public const float DefaultColWidth = 96f;

    /// <summary>Cells that actually HOLD something — iteration for save, copy, and bounds.</summary>
    public IReadOnlyDictionary<long, string> Cells => _cells;

    public string GetCell(CellRef cell) =>
        _cells.TryGetValue(cell.Key, out var value) ? value : "";

    /// <summary>
    /// Writes a cell DIRECTLY — the LOADING path (an app filling the sheet from its source), which
    /// is deliberately not undoable. A user's edit goes through <see cref="SheetController.SetCell"/>,
    /// which records the inverse. An empty value REMOVES the entry — sparse means empty is absent.
    /// </summary>
    public void SetCell(CellRef cell, string value)
    {
        if (GetCell(cell) == value) return;
        if (value.Length == 0) _cells.Remove(cell.Key);
        else _cells[cell.Key] = value;
        Version++;
    }

    public float RowHeight(int row) =>
        _rowHeights.TryGetValue(row, out var height) ? height : DefaultRowHeight;

    public float ColWidth(int col) =>
        _colWidths.TryGetValue(col, out var width) ? width : DefaultColWidth;

    /// <summary>
    /// Sizes a row DIRECTLY — the loading/PREVIEW path (a resize drag repaints every move through
    /// here), deliberately not undoable. The drag's release goes through
    /// <see cref="SheetController.Resize"/>, which records ONE inverse for the whole gesture.
    /// </summary>
    public void SetRowHeight(int row, float height)
    {
        if (Math.Abs(height - DefaultRowHeight) < 0.01f) _rowHeights.Remove(row);
        else _rowHeights[row] = Math.Max(12f, height);
        SizeVersion++;
    }

    /// <summary>Column twin of <see cref="SetRowHeight"/> — preview/load, not undoable.</summary>
    public void SetColWidth(int col, float width)
    {
        if (Math.Abs(width - DefaultColWidth) < 0.01f) _colWidths.Remove(col);
        else _colWidths[col] = Math.Max(24f, width);
        SizeVersion++;
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
    /// Shifts every populated cell and row size to make room for (or close over) inserted or
    /// deleted rows at <paramref name="atRow"/>: positive <paramref name="delta"/> inserts,
    /// negative deletes. The REMOVED band's cells are returned so the edit can restore them on undo.
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
        SizeVersion++;
        Version++;
        return removed;
    }

    /// <summary>
    /// The same along the other axis: columns at <paramref name="atCol"/>, positive
    /// <paramref name="delta"/> inserting and negative deleting, the removed band returned for undo.
    /// </summary>
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
        SizeVersion++;
        Version++;
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
