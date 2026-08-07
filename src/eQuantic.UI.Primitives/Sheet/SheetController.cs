namespace eQuantic.UI.Primitives;

/// <summary>How far one selection movement goes.</summary>
public enum SheetMotion : byte
{
    Cell = 0,
    /// <summary>Excel's Ctrl+arrow: to the edge of the data block in that direction.</summary>
    DataEdge = 1,
    /// <summary>Home/End on the row; Ctrl+Home/End to the sheet's corners.</summary>
    RowBoundary = 2,
    SheetBoundary = 3,
}

/// <summary>Which way a movement, insert or delete goes on the grid.</summary>
public enum SheetAxis : byte { Rows = 0, Cols = 1 }

/// <summary>
/// THE SPREADSHEET, minus the pixels — the code editor's shape in two dimensions. Selection and
/// navigation, cell edits, row/column structure, sizes, TSV in and out, undo/redo: every
/// manipulation Excel and Sheets offer over cells, rows and columns is a method here, on a
/// document, with no idea what any of it looks like. The v1 fence is equally explicit: values are
/// the strings the user typed — no formula engine, no per-cell formatting, no merges, no frozen
/// panes. Those are LATER, and nothing here has to be unlearned for them.
/// <para>
/// The app owns an instance; the surface (native and web realize the same one) renders from it
/// and routes input into it. Every mutation raises <see cref="Changed"/> with the edit.
/// </para>
/// </summary>
public sealed class SheetController
{
    private SheetRange _selection = new(new CellRef(0, 0));
    private CellRef _active = new(0, 0);

    public SheetController(int rows = 1000, int cols = 26)
    {
        Document = new SheetDocument(rows, cols);
    }

    public SheetDocument Document { get; }
    public SheetHistory History { get; } = new();

    /// <summary>Raised after any change to data or structure — never for selection moves.</summary>
    public Action<SheetEdit>? Changed { get; set; }

    /// <summary>The selected rectangle. Setting it puts the active cell at its focus end.</summary>
    public SheetRange Selection
    {
        get => _selection;
        set
        {
            _selection = new SheetRange(Document.Clamp(value.Anchor), Document.Clamp(value.Focus));
            _active = _selection.Focus;
        }
    }

    /// <summary>
    /// The cell edits land on. Usually the selection's focus — but Tab/Enter WALK it around inside
    /// a multi-cell selection while the rectangle stands still, exactly Excel's data-entry loop,
    /// which is why it is not merely an alias of the focus.
    /// </summary>
    public CellRef ActiveCell => _active;

    // ---- navigation -----------------------------------------------------------------------------

    /// <summary>
    /// Moves the focus (extend = false collapses the selection to the landing cell; true drags the
    /// focus and keeps the anchor — Shift+arrow). Motions mirror Excel: a CELL step, a DATA-EDGE
    /// jump (Ctrl+arrow: last populated cell of the run, or the next populated cell across a gap,
    /// or the sheet edge), row and sheet boundaries (Home/End, Ctrl+Home/End).
    /// </summary>
    public void Move(int dRow, int dCol, SheetMotion motion = SheetMotion.Cell, bool extend = false)
    {
        var from = ActiveCell;
        var landed = motion switch
        {
            SheetMotion.DataEdge => DataEdge(from, dRow, dCol),
            SheetMotion.RowBoundary => new CellRef(from.Row, dCol < 0 ? 0 : Document.Cols - 1),
            SheetMotion.SheetBoundary => dRow < 0 || dCol < 0 ? new CellRef(0, 0) : new CellRef(Document.Rows - 1, Document.Cols - 1),
            _ => new CellRef(from.Row + dRow, from.Col + dCol),
        };
        landed = Document.Clamp(landed);
        _selection = extend ? new SheetRange(_selection.Anchor, landed) : new SheetRange(landed);
        _active = landed;
    }

