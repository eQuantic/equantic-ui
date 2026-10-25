using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;

namespace eQuantic.UI.Spreadsheets;

/// <summary>
/// The editable grid, write-once: a <see cref="SheetSurface"/> over a <see cref="TableView"/> whose
/// pinned first row is the column headers, whose pinned first column is the row numbers, and whose cell
/// <c>(r + 1, c + 1)</c> is the sheet's cell <c>(r, c)</c>. Headers and cells share one layout and one
/// scroll, so a cell starts under its column's header at every width and every offset, by construction,
/// and only the cells in view are built, on both axes, up to Excel's 1 048 576 rows by 16 384 columns.
/// <para>
/// A cell is built from its value alone. The selection's band, the active cell's ring, the fill's
/// preview and the fill handle are marks the table draws above the cells, from the engine's rectangles,
/// so a selection change restyles no cell. The surface wraps the whole table, so its coordinates are
/// the table's: the engine reads every press against the scroll this component keeps
/// (<see cref="SheetController.Grid"/>), a key that moves the active cell asks the table to reveal it,
/// and a drag held past an edge scrolls the grid while the engine follows it.
/// </para>
/// <para>
/// All interaction lives in the <see cref="SheetController"/> the app owns (press and drag select,
/// Excel's keyboard, TSV clipboard, in-cell editing), the surface's model.
/// </para>
/// </summary>
public sealed class Spreadsheet : StatefulComponent
{
    /// <summary>What the row-number column is laid out for before the table has said how tall its window
    /// is.</summary>
    private const double FirstGuess = 768;

    private readonly TableScroll _scroll = new();
    private LeadingSpans? _rows;
    private LeadingSpans? _columns;
    private SheetDocument? _document;

    /// <summary>How many digits the widest row number the window shows has.</summary>
    private int _digits = 1;

    /// <summary>The configuration of the latest build: a parent that builds again hands a new one, and
    /// its init-only settings are read from it.</summary>
    private Spreadsheet _config;

    /// <summary>What a refresh of the controller does here: build again. Kept as one delegate, so the
    /// component unsubscribes exactly what it subscribed.</summary>
    private readonly Action _rebuild;

    /// <summary>The controller this component listens to while it is mounted, or null.</summary>
    private SheetController? _listening;

    public Spreadsheet(SheetController controller)
    {
        Controller = controller;
        _config = this;
        _rebuild = () => SetState(() => { });
    }

    public SheetController Controller { get; private set; }

    public SizeValue Width { get; init; }
    public SizeValue Height { get; init; }

    /// <summary>Rows and columns built past each edge of the window, so a scroll shows cells, not blank.</summary>
    public int Overscan { get; init; } = 3;

    /// <summary>The narrowest the row-number column is, however few digits its numbers have.</summary>
    public const float HeaderWidth = 44f;
    public const float HeaderHeight = 26f;

    /// <summary>What the row-number column keeps on each side of its widest number.</summary>
    public const float NumberPadding = 8f;

    /// <summary>What a cell keeps between its gridlines and its text: six points on each side, and the body's
    /// 20-point line centred in the default row's 28. A property that builds on read, as every value the
    /// runtime's barrel may evaluate before its module.</summary>
    public static EdgeInsets CellPadding => EdgeInsets.Symmetric(6, 4);

    /// <summary>The type a cell's value and its entry are written in.</summary>
    private const TypeRole CellType = TypeRole.BodyM;

    /// <summary>Resize floors — Excel lets a column vanish; we keep a sliver so it stays grabbable.</summary>
    public const float MinColWidth = 24f;
    public const float MinRowHeight = 14f;

    /// <summary>Grab zone width on a header's trailing edge, where the resize drag lives.</summary>
    public const float Grip = 6f;

    /// <summary>The fill handle's painted size (its grab zone in the surface is a little larger).</summary>
    public const float FillHandle = 7f;

