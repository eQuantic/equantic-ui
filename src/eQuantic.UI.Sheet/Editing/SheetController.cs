using System.Globalization;
using eQuantic.UI.Code;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Sheet;

/// <summary>
/// THE SPREADSHEET, minus the pixels — the code editor's shape in two dimensions. Selection and
/// navigation, cell edits, row/column structure, sizes, TSV in and out, undo/redo: every
/// manipulation Excel and Sheets offer over cells, rows and columns is a method here, on a
/// document, with no idea what any of it looks like. The v1 fence is equally explicit: values are
/// the strings the user typed — no formula engine, no per-cell formatting, no merges, no frozen
/// panes. Those are LATER, and nothing here has to be unlearned for them.
/// <para>
/// The app owns an instance, and the component that draws it renders from it. It is also the
/// <see cref="ISheetSurfaceModel"/> a realizer drives: the host hands it keys, text, the clipboard's
/// events and the pointer, and every decision about what they mean is made here, once, for both
/// targets. Every mutation raises <see cref="Changed"/> with the edit.
/// </para>
/// </summary>
public sealed class SheetController : ISheetSurfaceModel
{
    private SheetRange _selection = new(new CellRef(0, 0));
    private CellRef _active = new(0, 0);

    public SheetController(int rows = 1000, int cols = 26)
    {
        Document = new SheetDocument(rows, cols);
        Entry = new CodeEditorController("", new SheetEntryLanguage());
        FormulaBar = new SheetFormulaBar(this);
        NameBox = new SheetNameBox(this);
    }

    /// <summary>The formula bar's model: the entry's second view, which the formula bar component draws.</summary>
    public SheetFormulaBar FormulaBar { get; }

    /// <summary>The name box's model: the address of the selection, and Go To.</summary>
    public SheetNameBox NameBox { get; }

    public SheetDocument Document { get; }
    public SheetHistory History { get; } = new();

    /// <summary>Raised after any change to data or structure — never for selection moves.</summary>
    public Action<SheetEdit>? Changed { get; set; }

    /// <summary>
    /// Raised when the views of this sheet should build again: an input changed what the grid, the formula
    /// bar or the name box shows. The views are separate components the app places, and a keystroke in one
    /// changes what the others show, so every one of them listens, as a Flutter widget listens to its
    /// controller's <c>ChangeNotifier</c>.
    /// </summary>
    public event Action? Refreshed;

    /// <summary>Tells every view of this sheet to build again: what a view calls after an input it handled,
    /// and what an app calls after it changed the sheet itself.</summary>
    public void Refresh() => Refreshed?.Invoke();

    /// <summary>The selected rectangle. Setting it puts the active cell at its focus end, and asks the
    /// grid to bring that cell into view (<see cref="RevealVersion"/>), as a command that moves it does.</summary>
    public SheetRange Selection
    {
        get => _selection;
        set
        {
            Select(value);
            Reveal();
        }
    }

    /// <summary>The selection, clamped into the grid, with the active cell at its focus end and nothing
    /// asked of the grid: a press and a drag land on cells the person can see.</summary>
    private void Select(SheetRange range)
    {
        _selection = new SheetRange(Document.Clamp(range.Anchor), Document.Clamp(range.Focus));
        _active = _selection.Focus;
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
        Reveal();
    }

    /// <summary>
    /// PageDown and PageUp: the active cell by the rows the grid shows under its headers, the selection
    /// with it (extend keeps the anchor, Shift's way). Without a viewport to measure there is no page, and
    /// the key is not taken.
    /// </summary>
    public bool MovePage(int direction, bool extend = false)
    {
        if (Grid.Scroll is not { } scroll) return false;
        var page = Math.Max(Document.RowSpans.ExtentOf(_active.Row), scroll.Viewport.Height - Grid.HeaderHeight);
        var from = Document.RowSpans.OffsetOf(_active.Row);
        var row = Document.RowSpans.IndexAt(from + Math.Sign(direction) * page);
        var landed = Document.Clamp(new CellRef(row, _active.Col));
        _selection = extend ? new SheetRange(_selection.Anchor, landed) : new SheetRange(landed);
        _active = landed;
        Reveal();
        return true;
    }

    /// <summary>
    /// Counts every command that moved the active cell from the keyboard, a paste, a fill, an undo or a
    /// redo, never a press (the cell is under the pointer): the composing component hands it to its
    /// viewport as a request's version, revealing the active cell once per count.
    /// </summary>
    public int RevealVersion { get; private set; }

    /// <summary>
    /// The cell <see cref="RevealVersion"/> asks to have in view: the active cell as the command that
    /// raised the version left it. A viewport holds a request until the person moves the grid, so a
    /// request built from the active cell as it stands would chase every cell a later drag reaches,
    /// scrolling the grid under the pointer frame after frame.
    /// </summary>
    public CellRef RevealCell { get; private set; }