    private CellRef DataEdge(CellRef from, int dRow, int dCol)
    {
        var cell = from;
        var next = new CellRef(cell.Row + dRow, cell.Col + dCol);
        if (!InBounds(next)) return cell;

        // Standing on data with data ahead: run to the last populated cell of the block.
        // Standing on empty (or at the block's edge): jump across the gap to the next populated
        // cell, or the sheet edge when there is none. Exactly Excel's Ctrl+arrow.
        if (Document.HasValue(cell) && Document.HasValue(next))
        {
            while (InBounds(next) && Document.HasValue(next))
            {
                cell = next;
                next = new CellRef(cell.Row + dRow, cell.Col + dCol);
            }
            return cell;
        }

        cell = next;
        while (InBounds(cell) && !Document.HasValue(cell))
            cell = new CellRef(cell.Row + dRow, cell.Col + dCol);
        return InBounds(cell) ? cell : Edge(from, dRow, dCol);
    }

    private CellRef Edge(CellRef from, int dRow, int dCol) => new(
        dRow == 0 ? from.Row : dRow < 0 ? 0 : Document.Rows - 1,
        dCol == 0 ? from.Col : dCol < 0 ? 0 : Document.Cols - 1);

    private bool InBounds(CellRef cell) =>
        cell.Row >= 0 && cell.Row < Document.Rows && cell.Col >= 0 && cell.Col < Document.Cols;

    /// <summary>
    /// Tab/Enter stepping: within a MULTI-cell selection the focus walks the rectangle and wraps
    /// (Excel's data-entry loop); on a single cell it just moves — Enter down, Tab right.
    /// </summary>
    public void Step(int dRow, int dCol)
    {
        if (_selection.IsSingleCell)
        {
            Move(dRow, dCol);
            return;
        }
        var range = _selection;
        var row = _active.Row;
        var col = _active.Col;
        if (dCol != 0)
        {
            col += dCol;
            if (col > range.RightCol) { col = range.LeftCol; row = row == range.BottomRow ? range.TopRow : row + 1; }
            else if (col < range.LeftCol) { col = range.RightCol; row = row == range.TopRow ? range.BottomRow : row - 1; }
        }
        else
        {
            row += dRow;
            if (row > range.BottomRow) { row = range.TopRow; col = col == range.RightCol ? range.LeftCol : col + 1; }
            else if (row < range.TopRow) { row = range.BottomRow; col = col == range.LeftCol ? range.RightCol : col - 1; }
        }
        // The RECTANGLE stays; only the active cell walked inside it.
        _active = new CellRef(row, col);
    }

    /// <summary>Select the whole row band / column band of the current selection (header clicks).</summary>
    public void SelectRows(int fromRow, int toRow)
    {
        _selection = new SheetRange(new CellRef(fromRow, 0), new CellRef(toRow, Document.Cols - 1));
        _active = new CellRef(fromRow, 0);
    }

    public void SelectCols(int fromCol, int toCol)
    {
        _selection = new SheetRange(new CellRef(0, fromCol), new CellRef(Document.Rows - 1, toCol));
        _active = new CellRef(0, fromCol);
    }

    public void SelectAll()
    {
        _selection = new SheetRange(new CellRef(0, 0), new CellRef(Document.Rows - 1, Document.Cols - 1));
        _active = new CellRef(0, 0);
    }

    // ---- in-cell editing -------------------------------------------------------------------------
    //
    // The DRAFT lives here, not in a surface: both targets edit the same way, and the document
    // only changes on COMMIT — Escape restores exactly what was there, like every spreadsheet.

    /// <summary>Whether the active cell is being edited (the draft renders in its place).</summary>
    public bool Editing { get; private set; }

    /// <summary>The text being typed — the cell's value-to-be, until commit or cancel.</summary>
    public string Draft { get; private set; } = "";

    /// <summary>
    /// Starts editing the ACTIVE cell. Typing starts with the typed character (Excel: typing
    /// REPLACES); F2 and double-click start with the cell's current value (editing in place).
    /// </summary>
    public void BeginEdit(string seed)
    {
        Editing = true;
        Draft = seed;
    }

    /// <summary>Appends typed text to the draft (the platform composes; this just receives).</summary>
    public void TypeIntoDraft(string text) => Draft += text;

    /// <summary>Removes the draft's last character — Backspace while editing.</summary>
    public void EraseFromDraft()
    {
        if (Draft.Length > 0) Draft = Draft[..^1];
    }

