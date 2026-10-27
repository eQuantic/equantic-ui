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
            ResizeGrip = Grip,
        };

        var revealed = Controller.RevealCell;
        var ranges = Controller.Ranges;
        var active = Controller.ActiveCell;
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
            // The grid a reader walks (#1081): the letters and the numbers head their columns and rows, the
            // corner is nothing to reach, the cells of every range are selected and the active cell is current.
            Role = TableRole.Grid,
            Label = SdkStrings.Spreadsheet,
            CellRole = (row, column) => CellRoleAt(row, column),
            Selected = (row, column) => row > 0 && column > 0 && InAny(ranges, new CellRef(row - 1, column - 1)),
            Current = new TableRange(active.Row + 1, active.Col + 1),
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
            Announcement = Announcement(document),
        });
    }

    /// <summary>What a cell of the table is to a reader: the headers' row heads the columns, the headers'
    /// column heads the rows, and the corner between them is nothing to reach.</summary>
    private static TableCellRole CellRoleAt(int row, int column) =>
        row == 0 && column == 0 ? TableCellRole.None
        : row == 0 ? TableCellRole.ColumnHeader
        : column == 0 ? TableCellRole.RowHeader
        : TableCellRole.Cell;

    /// <summary>Whether <paramref name="cell"/> lies in one of <paramref name="ranges"/>.</summary>
    private static bool InAny(IReadOnlyList<SheetRange> ranges, CellRef cell)
    {
        for (var i = 0; i < ranges.Count; i++)
            if (ranges[i].Contains(cell)) return true;
        return false;
    }

    /// <summary>
    /// What the grid says now (<see cref="SheetSurface.Announcement"/>, #1081): the active range's address
    /// and size, when it changed and covers more than one cell, or else the active cell's address and what
    /// it holds, as it moves (<see cref="SheetController.Moved"/>). The same words for the same selection,
    /// however often the grid builds, so a reader hears a move once.
    /// </summary>
    private string Announcement(SheetDocument document)
    {
        var range = Controller.Selection;
        if (Controller.Moved == SheetMove.ActiveRange && !range.IsSingleCell)
            return SdkStrings.RangeAnnouncement(
                range.TopLeft.ToA1() + ":" + new CellRef(range.BottomRow, range.RightCol).ToA1(),
                range.RowCount, range.ColCount);
        var cell = Controller.ActiveCell;
        return SdkStrings.CellAnnouncement(cell.ToA1(), document.GetCell(cell));
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
        // A header is drawn here and READ by the engine: a press on it reaches the sheet's surface with its
        // modifiers, so Shift and ⌘ or Ctrl on a header, and a drag across headers, mean what they mean on a cell.
        if (row == 0 && column == 0) return HeaderCell("", palette);
        if (row == 0) return ColumnHeader(column - 1, palette);
        if (column == 0) return RowHeader(row - 1, palette, numbers);
        return SheetCell(new CellRef(row - 1, column - 1), document, palette);
    }

    private static VisualNode HeaderCell(string label, SheetPalette palette, bool selected = false, bool whole = false)
    {
        // A header inside a range's band shades, exactly as Excel marks it, and strongly when a range covers
        // its whole column or row.
        var background = whole ? palette.HeaderWholeBackground
            : selected ? palette.HeaderSelectedBackground : palette.HeaderBackground;
        var ink = whole ? palette.HeaderWholeText : selected ? palette.HeaderSelectedText : palette.HeaderText;
        var cell = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = background,
            BorderColor = palette.Gridline,
            BorderWidth = 0.5f,
        }, label.Length == 0 ? null : new Text(label, TypeRole.Caption, ink, maxLines: 1).Centered());
        return cell;
    }

    // ---- resize grips: an invisible Draggable riding each header's trailing edge ----------------
    // The grip clamps in the COMPONENT (not via the node's Min/Max — those would rebuild mid-drag),
    // previews straight into the document every move, and the release replays the whole gesture as
    // ONE undoable Controller.Resize.

    private VisualNode ColumnHeader(int c, SheetPalette palette)
    {
        var col = c;   // the loop variable would be shared by every closure
        var (inBand, whole) = ColumnEmphasis(c);
        var stack = new Stack();
        stack.Add(HeaderCell(CellRef.ColumnName(c), palette, selected: inBand, whole: whole));
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
        var (inBand, whole) = RowEmphasis(r);
        var stack = new Stack();
        stack.Add(HeaderCell($"{r + 1}", palette, selected: inBand, whole: whole));
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

    /// <summary>Whether a range crosses column <paramref name="col"/>, and whether one covers it whole, top to
    /// bottom.</summary>
    private (bool InBand, bool Whole) ColumnEmphasis(int col)
    {
        var inBand = false;
        var lastRow = Controller.Document.Rows - 1;
        foreach (var range in Controller.Ranges)
        {
            if (col < range.LeftCol || col > range.RightCol) continue;
            if (range.TopRow == 0 && range.BottomRow == lastRow) return (true, true);
            inBand = true;
        }
        return (inBand, false);
    }

    /// <summary>Whether a range crosses row <paramref name="row"/>, and whether one covers it whole, edge to
    /// edge.</summary>
    private (bool InBand, bool Whole) RowEmphasis(int row)
    {
        var inBand = false;
        var lastCol = Controller.Document.Cols - 1;
        foreach (var range in Controller.Ranges)
        {
            if (row < range.TopRow || row > range.BottomRow) continue;
            if (range.LeftCol == 0 && range.RightCol == lastCol) return (true, true);
            inBand = true;
        }
        return (inBand, false);
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
    /// What the selection draws above the cells, from the engine's rectangles in the table's indices: every
    /// range as one rectangle (a translucent fill, the active cell left unfilled, under one outline), the
    /// fill's preview, the active cell's ring, and the fill handle on the active range's bottom-right corner;
    /// in the dimmed colours while the sheet does not hold the keyboard.
    /// </summary>
    private List<TableViewCell> Marks(SheetPalette palette)
    {
        var marks = new List<TableViewCell>();
        var selection = Controller.Selection;
        var focused = Controller.HasKeyboard;
        var fill = focused ? palette.RangeFill : palette.DimmedRangeFill;
        var outline = focused ? palette.RangeOutline : palette.DimmedOutline;
        var ranges = Controller.Ranges;
        for (var i = 0; i < ranges.Count; i++)
        {
            var range = ranges[i];
            var active = i == ranges.Count - 1;
            // A lone active cell is the ring's alone.
            if (active && range.IsSingleCell) continue;
            foreach (var piece in Around(range, active ? Controller.ActiveCell : null))
                marks.Add(Mark(piece, new Box(new BoxStyle
                {
                    Width = SizeValue.Fill,
                    Height = SizeValue.Fill,
                    Background = fill,
                })));
            marks.Add(Mark(range, new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = SizeValue.Fill,
                BorderColor = outline,
                BorderWidth = 1.5f,
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
        // The cell cursor GLIDES to where a key or a press moves it, over the theme's base motion, and jumps
        // under reduced motion: its key keeps it the same mark from one build to the next.
        marks.Add(Mark(new SheetRange(Controller.ActiveCell), new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            BorderColor = focused ? palette.ActiveOutline : palette.DimmedOutline,
            BorderWidth = 2f,
        }), CursorKey, Motion.State));
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
            Background = focused ? palette.FillHandle : palette.DimmedOutline,
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

    /// <summary>
    /// <paramref name="range"/> as the rectangles its fill is drawn in: the whole range, or, around the active
    /// cell it holds, the rows above and below it and the cells beside it on its row, so the active cell stays
    /// unfilled without a mark covering its value.
    /// </summary>
    private static List<SheetRange> Around(SheetRange range, CellRef? hole)
    {
        if (hole is not { } cell || !range.Contains(cell)) return [range];
        var pieces = new List<SheetRange>();
        if (cell.Row > range.TopRow)
            pieces.Add(new SheetRange(new CellRef(range.TopRow, range.LeftCol), new CellRef(cell.Row - 1, range.RightCol)));
        if (cell.Row < range.BottomRow)
            pieces.Add(new SheetRange(new CellRef(cell.Row + 1, range.LeftCol), new CellRef(range.BottomRow, range.RightCol)));
        if (cell.Col > range.LeftCol)
            pieces.Add(new SheetRange(new CellRef(cell.Row, range.LeftCol), new CellRef(cell.Row, cell.Col - 1)));
        if (cell.Col < range.RightCol)
            pieces.Add(new SheetRange(new CellRef(cell.Row, cell.Col + 1), new CellRef(cell.Row, range.RightCol)));
        return pieces;
    }

    /// <summary>The key of the cell cursor's mark, which keeps it one mark from build to build so it glides.</summary>
    private const string CursorKey = "cursor";

    /// <summary>A mark over a range of the sheet, in the table's indices (one past the headers), keyed and gliding
    /// when <paramref name="glide"/> is given.</summary>
    private static TableViewCell Mark(SheetRange range, VisualNode child, string? key = null, MotionSpec? glide = null) =>
        new(range.TopRow + 1, range.LeftCol + 1, child, mark: true)
        {
            Rows = range.BottomRow - range.TopRow + 1,
            Columns = range.RightCol - range.LeftCol + 1,
            Key = key,
            Glide = glide,
        };
}