    private void Reveal()
    {
        RevealVersion++;
        RevealCell = _active;
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
        Reveal();
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

    /// <summary>Shift+click: the selection stretches from its anchor to the clicked cell. The
    /// active cell stays put — exactly what Excel leaves standing.</summary>
    public void SelectTo(CellRef cell) =>
        _selection = new SheetRange(_selection.Anchor, Document.Clamp(cell));

    // ---- in-cell editing -------------------------------------------------------------------------
    //
    // The ENTRY is a text document, the code engine's: a caret, a selection, word moves, a composition and
    // an undo of its own. It lives here, not in a surface: both targets edit the same way, and the cell's
    // editor and the formula bar are two views of it (EntryView says which one holds the caret), so they
    // cannot disagree. The sheet's document changes only on COMMIT: Escape leaves the cell as it was, like
    // every spreadsheet.

    /// <summary>Whether the active cell is being edited (its editor is drawn over it).</summary>
    public bool Editing { get; private set; }

    /// <summary>
    /// The entry being typed, the cell's value-to-be until it is committed or cancelled: the code engine's
    /// editor over one cell's text, in a language that pairs nothing and indents nothing
    /// (<see cref="SheetEntryLanguage"/>). Its text is <c>Entry.Document.Text</c>; between entries it is
    /// empty.
    /// </summary>
    public CodeEditorController Entry { get; }

    /// <summary>What the arrows do while editing: commit and move, or move the caret.</summary>
    public SheetEditMode EditMode { get; private set; }

    /// <summary>Which view of the entry holds its caret: the cell's editor or the formula bar.</summary>
    public SheetEntryView EntryView { get; private set; }

    /// <summary>
    /// Starts editing the ACTIVE cell with <paramref name="seed"/>, the caret at its end. Typing starts with
    /// nothing in <see cref="SheetEditMode.Enter"/> mode (Excel: typing REPLACES); F2 and a double click
    /// start with the cell's value in <see cref="SheetEditMode.Edit"/> mode. The entry's own undo starts
    /// empty.
    /// </summary>
    public void BeginEdit(string seed, SheetEditMode mode)
    {
        Editing = true;
        EditMode = mode;
        EntryView = SheetEntryView.Cell;
        _slide = 0;
        _rise = 0;
        Entry.Document = CodeDocument.FromText(seed);
        Entry.Selection = new CodeRange(Entry.Document.End);
    }

    /// <summary>A press in the cell's editor or the formula bar: the arrows move the caret from now on.</summary>
    internal void EnterEditMode()
    {
        if (Editing) EditMode = SheetEditMode.Edit;
    }

    /// <summary>Makes <paramref name="view"/> the one that holds the entry's caret, its geometry the entry's.</summary>
    internal void HoldEntryIn(SheetEntryView view)
    {
        EntryView = view;
        UseView();
    }

    /// <summary>Puts the entry on the geometry of the view that holds its caret, the one the next geometric
    /// answer is read on: the cell's editor's, or the formula bar's.</summary>
    internal void UseView()
    {
        if (EntryView == SheetEntryView.FormulaBar) Entry.Grid = FormulaBar.Grid;
        else _ = CellEditorGrid;
    }

    /// <summary>
    /// Excel's Go To: <paramref name="reference"/>, a cell, a range, whole rows or whole columns
    /// (<see cref="SheetReference"/>), becomes the selection with its first cell active and in view, and the
    /// grid takes the keyboard. Answers false, changing nothing, for text that names nothing in the grid.
    /// </summary>
    public bool GoTo(string reference)
    {
        if (!SheetReference.TryParse(reference, Document.Rows, Document.Cols, out var range)) return false;
        if (Editing) CommitEdit();
        _selection = range;
        _active = range.TopLeft;
        Reveal();
        RequestFocus();
        return true;
    }

    /// <summary>Whether a drag from a plain press is drawing a range of more than one cell: what the name box
    /// shows the size of while it lasts.</summary>
    public bool Selecting => _selecting && !_selection.IsSingleCell;

    /// <summary>F2 while editing: Enter mode becomes Edit mode, and back.</summary>
    public void ToggleEditMode()
    {
        if (Editing) EditMode = EditMode == SheetEditMode.Enter ? SheetEditMode.Edit : SheetEditMode.Enter;
    }

    /// <summary>The entry becomes the cell's value (one undo step). Answers whether it changed.</summary>
    public bool CommitEdit()
    {
        if (!Editing) return false;
        var changed = SetCell(ActiveCell, Entry.Document.Text);
        EndEdit();
        return changed;
    }

    /// <summary>Excel's Escape: the entry is discarded and the cell keeps what it had.</summary>
    public void CancelEdit()
    {
        if (Editing) EndEdit();
    }

    /// <summary>
    /// Excel's Ctrl+Enter: the entry is written into EVERY cell of the selection, as one undo step, and the
    /// selection stays where it was. Answers whether a cell changed.
    /// </summary>
    public bool FillSelectionWithEntry()
    {
        if (!Editing) return false;
        var value = Entry.Document.Text;
        EndEdit();
        return FillSelection(value);
    }

    private void EndEdit()
    {
        Editing = false;
        EntryView = SheetEntryView.Cell;
        Entry.Document = CodeDocument.FromText("");
    }

    /// <summary>The clock the date and time chords read. A test names its own moment.</summary>
    public Func<DateTime> Now { get; set; } = () => DateTime.Now;

    /// <summary>
    /// Today's date, or the time, written by the culture's short date or short time pattern: what Excel's
    /// date and time chords type. At rest the text begins an entry in Enter mode; while editing it goes in
    /// at the caret.
    /// </summary>
    public void TypeNow(bool time)
    {
        var text = Now().ToString(time ? "t" : "d", CultureInfo.CurrentCulture);
        if (!Editing) BeginEdit(text, SheetEditMode.Enter);
        else Entry.Insert(text);
    }

    /// <summary>Alt+Enter: a line break inside the entry.</summary>
    public void BreakLine()
    {
        if (Editing) Entry.Insert("\n");
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

    /// <summary>Every cell of the selection gets <paramref name="value"/>, as one undo step.</summary>
    public bool FillSelection(string value)
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
                if (old == value) continue;
                edit.Before.Add(new SheetCellSnapshot(cell, old));
                edit.After.Add(new SheetCellSnapshot(cell, value));
                Document.SetCell(cell, value);
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
        Select(_selection);   // re-clamp into the smaller grid
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

    /// <summary>The selection as TSV — hand it to the platform clipboard and Excel pastes it. While a cell
    /// is being edited, the entry's selected text, and nothing when nothing is selected in it.</summary>
    public string CopyText() =>
        Editing ? Entry.Document.TextIn(Entry.Selection) : TsvCodec.Serialize(Document, _selection);

    /// <summary>A copy that also empties what it took, as one undo step. Answers the text for the
    /// clipboard.</summary>
    public string Cut()
    {
        if (Editing) return Entry.Selection.IsEmpty ? "" : Entry.Cut();
        var text = CopyText();
        ClearSelection();
        return text;
    }

    /// <summary>
    /// Pastes a TSV grid with its top-left at the selection's top-left (growing the sheet if it
    /// must), selects the pasted rectangle, and records ONE undo step.
    /// </summary>
    public bool Paste(string text)
    {
        // While editing, the clipboard's text is TEXT: it goes in at the caret, tabs and line breaks as
        // they are, as one step of the entry's undo.
        if (Editing) return Entry.Paste(text);
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
        Reveal();
        if (edit.Before.Count == 0) return false;
        edit.SelectionAfter = _selection;
        Commit(edit);
        return true;
    }

    // ---- the fill handle (Excel's little square) ---------------------------------------------

    /// <summary>The fill drag in flight: the SOURCE block the gesture started from, or null.</summary>
    public SheetRange? FillSource { get; private set; }

    /// <summary>Where a release would pour values — the painted preview. Never overlaps the
    /// source, and always a straight extension of it (Excel picks ONE axis per drag).</summary>
    public SheetRange? FillTarget { get; private set; }

    public bool Filling => FillSource is not null;

    /// <summary>The drag grabbed the little square: the current selection becomes the source.</summary>
    public void BeginFill()
    {
        FillSource = _selection;
        FillTarget = null;
    }

    /// <summary>
    /// The pointer reached a cell: pick the DOMINANT axis out from the source — Excel's rule — and
    /// preview the stretch a release would fill. Back inside the source, the preview clears.
    /// </summary>
    public void UpdateFill(CellRef at)
    {
        if (FillSource is not { } source) return;
        at = Document.Clamp(at);
        var dRow = at.Row < source.TopRow ? at.Row - source.TopRow
            : at.Row > source.BottomRow ? at.Row - source.BottomRow : 0;
        var dCol = at.Col < source.LeftCol ? at.Col - source.LeftCol
            : at.Col > source.RightCol ? at.Col - source.RightCol : 0;
        if (dRow == 0 && dCol == 0)
        {
            FillTarget = null;
            return;
        }
        FillTarget = Math.Abs(dRow) >= Math.Abs(dCol)
            ? dRow > 0
                ? new SheetRange(new CellRef(source.BottomRow + 1, source.LeftCol),
                    new CellRef(at.Row, source.RightCol))
                : new SheetRange(new CellRef(at.Row, source.LeftCol),
                    new CellRef(source.TopRow - 1, source.RightCol))
            : dCol > 0
                ? new SheetRange(new CellRef(source.TopRow, source.RightCol + 1),
                    new CellRef(source.BottomRow, at.Col))
                : new SheetRange(new CellRef(source.TopRow, at.Col),
                    new CellRef(source.BottomRow, source.LeftCol - 1));
    }

    /// <summary>
    /// Release: the source tiles across the target (one undo step) and the selection grows to
    /// cover both — exactly what Excel leaves selected after the handle drops.
    /// </summary>
    public bool CommitFill()
    {
        var source = FillSource;
        var target = FillTarget;
        FillSource = null;
        FillTarget = null;
        if (source is null || target is null) return false;
        var selectionBefore = _selection;
        _selection = new SheetRange(
            new CellRef(Math.Min(source.Value.TopRow, target.Value.TopRow),
                Math.Min(source.Value.LeftCol, target.Value.LeftCol)),
            new CellRef(Math.Max(source.Value.BottomRow, target.Value.BottomRow),
                Math.Max(source.Value.RightCol, target.Value.RightCol)));
        _active = source.Value.TopLeft;
        return Fill(source.Value, target.Value, selectionBefore);
    }

    /// <summary>The drag left the surface without releasing over a target.</summary>
    public void CancelFill()
    {
        FillSource = null;
        FillTarget = null;
    }

    /// <summary>
    /// The fill ENGINE the handle and ⌘D/⌘R share: the source block tiles across the target as one
    /// undo step. Wrap-around indexing keeps the pattern in phase in all four directions.
    /// </summary>
    public bool Fill(SheetRange source, SheetRange target, SheetRange? selectionBefore = null)
    {
        var edit = new SheetEdit
        {
            Kind = SheetEditKind.SetCells,
            SelectionBefore = selectionBefore ?? _selection,
        };
        var sourceRows = source.BottomRow - source.TopRow + 1;
        var sourceCols = source.RightCol - source.LeftCol + 1;
        for (var row = target.TopRow; row <= target.BottomRow; row++)
        {
            for (var col = target.LeftCol; col <= target.RightCol; col++)
            {
                var cell = new CellRef(row, col);
                if (!InBounds(cell) || source.Contains(cell)) continue;
                var from = new CellRef(
                    source.TopRow + Mod(row - source.TopRow, sourceRows),
                    source.LeftCol + Mod(col - source.LeftCol, sourceCols));
                var value = Document.GetCell(from);
                var old = Document.GetCell(cell);
                if (old == value) continue;
                edit.Before.Add(new SheetCellSnapshot(cell, old));
                edit.After.Add(new SheetCellSnapshot(cell, value));
                Document.SetCell(cell, value);
            }
        }
        if (edit.Before.Count == 0) return false;
        edit.SelectionAfter = _selection;
        Commit(edit);
        Reveal();
        return true;
    }

    private static int Mod(int value, int size) => ((value % size) + size) % size;

    /// <summary>⌘D: the selection's first row pours down through the rest of it. A single-row
    /// selection takes from the row ABOVE instead — Excel's exact reading.</summary>
    public bool FillDown()
    {
        var selection = _selection;
        if (selection.TopRow == selection.BottomRow)
        {
            if (selection.TopRow == 0) return false;
            var above = new SheetRange(new CellRef(selection.TopRow - 1, selection.LeftCol),
                new CellRef(selection.TopRow - 1, selection.RightCol));
            return Fill(above, selection);
        }
        var first = new SheetRange(new CellRef(selection.TopRow, selection.LeftCol),
            new CellRef(selection.TopRow, selection.RightCol));
        return Fill(first, selection);
    }

    /// <summary>⌘R: the selection's first column pours right — or a single-column selection takes
    /// from the column to its LEFT.</summary>
    public bool FillRight()
    {
        var selection = _selection;
        if (selection.LeftCol == selection.RightCol)
        {
            if (selection.LeftCol == 0) return false;
            var left = new SheetRange(new CellRef(selection.TopRow, selection.LeftCol - 1),
                new CellRef(selection.BottomRow, selection.LeftCol - 1));
            return Fill(left, selection);
        }
        var first = new SheetRange(new CellRef(selection.TopRow, selection.LeftCol),
            new CellRef(selection.BottomRow, selection.LeftCol));
        return Fill(first, selection);
    }

    // ---- the surface: what a realizer drives ---------------------------------------------------

    /// <summary>How far from the selection's bottom-right corner a press still grabs the fill
    /// handle, in dp: the little square is drawn smaller than a fingertip.</summary>
    public const float FillHandleGrab = 8f;

    /// <summary>
    /// The viewport the surface draws: the scroll that places its cells and the pinned headers in front
    /// of them. The composing component hands it over on every build, before it reads the selection;
    /// this is the only place that turns a point into a cell with it, and the selection's corner back
    /// into a point.
    /// </summary>
    public SheetGrid Grid { get; set; } = SheetGrid.Default;

    /// <summary>Whether the press in flight began as a plain single press, whose drag moves the
    /// selection's far corner. A press on the fill handle is <see cref="Filling"/> instead.</summary>
    private bool _selecting;

    /// <summary>The cell a finger landed on, which its release selects; none once a pan took it.</summary>
    private CellRef? _tapped;

    /// <summary>Whether the press in flight began inside the cell's editor, whose moves and release are the
    /// entry's: they place its caret and draw its selection.</summary>
    private bool _entryPressed;

    /// <summary>How far the cell's editor has slid its text to the left, to keep the caret inside it when the
    /// entry's line is wider than the editor can grow.</summary>
    private float _slide;

    /// <summary>How far the cell's editor has slid its text up, to keep the caret's line inside it when the
    /// entry has more lines than the editor can grow rows for.</summary>
    private float _rise;

    /// <summary>Where the drag in flight last stood, which <see cref="Follow"/> reads again when the grid
    /// moves under it.</summary>
    private Point? _dragAt;

    /// <summary>
    /// The cell a point in the surface's own coordinates lands on, read through the document's spans at
    /// the grid's offset at this moment. A point over a pinned header reads the first cell shown past it,
    /// and one past the grid's end its last, so a drag past an edge holds the edge.
    /// </summary>
    public CellRef CellAt(Point point)
    {
        var offset = Grid.Offset;
        var across = Math.Max(point.X - Grid.HeaderWidth, 0) + offset.Across;
        var down = Math.Max(point.Y - Grid.HeaderHeight, 0) + offset.Down;
        return Document.Clamp(new CellRef(Document.RowSpans.IndexAt(down), Document.ColumnSpans.IndexAt(across)));
    }

    /// <summary>The bottom-right corner of the selection's bottom-right cell, in the surface's own
    /// coordinates: where the fill handle sits. The inverse of <see cref="CellAt"/>.</summary>
    private Point FillHandleCorner()
    {
        var offset = Grid.Offset;
        return new Point(
            (float)(Grid.HeaderWidth + Document.ColumnSpans.OffsetOf(_selection.RightCol + 1) - offset.Across),
            (float)(Grid.HeaderHeight + Document.RowSpans.OffsetOf(_selection.BottomRow + 1) - offset.Down));
    }

    /// <summary>A range of cells as a rectangle in the surface's own coordinates, at the grid's offset now.</summary>
    private Rect BoundsOf(SheetRange range)
    {
        var offset = Grid.Offset;
        var left = Grid.HeaderWidth + Document.ColumnSpans.OffsetOf(range.LeftCol) - offset.Across;
        var top = Grid.HeaderHeight + Document.RowSpans.OffsetOf(range.TopRow) - offset.Down;
        var right = Grid.HeaderWidth + Document.ColumnSpans.OffsetOf(range.RightCol + 1) - offset.Across;
        var bottom = Grid.HeaderHeight + Document.RowSpans.OffsetOf(range.BottomRow + 1) - offset.Down;
        return new Rect((float)left, (float)top, (float)(right - left), (float)(bottom - top));
    }

    /// <summary>The last column and the last row the viewport shows any of; the grid's last ones when
    /// nothing scrolls it.</summary>
    private CellRef LastInView()
    {
        if (Grid.Scroll is not { } scroll || scroll.Viewport.Width <= 0 || scroll.Viewport.Height <= 0)
            return new CellRef(Document.Rows - 1, Document.Cols - 1);
        var offset = Grid.Offset;
        var across = offset.Across + Math.Max(0, scroll.Viewport.Width - Grid.HeaderWidth) - 0.5;
        var down = offset.Down + Math.Max(0, scroll.Viewport.Height - Grid.HeaderHeight) - 0.5;
        return Document.Clamp(new CellRef(Document.RowSpans.IndexAt(down), Document.ColumnSpans.IndexAt(across)));
    }

    /// <summary>How tall a line of the cells' type is: what the grid says, or the body's 20 points when
    /// nothing said.</summary>
    private float LineHeight => Grid.LineHeight > 0 ? Grid.LineHeight : 20f;

    /// <summary>
    /// The cells the cell's editor covers while the active cell is edited: the active cell, widened to the
    /// right in whole columns while the entry's widest line needs more room, and down in whole rows while its
    /// lines need more height, never past the last column and the last row in view. At rest, the active cell.
    /// </summary>
    public SheetRange EditorRange
    {
        get
        {
            var active = _active;
            if (!Editing) return new SheetRange(active);
            var padding = Grid.CellPadding;
            var wide = WidestEntryLine() + padding.Start + padding.End + CodeEditorController.CaretWidth;
            var tall = Entry.Document.LineCount * LineHeight + padding.Top + padding.Bottom;
            var last = LastInView();
            var right = active.Col;
            var width = Document.ColumnSpans.ExtentOf(right);
            while (width < wide && right < last.Col)
            {
                right++;
                width += Document.ColumnSpans.ExtentOf(right);
            }
            var bottom = active.Row;
            var height = Document.RowSpans.ExtentOf(bottom);
            while (height < tall && bottom < last.Row)
            {
                bottom++;
                height += Document.RowSpans.ExtentOf(bottom);
            }
            return new SheetRange(active, new CellRef(bottom, right));
        }
    }

    /// <summary>The document and the measurer the widest line was last measured for, and its width: an entry
    /// is measured once per edit, not on every pointer move that asks where the editor is.</summary>
    private CodeDocument? _widestOf;
    private Func<string, float>? _widestBy;
    private float _widest;

    /// <summary>How wide the entry's widest line is in the cells' type; 0 where nothing measures.</summary>
    private float WidestEntryLine()
    {
        if (Grid.Measure is not { } measure) return 0;
        var document = Entry.Document;
        if (ReferenceEquals(document, _widestOf) && ReferenceEquals(measure, _widestBy)) return _widest;
        var widest = 0f;
        for (var line = 0; line < document.LineCount; line++)
            widest = Math.Max(widest, measure(document.Line(line)));
        _widestOf = document;
        _widestBy = measure;
        _widest = widest;
        return widest;
    }

    /// <summary>The cell's editor as a rectangle in the surface's own coordinates.</summary>
    public Rect EditorBounds => BoundsOf(EditorRange);

    /// <summary>
    /// The grid the entry is drawn on in the cell's editor, in the surface's own coordinates: the editor's
    /// corner inside the cell's padding, slid left and up as far as it has to be for the caret to stay inside
    /// the editor, a line of the cells' type tall, measured in the cells' type. What the composing component
    /// draws the entry's lines on, what a press and a drag are read on, and what places the caret the host
    /// paints and the input method's window stands at.
    /// </summary>
    public CodeGrid CellEditorGrid
    {
        get
        {
            var bounds = EditorBounds;
            var padding = Grid.CellPadding;
            // Kept inside the part of the editor that SHOWS: its last column or row may be one the view
            // shows only some of, and a caret at the editor's own edge stood past the viewport's.
            var shown = bounds.Intersect(Body());
            var left = Math.Max(bounds.X + padding.Start, shown.X);
            var right = Math.Min(bounds.Right - padding.End, shown.Right) - CodeEditorController.CaretWidth;
            var top = Math.Max(bounds.Y + padding.Top, shown.Y);
            var bottom = Math.Min(bounds.Bottom - padding.Bottom, shown.Bottom);
            Entry.Grid = CellGrid(bounds, _slide, _rise);
            var caret = Entry.CaretRect(Entry.Caret);
            var slide = _slide;
            if (caret.X > right) slide += caret.X - right;
            else if (caret.X < left) slide -= left - caret.X;
            var rise = _rise;
            if (caret.Bottom > bottom) rise += caret.Bottom - bottom;
            else if (caret.Y < top) rise -= top - caret.Y;
            // The start wins where what shows is narrower or shorter than the caret (a last row the scroll
            // bar half covers): never past the slide that stands the caret at the start, or the two rules
            // undid each other on every call.
            slide = Math.Min(slide, caret.X + _slide - left);
            rise = Math.Min(rise, caret.Y + _rise - top);
            // Never further than the text needs: a line that shrank back shows from its start again, and an
            // entry that lost lines shows from its first.
            slide = Math.Clamp(slide, 0, Math.Max(0, WidestEntryLine() - (right - left)));
            rise = Math.Clamp(rise, 0, Math.Max(0, Entry.Document.LineCount * LineHeight - (bottom - top)));
            if (slide != _slide || rise != _rise)
            {
                _slide = slide;
                _rise = rise;
                Entry.Grid = CellGrid(bounds, slide, rise);
            }
            return Entry.Grid;
        }
    }

    private CodeGrid CellGrid(Rect bounds, float slide, float rise)
    {
        var padding = Grid.CellPadding;
        var origin = new Point(bounds.X + padding.Start - slide, bounds.Y + padding.Top - rise);
        // Measured, a cell of the grid is only the room a band leaves for a line's break.
        return Grid.Measure is { } measure
            ? new CodeGrid(origin, new Size(Math.Max(measure(" "), 4f), LineHeight), Measure: measure)
            : new CodeGrid(origin, new Size(8, LineHeight));
    }

    /// <summary>Puts the entry on the cell's editor's grid, the one the next geometric answer is read on.</summary>
    private void UseCellView()
    {
        EntryView = SheetEntryView.Cell;
        _ = CellEditorGrid;
    }

    /// <summary>Whether a point lies inside the cell's editor while a cell is edited.</summary>
    private bool InEditor(Point position) => Editing && EditorBounds.Contains(position);

    /// <summary>Whether a press at <paramref name="position"/> grabs the fill handle: within
    /// <see cref="FillHandleGrab"/> of the selection's corner, while no cell is being edited and the corner
    /// is drawn, which it is not under a pinned header.</summary>
    private bool OnFillHandle(Point position)
    {
        if (Editing) return false;
        var corner = FillHandleCorner();
        return corner.X > Grid.HeaderWidth && corner.Y > Grid.HeaderHeight
            && Math.Abs(position.X - corner.X) <= FillHandleGrab
            && Math.Abs(position.Y - corner.Y) <= FillHandleGrab;
    }

    /// <inheritdoc />
    public PointerCursor CursorAt(Point position) =>
        InEditor(position) ? PointerCursor.Text
        : OnFillHandle(position) ? PointerCursor.Crosshair
        : PointerCursor.Default;

    /// <inheritdoc />
    public bool HandleKey(string key, KeyModifiers modifiers, KeyboardConvention convention, ITextClipboard? clipboard) =>
        SheetKeymap.Handle(this, key, modifiers, convention, clipboard);

    /// <summary>
    /// Typed text, Excel's way: the first character of an entry REPLACES the active cell (a fresh, empty
    /// entry in Enter mode takes it), and the rest go in at the entry's caret. The document changes on
    /// commit.
    /// </summary>
    public bool HandleText(string text)
    {
        if (text.Length == 0) return false;
        TabReleased = false;
        if (!Editing) BeginEdit("", SheetEditMode.Enter);
        UseCellView();
        return Entry.HandleText(text);
    }

    /// <inheritdoc />
    public bool SetComposition(string text)
    {
        if (!Editing)
        {
            if (text.Length == 0) return false;
            TabReleased = false;
            BeginEdit("", SheetEditMode.Enter);
        }
        UseCellView();
        return Entry.SetComposition(text);
    }

    /// <inheritdoc />
    public IReadOnlyList<Rect> Carets
    {
        get
        {
            if (!Editing || EntryView != SheetEntryView.Cell) return [];
            _ = CellEditorGrid;
            // Clipped to the part of the editor that shows: an editor scrolled partly under the pinned
            // headers is drawn clipped by the body, and a caret drawn whole stood over the headers.
            var shown = EditorBounds.Intersect(Body());
            var carets = new List<Rect>();
            foreach (var caret in Entry.Carets)
            {
                var clipped = caret.Intersect(shown);
                if (clipped.Width > 0 && clipped.Height > 0) carets.Add(clipped);
            }
            return carets;
        }
    }

    /// <summary>The cells' part of the surface, past the pinned headers, as far as the viewport shows; the
    /// whole editor when nothing scrolls the grid.</summary>
    private Rect Body() =>
        Grid.Scroll is { } scroll && scroll.Viewport.Width > 0 && scroll.Viewport.Height > 0
            ? Rect.FromLTRB(Grid.HeaderWidth, Grid.HeaderHeight, scroll.Viewport.Width, scroll.Viewport.Height)
            : EditorBounds;

    /// <inheritdoc />
    public Rect InputBounds =>
        Carets is { Count: > 0 } carets ? carets[0] : BoundsOf(new SheetRange(_active));

    private int _focusVersion;

    /// <inheritdoc />
    public int FocusVersion => _focusVersion;

    /// <summary>Asks for the keyboard: whichever host draws this sheet gives it to the surface on its next
    /// frame. What the formula bar and the name box call when they are done with it.</summary>
    public void RequestFocus() => _focusVersion++;

    /// <inheritdoc />
    public void FocusChanged(bool focused)
    {
        // Coming back takes Tab again, as a code editor's does; leaving cancels a composition and keeps the
        // entry open, as Excel keeps it while another window has the keyboard.
        if (focused) TabReleased = false;
        else if (Editing && EntryView == SheetEntryView.Cell) Entry.FocusChanged(false);
    }

    /// <summary>
    /// Whether the next Tab LEAVES the sheet instead of moving the active cell: Escape at rest sets it, the
    /// one way out a keyboard has from a grid that takes Tab, and any other key, or coming back to the sheet,
    /// clears it (WCAG 2.1.2, as the code editor's <c>TabMovesFocus</c>).
    /// </summary>
    public bool TabReleased { get; set; }

    /// <summary>
    /// Whether <paramref name="command"/> is one a cell's entry has no use for, among the code engine's
    /// chords: indenting, comments, the line operations, and the completion, signature, hover and problem
    /// keys until a language for formulas brings them (slice 7). The entry gives them back to the host
    /// rather than run them.
    /// </summary>
    private static bool NotACellsCommand(CodeCommand command) =>
        command is CodeCommand.Indent or CodeCommand.Outdent or CodeCommand.ToggleLineComment
            or CodeCommand.MoveLinesUp or CodeCommand.MoveLinesDown or CodeCommand.CopyLinesUp
            or CodeCommand.CopyLinesDown or CodeCommand.DeleteLines or CodeCommand.JoinLines
            or CodeCommand.InsertLineAbove or CodeCommand.InsertLineBelow or CodeCommand.CompletionShow
            or CodeCommand.SignatureHelpShow or CodeCommand.HoverShow or CodeCommand.ProblemNext
            or CodeCommand.ProblemPrevious;

    /// <summary>The keyboard tradition the entry's given-back chords were read for, or null before any.</summary>
    private KeyboardConvention? _entryKeysFor;

    /// <summary>
    /// A key for the entry, through the code engine's keymap in the keyboard's own tradition, once the chords
    /// a cell has no use for are given back (<see cref="CodeEditorController.KeyBindings"/>). Answers whether
    /// the entry claimed it.
    /// </summary>
    internal bool EntryKey(string key, KeyModifiers modifiers, KeyboardConvention convention)
    {
        if (_entryKeysFor != convention)
        {
            Entry.KeyBindings.Clear();
            foreach (var binding in CodeKeymap.Defaults(convention))
            {
                if (NotACellsCommand(binding.Command))
                    Entry.KeyBindings.Add(binding with { Command = CodeCommand.None });
            }
            _entryKeysFor = convention;
        }
        UseView();
        return Entry.HandleKey(key, modifiers, convention, null);
    }

    /// <summary>
    /// What a pointer MEANS on a sheet, for every host. A press lands a draft first, as clicking away
    /// does in Excel; then it grabs the fill handle when it falls within <see cref="FillHandleGrab"/> of
    /// the selection's corner, or selects the cell under it, stretching the selection from its anchor
    /// with Shift, and the second press of a double click begins editing that cell with its value. A
    /// drag that began with a plain single press moves the selection's far corner with the pointer;
    /// one that grabbed the handle previews the fill, and the release lands it as one undo step. Either
    /// drag hands its position to the grid's scroll, which scrolls while it is held past an edge. A
    /// press the system cancels lands nothing, and a press that finds a fill still in flight drops it.
    /// <para>
    /// A finger is the exception, as a touch platform reads one: its press means nothing until it
    /// lifts, because the grid may take it as a pan; its release selects the cell it began on, and a
    /// cancel (the pan) selects nothing.
    /// </para>
    /// </summary>
    public bool HandlePointer(PointerPhase phase, Point position, KeyModifiers modifiers, int clicks, PointerKind kind)
    {
        switch (phase)
        {
            case PointerPhase.Down:
            {
                // A fill whose release never came is over: this press starts afresh.
                _selecting = false;
                _tapped = null;
                _entryPressed = false;
                if (Filling) CancelFill();
                if (kind == PointerKind.Touch)
                {
                    _tapped = CellAt(position);
                    return false;
                }
                // Inside the cell's editor a press is the entry's: it places the caret, a double click
                // selects a word and a drag selects text, in Edit mode, as a press in Excel's editor does.
                if (InEditor(position))
                {
                    EditMode = SheetEditMode.Edit;
                    UseCellView();
                    _entryPressed = true;
                    Entry.HandlePointer(PointerPhase.Down, position, modifiers, clicks);
                    return true;
                }
                if (Editing) CommitEdit();
                if (OnFillHandle(position))
                {
                    BeginFill();
                    _dragAt = position;
                    return true;
                }
                Press(CellAt(position), position, modifiers, clicks);
                _selecting = clicks < 2 && (modifiers & KeyModifiers.Shift) == 0;
                if (_selecting) _dragAt = position;
                return true;
            }
            case PointerPhase.Move:
            {
                if (_entryPressed)
                {
                    UseCellView();
                    return Entry.HandlePointer(PointerPhase.Move, position, modifiers, clicks);
                }
                if (!Filling && !_selecting) return false;
                _dragAt = position;
                // Past an edge of the cells the grid scrolls toward the pointer, and the move answers
                // true whatever cell it reached, so the host asks for the frames that scroll it. Inside
                // them nothing scrolls, and a glide in flight goes on.
                var past = PastTheCells(position);
                Grid.Scroll?.AutoScroll(past ? position : null);
                return Drag(position) || past;
            }
            case PointerPhase.Up:
            {
                // A drag that drew a range ends here, and what shows its size changes with it.
                var wasSelecting = Selecting;
                StopDrag();
                if (_entryPressed)
                {
                    _entryPressed = false;
                    UseCellView();
                    Entry.HandlePointer(PointerPhase.Up, position, modifiers, clicks);
                    return true;
                }
                if (_tapped is { } tapped)
                {
                    _tapped = null;
                    // A finger that lifts inside the editor places the caret there, in Edit mode.
                    if (InEditor(position))
                    {
                        EditMode = SheetEditMode.Edit;
                        UseCellView();
                        Entry.Selection = new CodeRange(Entry.PositionAt(position));
                        return true;
                    }
                    if (Editing) CommitEdit();
                    Press(tapped, position, modifiers, clicks);
                    return true;
                }
                if (!Filling) return wasSelecting;
                CommitFill();
                return true;
            }
            case PointerPhase.Cancel:
            {
                // Taken away, not released: the selection stays where the drag left it, a fill pours
                // nothing, and a finger the grid took as a pan selects nothing.
                StopDrag();
                _tapped = null;
                if (_entryPressed)
                {
                    _entryPressed = false;
                    Entry.HandlePointer(PointerPhase.Cancel, position, modifiers, clicks);
                    return true;
                }
                if (!Filling) return false;
                CancelFill();
                return true;
            }
            default:
                return false;
        }
    }

    /// <summary>
    /// The grid moved under a drag in flight (the viewport reports every offset, and the component hands
    /// it here): the drag's last position is read again as a move, so a selection held past an edge
    /// keeps reaching the cells that scroll in under it. Answers whether the selection or the fill's
    /// preview changed; with no drag in flight it changes nothing.
    /// </summary>
    public bool Follow() => _dragAt is { } at && (Filling || _selecting) && Drag(at);

    /// <summary>A press that selects: the cell, the selection stretched to it with Shift, and the
    /// second press of a double click editing it in Edit mode, with the caret where it landed.</summary>
    private void Press(CellRef cell, Point position, KeyModifiers modifiers, int clicks)
    {
        if ((modifiers & KeyModifiers.Shift) != 0) SelectTo(cell);
        else Select(new SheetRange(cell));
        if (clicks < 2) return;
        BeginEdit(Document.GetCell(cell), SheetEditMode.Edit);
        UseCellView();
        Entry.Selection = new CodeRange(Entry.PositionAt(position));
    }

    /// <summary>The drag in flight reaches the cell under <paramref name="position"/>, held to the cells
    /// the grid shows: past an edge it holds the edge, as Excel's does, and the rows beyond reach it as
    /// the grid scrolls them in.</summary>
    private bool Drag(Point position)
    {
        var cell = CellAt(InsideTheCells(position));
        if (Filling)
        {
            var before = FillTarget;
            UpdateFill(cell);
            return FillTarget != before;
        }
        if (cell == _selection.Focus) return false;
        Select(new SheetRange(_selection.Anchor, cell));
        return true;
    }

    /// <summary>Whether a point lies past an edge of the part of the viewport where the cells scroll:
    /// over a pinned header, or past the window. Without a viewport, nothing is past.</summary>
    private bool PastTheCells(Point position) =>
        Grid.Scroll is { } scroll
        && (position.X < Grid.HeaderWidth || position.Y < Grid.HeaderHeight
            || position.X >= scroll.Viewport.Width || position.Y >= scroll.Viewport.Height);

    /// <summary>The point held inside the cells the viewport shows; without a viewport, the point.</summary>
    private Point InsideTheCells(Point position)
    {
        if (Grid.Scroll is not { } scroll || scroll.Viewport.Width <= 0 || scroll.Viewport.Height <= 0) return position;
        return new Point(
            Math.Clamp(position.X, Grid.HeaderWidth, Math.Max(Grid.HeaderWidth, scroll.Viewport.Width - 1)),
            Math.Clamp(position.Y, Grid.HeaderHeight, Math.Max(Grid.HeaderHeight, scroll.Viewport.Height - 1)));
    }

    private void StopDrag()
    {
        _selecting = false;
        if (_dragAt is null) return;
        _dragAt = null;
        Grid.Scroll?.AutoScroll(null);
    }

    // ---- undo/redo -------------------------------------------------------------------------------

    public bool Undo()
    {
        var edit = History.PopUndo();
        if (edit is null) return false;
        Apply(edit, forward: false);
        _selection = edit.SelectionBefore;
        _active = _selection.Focus;
        Reveal();
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
        Reveal();
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
