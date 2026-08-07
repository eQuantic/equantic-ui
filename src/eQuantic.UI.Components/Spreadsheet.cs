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

    public override void AdoptConfig(UiComponent next)
    {
        if (next is Spreadsheet fresh) Controller = fresh.Controller;
    }

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
        headerRow.Add(HeaderCell("", HeaderWidth, HeaderHeight, theme));
        for (var c = 0; c < document.Cols; c++)
            headerRow.Add(HeaderCell(ColumnName(c), document.ColWidth(c), HeaderHeight, theme));

        // ---- the scrolling half: row headers + the surface, INSIDE the scroll --------------------
        var windowRows = new Row(gap: 0);
        var rowHeaders = new Column(gap: 0);
        var grid = new Column(gap: 0);
        for (var r = _first; r <= _last; r++)
        {
            rowHeaders.Add(HeaderCell($"{r + 1}", HeaderWidth, document.RowHeight(r), theme));
            grid.Add(GridRow(r, document, theme));
        }
        windowRows.Add(rowHeaders);
        windowRows.Add(new SheetSurface(grid, Controller)
        {
            FirstRow = _first,
            OnChanged = () => SetState(() => { }),
            Label = "Spreadsheet",
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

    private static VisualNode HeaderCell(string label, float width, float height, IAppTheme theme) =>
        new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(width),
            Height = SizeValue.Fixed(height),
            Background = theme.SurfaceSubtle,
            BorderColor = theme.Border,
            BorderWidth = 0.5f,
        }, new Text(label, TypeRole.Caption, theme.TextMuted, maxLines: 1).Centered());

    private VisualNode GridRow(int row, SheetDocument document, IAppTheme theme)
    {
        var line = new Row(gap: 0);
        for (var c = 0; c < document.Cols; c++)
        {
            var value = document.GetCell(new CellRef(row, c));
            line.Add(new Box(new BoxStyle
            {
                Width = SizeValue.Fixed(document.ColWidth(c)),
                Height = SizeValue.Fixed(document.RowHeight(row)),
                Background = theme.Surface,
                BorderColor = theme.Border,
                BorderWidth = 0.5f,
                Padding = new EdgeInsets(0, 6, 0, 6),
            }, value.Length > 0
                ? new Text(value, TypeRole.BodyM, theme.TextPrimary, maxLines: 1)
                : null));
        }
        return line;
    }
}
