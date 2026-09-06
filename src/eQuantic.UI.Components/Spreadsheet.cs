using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The editable grid, write-once: column headers fixed on top, row headers scrolling WITH the
/// rows, and the visible window of cells wrapped in a <see cref="SheetSurface"/> that lives
/// INSIDE the scroll — so the selection band and the active-cell ring translate with the content
/// and stay aligned at any scroll position, by construction. Rows virtualize the ListView way:
/// the window plus spacers, converging through the ScrollView's out-channels.
/// <para>
/// All interaction lives in the <see cref="SheetController"/> the app owns (click/drag select,
/// Excel's keyboard, TSV clipboard — wired by the surface). v1 fences: vertical virtualization
/// only (columns materialize — sheets in the tens of columns; wide-sheet 2D scroll joins later);
/// in-cell EDITING is the next slice.
/// </para>
/// </summary>
public sealed class Spreadsheet : StatefulComponent
{
    private float _offset;
    private float _viewport;
    private int _first;
    private int _last = -1;

    public Spreadsheet(SheetController controller)
    {
        Controller = controller;
    }

    public SheetController Controller { get; private set; }

    public SizeValue Width { get; init; }
    public SizeValue Height { get; init; }

    /// <summary>Rows built past each viewport edge, so a scroll shows cells, not blank.</summary>
    public int Overscan { get; init; } = 3;