    /// <summary>The draft becomes the cell's value (one undo step). Answers whether it changed.</summary>
    public bool CommitEdit()
    {
        if (!Editing) return false;
        var changed = SetCell(ActiveCell, Draft);
        Editing = false;
        Draft = "";
        return changed;
    }

    /// <summary>Excel's Escape: the draft is discarded and the cell keeps what it had.</summary>
    public void CancelEdit()
    {
        Editing = false;
        Draft = "";
    }

    // ---- cell edits ------------------------------------------------------------------------------

    /// <summary>One cell gets a value — the in-cell editor's commit. No-op if nothing changes.</summary>
    public bool SetCell(CellRef cell, string value)
    {
        cell = Document.Clamp(cell);
        var old = Document.GetCell(cell);
        if (old == value) return false;
        var edit = new SheetEdit
        {
            Kind = SheetEditKind.SetCells,
            Before = new List<SheetCellSnapshot> { new(cell, old) },
            After = new List<SheetCellSnapshot> { new(cell, value) },
            SelectionBefore = _selection,
            SelectionAfter = _selection,
        };
        Document.SetCell(cell, value);
        Commit(edit);
        return true;
    }

    /// <summary>Delete over a selection: every populated cell in the rectangle empties.</summary>
    public bool ClearSelection()
    {
        var edit = new SheetEdit
        {
            Kind = SheetEditKind.SetCells,
            SelectionBefore = _selection,
            SelectionAfter = _selection,
        };
        for (var row = _selection.TopRow; row <= _selection.BottomRow; row++)
        {
            for (var col = _selection.LeftCol; col <= _selection.RightCol; col++)
            {
                var cell = new CellRef(row, col);
                var old = Document.GetCell(cell);
                if (old.Length == 0) continue;
                edit.Before.Add(new SheetCellSnapshot(cell, old));
                edit.After.Add(new SheetCellSnapshot(cell, ""));
                Document.SetCell(cell, "");
            }
        }
        if (edit.Before.Count == 0) return false;
        Commit(edit);
        return true;
    }

    // ---- structure -------------------------------------------------------------------------------

    public void Insert(SheetAxis axis, int at, int count = 1)
    {
        if (count < 1) return;
        var selectionBefore = _selection;
        if (axis == SheetAxis.Rows) Document.ShiftRows(at, count);
        else Document.ShiftCols(at, count);
        Commit(new SheetEdit
        {
            Kind = axis == SheetAxis.Rows ? SheetEditKind.InsertRows : SheetEditKind.InsertCols,
            At = at,
            Count = count,
            SelectionBefore = selectionBefore,
            SelectionAfter = _selection,
        });
    }

    public void Delete(SheetAxis axis, int at, int count = 1)
    {
        if (count < 1) return;
        var selectionBefore = _selection;
        var removed = axis == SheetAxis.Rows ? Document.ShiftRows(at, -count) : Document.ShiftCols(at, -count);
        Selection = _selection;   // re-clamp into the smaller grid
        Commit(new SheetEdit
        {
            Kind = axis == SheetAxis.Rows ? SheetEditKind.DeleteRows : SheetEditKind.DeleteCols,
            At = at,
            Count = count,
            Removed = removed,
            SelectionBefore = selectionBefore,
            SelectionAfter = _selection,
        });
    }

    public void Resize(SheetAxis axis, int index, float size)
    {
        var old = axis == SheetAxis.Rows ? Document.RowHeight(index) : Document.ColWidth(index);
        if (Math.Abs(old - size) < 0.01f) return;
        if (axis == SheetAxis.Rows) Document.SetRowHeight(index, size);
        else Document.SetColWidth(index, size);
        Commit(new SheetEdit
        {
            Kind = axis == SheetAxis.Rows ? SheetEditKind.ResizeRow : SheetEditKind.ResizeCol,
            At = index,
            OldSize = old,
            NewSize = axis == SheetAxis.Rows ? Document.RowHeight(index) : Document.ColWidth(index),
            SelectionBefore = _selection,
            SelectionAfter = _selection,
        });
    }

    // ---- clipboard -------------------------------------------------------------------------------