    /// <summary>The size the resized row/col had when the drag engaged; −1 outside a gesture.</summary>
    private float _resizeBase = -1f;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not Spreadsheet fresh) return;
        if (_listening is not null) Listen(fresh.Controller);
        Controller = fresh.Controller;
        _config = fresh;
    }

    protected override void OnMount() => Listen(Controller);

    protected override void OnUnmount() => Listen(null);

    /// <summary>Listens to <paramref name="controller"/>'s refreshes, and to no other.</summary>
    private void Listen(SheetController? controller)
    {
        if (ReferenceEquals(_listening, controller)) return;
        if (_listening is not null) _listening.Refreshed -= _rebuild;
        _listening = controller;
        if (controller is not null) controller.Refreshed += _rebuild;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var palette = SheetPalette.Of(theme);
        var document = Controller.Document;
        _digits = DigitsShown(document);

        // As wide as the widest number the window shows, in the header's type: row 10 000 needs a column
        // row 10 does not. Measured, and never narrower than the floor, which is also what a render with
        // no measurer gets.
        var numbers = Math.Max(HeaderWidth,
            MathF.Ceiling(context.MeasureText(new string('0', _digits), theme.Type(TypeRole.Caption))) + 2 * NumberPadding);
        if (_rows is null || _columns is null || _document != document || _columns.Lead != numbers)
        {
            _document = document;
            _rows = new LeadingSpans(document.RowSpans, HeaderHeight);
            _columns = new LeadingSpans(document.ColumnSpans, numbers);
        }

        // THE geometry, handed to the engine: the scroll that places the cells, read live, the bands
        // pinned in front of them, and the cells' type, measured here. The only thing that turns a press into
        // a cell, the selection's corner into the fill handle's place and a column of the entry into its
        // caret, so the cells drawn and the cells pressed cannot disagree.
        var cellType = theme.Type(CellType);
        Controller.Grid = new SheetGrid(_scroll, numbers, HeaderHeight)
        {
            Measure = text => context.MeasureText(text, cellType),
            LineHeight = cellType.LineHeight,
            CellPadding = CellPadding,
        };

        var revealed = Controller.RevealCell;
        var table = new TableView(_rows, _columns, (row, column) => Cell(row, column, document, palette, numbers))
        {
            PinnedRows = 1,
            PinnedColumns = 1,
            Overscan = _config.Overscan,
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Scroll = _scroll,
            Marks = Marks(palette),
            Request = new TableScrollRequest(Controller.RevealVersion) { Reveal = new TableRange(revealed.Row + 1, revealed.Col + 1) },
            OnScrolled = Scrolled,
            OnViewportChanged = ViewportChanged,
            // The surface is the sheet's one stop of the Tab order, and an arrow moves the active cell.
            CanRequestFocus = false,
        };

        return new Box(new BoxStyle
        {
            Width = _config.Width,
            Height = _config.Height,
            BorderColor = palette.Gridline,
            BorderWidth = 1f,
            Clip = true,
        }, new SheetSurface(
            // The surface fills by what it holds, and a component says nothing until it builds: the box
            // says it, so the sheet takes the room the page gives it rather than its table's first guess.
            new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }, table), Controller)
        {
            // Every view of the sheet builds again, this one among them: a key here moves what the formula
            // bar and the name box show.
            OnChanged = () => Controller.Refresh(),
            Label = SdkStrings.Spreadsheet,
            CaretColor = palette.CellText,
        });
    }

    /// <summary>How many digits the last row number the window shows has, at the scroll's offset now.</summary>
    private int DigitsShown(SheetDocument document)
    {
        var height = _scroll.Viewport.Height > 0 ? _scroll.Viewport.Height : FirstGuess;
        var last = document.RowSpans.IndexAt(_scroll.Offset.Down + height) + 1;
        return last.ToString().Length;
    }

    /// <summary>The grid moved: a drag in flight reaches what scrolled in under it, and a row number that
    /// gained a digit widens its column.</summary>
    private void Scrolled(TableOffset offset)
    {
        if (Controller.Follow()) Controller.Refresh();
        else if (DigitsShown(Controller.Document) != _digits) SetState(() => { });
    }

    private void ViewportChanged(Size viewport)
    {
        if (DigitsShown(Controller.Document) != _digits) SetState(() => { });
    }

    /// <summary>The table's cell at a row and a column: the corner, a column header, a row number, or the
    /// sheet's own cell one up and one to the left.</summary>
    private VisualNode Cell(int row, int column, SheetDocument document, SheetPalette palette, float numbers)
    {
        if (row == 0 && column == 0)
            return HeaderCell("", palette, onPressed: () =>
            {
                Controller.SelectAll();
                Controller.Refresh();
            });
        if (row == 0) return ColumnHeader(column - 1, palette);
        if (column == 0) return RowHeader(row - 1, palette, numbers);
        return SheetCell(new CellRef(row - 1, column - 1), document, palette);
    }

    private static VisualNode HeaderCell(string label, SheetPalette palette, bool selected = false,
        Action? onPressed = null)
    {
        // A header inside the selection's band shades, exactly as Excel marks it.
        var cell = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = selected ? palette.HeaderSelectedBackground : palette.HeaderBackground,
            BorderColor = palette.Gridline,
            BorderWidth = 0.5f,
        }, label.Length == 0 ? null : new Text(label, TypeRole.Caption,
            selected ? palette.HeaderSelectedText : palette.HeaderText, maxLines: 1).Centered());
        // A header press leaves the keyboard on the sheet, the one stop of the Tab order, as Excel's
        // header does: the arrows go on moving the active cell after a column is selected.
        return onPressed is null ? cell : new Pressable(cell, onPressed) { CanRequestFocus = false };
    }

    // ---- resize grips: an invisible Draggable riding each header's trailing edge ----------------
    // The grip clamps in the COMPONENT (not via the node's Min/Max — those would rebuild mid-drag),
    // previews straight into the document every move, and the release replays the whole gesture as
    // ONE undoable Controller.Resize.

    private VisualNode ColumnHeader(int c, SheetPalette palette)
    {
        var col = c;   // the loop variable would be shared by every closure
        var selection = Controller.Selection;
        var inBand = c >= selection.LeftCol && c <= selection.RightCol;
        var stack = new Stack();
        stack.Add(HeaderCell(CellRef.ColumnName(c), palette, selected: inBand,
            onPressed: () =>
            {
                Controller.SelectCols(col, col);
                Controller.Refresh();
            }));
        stack.Add(new Positioned(new Draggable(new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(Grip),
            Height = SizeValue.Fixed(HeaderHeight),
            Cursor = PointerCursor.ColResize,
        }))
        {
            Axis = DragAxis.Horizontal,
            Min = -4000,
            Max = 4000,
            Follows = false,
            OnMoved = delta => PreviewResize(SheetAxis.Cols, col, delta),
            OnReleased = delta => CommitResize(SheetAxis.Cols, col, delta),
        }, top: 0, end: 0) { Layer = 1 });
        return stack;
    }

    private VisualNode RowHeader(int r, SheetPalette palette, float width)
    {
        var row = r;
        var selection = Controller.Selection;
        var inBand = r >= selection.TopRow && r <= selection.BottomRow;
        var stack = new Stack();
        stack.Add(HeaderCell($"{r + 1}", palette, selected: inBand,
            onPressed: () =>
            {
                Controller.SelectRows(row, row);
                Controller.Refresh();
            }));
        stack.Add(new Positioned(new Draggable(new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(width),
            Height = SizeValue.Fixed(Grip),
            Cursor = PointerCursor.RowResize,
        }))
        {
            Axis = DragAxis.Vertical,
            Min = -4000,
            Max = 4000,
            Follows = false,
            OnMoved = delta => PreviewResize(SheetAxis.Rows, row, delta),
            OnReleased = delta => CommitResize(SheetAxis.Rows, row, delta),
        }, bottom: 0, start: 0) { Layer = 1 });
        return stack;
    }

    private void PreviewResize(SheetAxis axis, int index, float delta)
    {
        var document = Controller.Document;
        if (_resizeBase < 0)
            _resizeBase = axis == SheetAxis.Cols ? document.ColWidth(index) : document.RowHeight(index);
        var floor = axis == SheetAxis.Cols ? MinColWidth : MinRowHeight;
        var size = Math.Max(floor, _resizeBase + delta);
        SetState(() =>
        {
            if (axis == SheetAxis.Cols) document.SetColWidth(index, size);
            else document.SetRowHeight(index, size);
        });
    }

    private void CommitResize(SheetAxis axis, int index, float delta)
    {
        if (_resizeBase < 0) return;   // a tap on the grip never armed the gesture
        var document = Controller.Document;
        var floor = axis == SheetAxis.Cols ? MinColWidth : MinRowHeight;
        var final = Math.Max(floor, _resizeBase + delta);

        // Rewind the preview without touching history, then land the gesture as one inverse.
        if (axis == SheetAxis.Cols) document.SetColWidth(index, _resizeBase);
        else document.SetRowHeight(index, _resizeBase);
        _resizeBase = -1f;
        Controller.Resize(axis, index, final);
        SetState(() => { });
    }

    /// <summary>A cell of the sheet, from its value alone: the entry being typed into it is drawn over it,
    /// as a mark, by the cell's editor.</summary>
    private static VisualNode SheetCell(CellRef cell, SheetDocument document, SheetPalette palette)
    {
        var value = document.GetCell(cell);
        VisualNode? content = null;
        if (value.Contains('\n'))
        {
            // A value of several lines shows them from the top, each on its own, as many as the row's
            // height holds: the row does not grow with them until wrapping rows come (slice 6).
            var lines = new Column(gap: 0);
            foreach (var line in value.Split('\n'))
                lines.Add(new Text(line, CellType, palette.CellText, maxLines: 1));
            content = lines;
        }
        else if (value.Length > 0)
        {
            content = new Text(value, CellType, palette.CellText, maxLines: 1);
        }

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = palette.CellBackground,
            BorderColor = palette.Gridline,
            BorderWidth = 0.5f,
            Padding = CellPadding,
            Clip = true,
        }, content);
    }

    /// <summary>
    /// What the selection draws above the cells, from the engine's rectangles in the table's indices: the
    /// band over a range of more than one cell, the fill's preview, the active cell's ring, and the fill
    /// handle on the selection's bottom-right corner.
    /// </summary>
    private List<TableViewCell> Marks(SheetPalette palette)
    {
        var marks = new List<TableViewCell>();
        var selection = Controller.Selection;
        if (!selection.IsSingleCell)
        {
            marks.Add(Mark(selection, new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
                Background = palette.RangeFill,
            })));
        }
        if (Controller.FillTarget is { } preview)
        {
            marks.Add(Mark(preview, new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
                BorderColor = palette.FillPreview,
                BorderWidth = 1.5f,
            })));
        }
        if (Controller.Editing)
        {
            marks.Add(Mark(Controller.EditorRange, Editor(palette)));
            return marks;
        }
        marks.Add(Mark(new SheetRange(Controller.ActiveCell), new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            BorderColor = palette.ActiveOutline,
            BorderWidth = 2f,
        })));
        // Excel's little square on the selection's corner. Grabbing it, and the crosshair over it, are
        // the engine's answers; this is only the pixels.
        // The Stack takes the cell's size from a box that draws nothing, and the handle sits on its
        // corner.
        var corner = new Stack();
        corner.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }));
        corner.Add(new Positioned(new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(FillHandle),
            Height = SizeValue.Fixed(FillHandle),
            Background = palette.FillHandle,
            BorderColor = palette.CellBackground,
            BorderWidth = 1f,
        }), bottom: -1, end: -1));
        marks.Add(Mark(new SheetRange(new CellRef(selection.BottomRow, selection.RightCol)), corner));
        return marks;
    }

    /// <summary>
    /// The cell's editor, over the cells the engine says it covers: the cells' background, hiding what it
    /// covers, the active outline around it, and the entry's lines on the grid the engine placed them on,
    /// with its selection and its composition while the editor holds the caret. It clips, so a line slid
    /// to keep the caret in view shows its part that fits.
    /// </summary>
    private Box Editor(SheetPalette palette)
    {
        var grid = Controller.CellEditorGrid;
        var bounds = Controller.EditorBounds;
        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = palette.CellBackground,
            BorderColor = palette.ActiveOutline,
            BorderWidth = 2f,
            Clip = true,
        }, new EntryLines(Controller.Entry, grid, new Point(bounds.X, bounds.Y), CellType, palette,
            active: Controller.EntryView == SheetEntryView.Cell));
    }

    /// <summary>A mark over a range of the sheet, in the table's indices (one past the headers).</summary>
    private static TableViewCell Mark(SheetRange range, VisualNode child) =>
        new(range.TopRow + 1, range.LeftCol + 1, child, mark: true)
        {
            Rows = range.BottomRow - range.TopRow + 1,
            Columns = range.RightCol - range.LeftCol + 1,
        };
}
