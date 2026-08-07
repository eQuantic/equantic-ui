namespace eQuantic.UI.Primitives;

/// <summary>What kind of change a <see cref="SheetEdit"/> records.</summary>
public enum SheetEditKind : byte
{
    SetCells = 0,
    InsertRows = 1,
    DeleteRows = 2,
    InsertCols = 3,
    DeleteCols = 4,
    ResizeRow = 5,
    ResizeCol = 6,
}

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

/// <summary>
/// Undo/redo for the sheet — the code editor's shape: two stacks, redo cleared by any new edit.
/// Edits are atomic (a cell commit, a structural change), so there is no typing coalescing here;
/// the in-cell editor owns its own keystrokes and commits ONE edit.
/// </summary>
public sealed class SheetHistory
{
    private readonly List<SheetEdit> _undo = new();
    private readonly List<SheetEdit> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Push(SheetEdit edit)
    {
        _undo.Add(edit);
        _redo.Clear();
    }

    public SheetEdit? PopUndo()
    {
        if (_undo.Count == 0) return null;
        var edit = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(edit);
        return edit;
    }

    public SheetEdit? PopRedo()
    {
        if (_redo.Count == 0) return null;
        var edit = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(edit);
        return edit;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}

/// <summary>
/// The clipboard dialect every spreadsheet already speaks: tab-separated cells, newline-separated
/// rows, Excel's quoting for cells that CONTAIN tabs, newlines or quotes. This is what makes
/// copy/paste between this component and Excel/Google Sheets just work.
/// </summary>
public static class TsvCodec
{
    public static string Serialize(SheetDocument document, SheetRange range)
    {
        var rows = new List<string>();
        for (var row = range.TopRow; row <= range.BottomRow; row++)
        {
            var cells = new List<string>();
            for (var col = range.LeftCol; col <= range.RightCol; col++)
                cells.Add(Escape(document.GetCell(new CellRef(row, col))));
            rows.Add(string.Join("\t", cells));
        }
        return string.Join("\n", rows);
    }

    private static string Escape(string value)
    {
        if (value.IndexOf('\t') < 0 && value.IndexOf('\n') < 0 && value.IndexOf('"') < 0)
            return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>Rows of cells. A single unquoted trailing newline (every real clipboard has one)
    /// is not a row.</summary>
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = "";
        var quoted = false;
        var i = 0;
        while (i < text.Length)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell += '"'; i += 2; continue; }
                    quoted = false;
                    i++;
                    continue;
                }
                cell += ch;
                i++;
                continue;
            }
            if (ch == '"' && cell.Length == 0) { quoted = true; i++; continue; }
            if (ch == '\t') { row.Add(cell); cell = ""; i++; continue; }
            if (ch == '\r') { i++; continue; }
            if (ch == '\n')
            {
                row.Add(cell);
                rows.Add(row);
                row = new List<string>();
                cell = "";
                i++;
                continue;
            }
            cell += ch;
            i++;
        }
        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell);
            rows.Add(row);
        }
        return rows;
    }
}