    /// <summary>The selection as TSV — hand it to the platform clipboard and Excel pastes it.</summary>
    public string CopyTsv() => TsvCodec.Serialize(Document, _selection);

    /// <summary>
    /// Pastes a TSV grid with its top-left at the selection's top-left (growing the sheet if it
    /// must), selects the pasted rectangle, and records ONE undo step.
    /// </summary>
    public bool PasteTsv(string text)
    {
        var grid = TsvCodec.Parse(text);
        if (grid.Count == 0) return false;
        var origin = _selection.TopLeft;

        var maxCols = 0;
        foreach (var line in grid) maxCols = Math.Max(maxCols, line.Count);
        if (origin.Row + grid.Count > Document.Rows) Document.Rows = origin.Row + grid.Count;
        if (origin.Col + maxCols > Document.Cols) Document.Cols = Math.Min(origin.Col + maxCols, CellRef.MaxCols);

        var edit = new SheetEdit
        {
            Kind = SheetEditKind.SetCells,
            SelectionBefore = _selection,
        };
        for (var r = 0; r < grid.Count; r++)
        {
            var line = grid[r];
            for (var c = 0; c < line.Count; c++)
            {
                var cell = new CellRef(origin.Row + r, origin.Col + c);
                if (!InBounds(cell)) continue;
                var old = Document.GetCell(cell);
                if (old == line[c]) continue;
                edit.Before.Add(new SheetCellSnapshot(cell, old));
                edit.After.Add(new SheetCellSnapshot(cell, line[c]));
                Document.SetCell(cell, line[c]);
            }
        }
        _selection = new SheetRange(origin,
            Document.Clamp(new CellRef(origin.Row + grid.Count - 1, origin.Col + maxCols - 1)));
        if (edit.Before.Count == 0) return false;
        edit.SelectionAfter = _selection;
        Commit(edit);
        return true;
    }

    // ---- undo/redo -------------------------------------------------------------------------------

    public bool Undo()
    {
        var edit = History.PopUndo();
        if (edit is null) return false;
        Apply(edit, forward: false);
        _selection = edit.SelectionBefore;
        _active = _selection.Focus;
        Changed?.Invoke(edit);
        return true;
    }

    public bool Redo()
    {
        var edit = History.PopRedo();
        if (edit is null) return false;
        Apply(edit, forward: true);
        _selection = edit.SelectionAfter;
        _active = _selection.Focus;
        Changed?.Invoke(edit);
        return true;
    }

    private void Apply(SheetEdit edit, bool forward)
    {
        switch (edit.Kind)
        {
            case SheetEditKind.SetCells:
                var cells = forward ? edit.After : edit.Before;
                foreach (var snapshot in cells) Document.SetCell(snapshot.Cell, snapshot.Value);
                break;
            case SheetEditKind.InsertRows:
                if (forward) Document.ShiftRows(edit.At, edit.Count);
                else Document.ShiftRows(edit.At, -edit.Count);
                break;
            case SheetEditKind.DeleteRows:
                if (forward) Document.ShiftRows(edit.At, -edit.Count);
                else
                {
                    Document.ShiftRows(edit.At, edit.Count);
                    foreach (var snapshot in edit.Removed) Document.SetCell(snapshot.Cell, snapshot.Value);
                }
                break;
            case SheetEditKind.InsertCols:
                if (forward) Document.ShiftCols(edit.At, edit.Count);
                else Document.ShiftCols(edit.At, -edit.Count);
                break;
            case SheetEditKind.DeleteCols:
                if (forward) Document.ShiftCols(edit.At, -edit.Count);
                else
                {
                    Document.ShiftCols(edit.At, edit.Count);
                    foreach (var snapshot in edit.Removed) Document.SetCell(snapshot.Cell, snapshot.Value);
                }
                break;
            case SheetEditKind.ResizeRow:
                Document.SetRowHeight(edit.At, forward ? edit.NewSize : edit.OldSize);
                break;
            case SheetEditKind.ResizeCol:
                Document.SetColWidth(edit.At, forward ? edit.NewSize : edit.OldSize);
                break;
        }
    }

    private void Commit(SheetEdit edit)
    {
        History.Push(edit);
        Changed?.Invoke(edit);
    }
}