    public const float HeaderWidth = 44f;
    public const float HeaderHeight = 26f;

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
        if (next is Spreadsheet fresh) Controller = fresh.Controller;
    }

    /// <summary>The reconciliation key of a row's nodes: its number, unique among the siblings of
    /// the window and the same whichever window holds it.</summary>
    private static string RowKey(int row) => row.ToString();

    /// <summary>Column name the way every spreadsheet spells it: A..Z, AA..</summary>
    public static string ColumnName(int col)
    {
        var name = "";
        while (true)
        {
            name = (char)('A' + col % 26) + name;
            col = col / 26 - 1;
            if (col < 0) break;
        }
        return name;
    }

    private (int First, int Last, float Top) WindowFor(float offset, float viewport)
    {
        var document = Controller.Document;
        var row = 0;
        var top = 0f;
        while (row < document.Rows - 1 && top + document.RowHeight(row) <= offset)
        {
            top += document.RowHeight(row);
            row++;
        }
        var first = Math.Max(0, row - Overscan);
        var firstTop = top;
        for (var r = row - 1; r >= first; r--) firstTop -= document.RowHeight(r);

        var last = row;
        var covered = top - offset;
        while (last < document.Rows - 1 && covered < viewport)
        {
            covered += document.RowHeight(last);
            last++;
        }
        last = Math.Min(document.Rows - 1, last + Overscan);
        return (first, last, firstTop);
    }

    private void OnScrolled(float offset)
    {
        _offset = offset;
        var (first, last, _) = WindowFor(offset, _viewport > 0 ? _viewport : HeaderHeight * 14);
        if (first == _first && last == _last) return;
        SetState(() => { });
    }

    private void OnViewportChanged(float viewport)
    {
        if (Math.Abs(viewport - _viewport) < 0.5f) return;
        SetState(() => _viewport = viewport);
    }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var document = Controller.Document;
        float topOfWindow;
        (_first, _last, topOfWindow) = WindowFor(_offset, _viewport > 0 ? _viewport : HeaderHeight * 14);

        // ---- fixed column headers (the corner + A..Z) --------------------------------------------
        var headerRow = new Row(gap: 0) { Width = SizeValue.Fill };
        headerRow.Add(HeaderCell("", HeaderWidth, HeaderHeight, theme,
            onPressed: () => SetState(Controller.SelectAll)));
        for (var c = 0; c < document.Cols; c++)
            headerRow.Add(ColumnHeader(c, document, theme));

        // ---- the scrolling half: row headers + the surface, INSIDE the scroll --------------------
        // Keyed too: the spacer standing in for the rows above appears the moment the window leaves
        // row 0, and would push this row from the first position to the second — a keyed row inside
        // a shifted parent is a moved row all the same.
        var windowRows = new Row(gap: 0) { Key = "window" };
        var rowHeaders = new Column(gap: 0);
        var grid = new Column(gap: 0);
        for (var r = _first; r <= _last; r++)
        {
            rowHeaders.Add(RowHeader(r, document, theme));
            grid.Add(GridRow(r, document, theme));
        }
        windowRows.Add(rowHeaders);
        windowRows.Add(new SheetSurface(grid, Controller)
        {
            FirstRow = _first,
            OnChanged = () => SetState(() => { }),
            Label = SdkStrings.Spreadsheet,
        });

        var content = new Column(gap: 0) { Width = SizeValue.Fill };
        if (topOfWindow > 0) content.Add(Spacer.Fixed(topOfWindow));
        content.Add(windowRows);
        var below = 0f;
        for (var r = _last + 1; r < document.Rows; r++) below += document.RowHeight(r);
        if (below > 0) content.Add(Spacer.Fixed(below));

        var frame = new Column(gap: 0) { Width = Width, Height = Height };
        frame.Add(headerRow);
        frame.Add(new Flexible(new ScrollView(content)
        {
            OnScrolled = OnScrolled,
            OnViewportChanged = OnViewportChanged,
        }, 1));

        return new Box(new BoxStyle
        {
            Width = Width,
            Height = Height,
            BorderColor = theme.Border,
            BorderWidth = 1f,
            Clip = true,
        }, frame);
    }

    private static VisualNode HeaderCell(string label, float width, float height, IAppTheme theme,
        bool selected = false, Action? onPressed = null)
    {
        // A header inside the selection's band shades, exactly as Excel marks it.
        var cell = new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(width),
            Height = SizeValue.Fixed(height),
            Background = selected ? theme.SurfaceHighlight : theme.SurfaceSubtle,
            BorderColor = theme.Border,
            BorderWidth = 0.5f,
        }, new Text(label, TypeRole.Caption, selected ? theme.TextPrimary : theme.TextMuted,
            maxLines: 1).Centered());
        return onPressed is null ? cell : new Pressable(cell, onPressed);
    }

    // ---- resize grips: an invisible Draggable riding each header's trailing edge ----------------
    // The grip clamps in the COMPONENT (not via the node's Min/Max — those would rebuild mid-drag),
    // previews straight into the document every move, and the release replays the whole gesture as
    // ONE undoable Controller.Resize.

    private VisualNode ColumnHeader(int c, SheetDocument document, IAppTheme theme)
    {
        var col = c;   // the loop variable would be shared by every closure
        var selection = Controller.Selection;
        var inBand = c >= selection.LeftCol && c <= selection.RightCol;
        var stack = new Stack();
        stack.Add(HeaderCell(ColumnName(c), document.ColWidth(c), HeaderHeight, theme,
            selected: inBand, onPressed: () => SetState(() => Controller.SelectCols(col, col))));
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

    private VisualNode RowHeader(int r, SheetDocument document, IAppTheme theme)
    {
        var row = r;
        var selection = Controller.Selection;
        var inBand = r >= selection.TopRow && r <= selection.BottomRow;
        // Keyed by ROW: the window slides, and a header's identity must not slide with it — focus
        // and hover are remembered by path on Photon and by key on the web, and both realizers read
        // this one Key. Without it, Tab down the headers restarted from the first control the
        // moment the window shifted past the rows it began with.
        var stack = new Stack { Key = RowKey(r) };
        stack.Add(HeaderCell($"{r + 1}", HeaderWidth, document.RowHeight(r), theme,
            selected: inBand, onPressed: () => SetState(() => Controller.SelectRows(row, row))));
        stack.Add(new Positioned(new Draggable(new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(HeaderWidth),
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

    private VisualNode GridRow(int row, SheetDocument document, IAppTheme theme)
    {
        // Selection, the active ring, the editing draft, the FILL HANDLE and the fill preview all
        // paint ON THE CELLS — write-once, so both targets are identical by construction.
        var selection = Controller.Selection;
        var active = Controller.ActiveCell;
        var fillTarget = Controller.FillTarget;
        var line = new Row(gap: 0) { Key = RowKey(row) };
        for (var c = 0; c < document.Cols; c++)
        {
            var cell = new CellRef(row, c);
            var isActive = cell == active;
            var selected = selection.Contains(cell) && !selection.IsSingleCell;
            var inFillPreview = fillTarget is { } preview && preview.Contains(cell);
            var editingHere = isActive && Controller.Editing;
            var value = editingHere ? Controller.Draft : document.GetCell(cell);

            VisualNode? content = null;
            if (editingHere)
            {
                var draftLine = new Row(gap: 0) { Cross = CrossAlign.Center };
                if (value.Length > 0)
                    draftLine.Add(new Text(value, TypeRole.BodyM, theme.TextPrimary, maxLines: 1));
                // The caret at the draft's end — a thin bar, steady (the blink is the host's).
                draftLine.Add(new Box(new BoxStyle
                {
                    Width = SizeValue.Fixed(2),
                    Height = SizeValue.Fixed(document.RowHeight(row) - 8),
                    Background = theme.TextPrimary,
                }));
                content = draftLine;
            }
            else if (value.Length > 0)
            {
                content = new Text(value, TypeRole.BodyM, theme.TextPrimary, maxLines: 1);
            }

            var box = new Box(new BoxStyle
            {
                Width = SizeValue.Fixed(document.ColWidth(c)),
                Height = SizeValue.Fixed(document.RowHeight(row)),
                Background = selected ? theme.SurfaceHighlight : theme.Surface,
                BorderColor = isActive || inFillPreview ? theme.FocusRing : theme.Border,
                BorderWidth = isActive ? 2f : inFillPreview ? 1.5f : 0.5f,
                Padding = new EdgeInsets(0, 6, 0, 6),
            }, content);

            // Excel's little square: the fill handle rides the selection's bottom-right corner.
            // Grabbing and dragging it is the surface's gesture; this is only the pixels + cursor.
            if (!editingHere && row == selection.BottomRow && c == selection.RightCol)
            {
                var handled = new Stack();
                handled.Add(box);
                handled.Add(new Positioned(new Box(new BoxStyle
                {
                    Width = SizeValue.Fixed(FillHandle),
                    Height = SizeValue.Fixed(FillHandle),
                    Background = theme.FocusRing,
                    BorderColor = theme.Surface,
                    BorderWidth = 1f,
                    Cursor = PointerCursor.Crosshair,
                }), bottom: -1, end: -1) { Layer = 2 });
                line.Add(handled);
                continue;
            }
            line.Add(box);
        }
        return line;
    }
}
