using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A table that builds only the cells in view, as Flutter's <c>TableView</c>: the extents of its rows
/// and columns, a builder that answers the cell at a row and a column, and the leading rows and columns
/// it keeps pinned. It hands a <see cref="TableViewport"/> the cells of the window the viewport reports
/// plus an <see cref="Overscan"/> on each side, so a grid of 1 048 576 rows by 16 384 columns costs
/// what a screenful costs, on every target.
/// <para>
/// The window has hysteresis: a scroll that keeps the visible cells inside the ones already built
/// builds nothing, and the viewport moves those cells by itself; the table builds again only when the
/// visible rows or columns leave them. A cell is keyed by its position, so one that stays in the window
/// keeps its element. The scroll is the table's own (<see cref="TableScroll"/>), kept across rebuilds.
/// </para>
/// <para>
/// A mark (<see cref="Marks"/>) is drawn above the cells over the range it covers: one that crosses the
/// edge of a pinned band is handed to the viewport as one piece per band, so it shows in each band it
/// reaches and hides where it passes under one.
/// </para>
/// </summary>
public sealed class TableView : StatefulComponent
{
    /// <summary>What a table builds for before its viewport has said how large it is: a common window,
    /// rather than nothing, so the first frame shows cells.</summary>
    private static readonly Size FirstGuess = new(1024, 768);

    /// <summary>The scroll the table keeps when the configuration hands it none.</summary>
    private readonly TableScroll _ownScroll = new();

    /// <summary>The configuration of the latest build: a parent that builds again hands a new one, and
    /// everything is read from it, its init-only settings included.</summary>
    private TableView _config;

    private Size _viewport;
    private int _firstRow;
    private int _lastRow = -1;
    private int _firstColumn;
    private int _lastColumn = -1;

    public TableView(TableSpans rows, TableSpans columns, Func<int, int, VisualNode?> cellBuilder)
    {
        Rows = rows;
        Columns = columns;
        CellBuilder = cellBuilder;
        _config = this;
    }

    /// <summary>The extents of the rows.</summary>
    public TableSpans Rows { get; }

    /// <summary>The extents of the columns.</summary>
    public TableSpans Columns { get; }

    /// <summary>The cell at a row and a column, or nothing for an empty one.</summary>
    public Func<int, int, VisualNode?> CellBuilder { get; }

    /// <summary>How many leading rows stay at the top while the others scroll.</summary>
    public int PinnedRows { get; init; }

    /// <summary>How many leading columns stay at the leading edge while the others scroll.</summary>
    public int PinnedColumns { get; init; }

    /// <summary>Rows and columns built past each edge of the window, so a scroll shows cells, not blank.</summary>
    public int Overscan { get; init; } = 3;

    public SizeValue Width { get; init; }
    public SizeValue Height { get; init; }

    /// <summary>The marks to draw above the cells, each a <see cref="TableViewCell"/> over its range.</summary>
    public IReadOnlyList<TableViewCell>? Marks { get; init; }

    /// <summary>A scroll to an offset, or a range to reveal, honoured once per version.</summary>
    public TableScrollRequest? Request { get; init; }

    /// <summary>Where the grid stands under the window, each time that changes.</summary>
    public Action<TableOffset>? OnScrolled { get; init; }

    /// <summary>The size of the part of the window the cells scroll in, each time that changes.</summary>
    public Action<Size>? OnViewportChanged { get; init; }

    /// <summary>Accessible name.</summary>
    public string? Label { get; init; }

    /// <summary>
    /// The scroll to keep the grid's offset in, when what composes the table must read it between builds
    /// (a scroll step inside the cells already built builds nothing): a spreadsheet's engine reads a press
    /// against it. Null keeps one the table owns. <em>How does Flutter solve it?</em> The
    /// <c>ScrollController</c> a scrollable is handed, kept by its owner across rebuilds.
    /// </summary>
    public TableScroll? Scroll { get; init; }

    /// <summary>Whether the table is a stop of the Tab order itself and scrolls by the keys
    /// (<see cref="TableViewport.CanRequestFocus"/>): false leaves the keyboard to what composes it.</summary>
    public bool CanRequestFocus { get; init; } = true;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is not TableView fresh) return;
        // Other extents, bands or margin make the window built for the old ones mean nothing: it is
        // chosen again, or a grid that shrank would be asked for rows it no longer has.
        if (fresh.Rows != _config.Rows || fresh.Columns != _config.Columns
            || fresh.PinnedRows != _config.PinnedRows || fresh.PinnedColumns != _config.PinnedColumns
            || fresh.Overscan != _config.Overscan)
        {
            _lastRow = -1;
            _lastColumn = -1;
        }
        _config = fresh;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var config = _config;
        var rows = config.Rows;
        var columns = config.Columns;
        var pinnedRows = Math.Clamp(config.PinnedRows, 0, rows.Count);
        var pinnedColumns = Math.Clamp(config.PinnedColumns, 0, columns.Count);
        var scroll = config.Scroll ?? _ownScroll;
        // An axis can shrink IN PLACE, the same object answering fewer rows (a spreadsheet's document
        // after a delete), which AdoptConfig cannot see: the window built before it then reaches past
        // the end, and is chosen again rather than asking the builder for rows the grid no longer has.
        if (!Covers(scroll.Offset) || _lastRow >= rows.Count || _lastColumn >= columns.Count) Window(scroll.Offset);

        var viewport = new TableViewport(rows, columns, scroll)
        {
            PinnedRows = pinnedRows,
            PinnedColumns = pinnedColumns,
            Width = config.Width,
            Height = config.Height,
            Request = config.Request,
            OnScrolled = Scrolled,
            OnViewportChanged = ViewportChanged,
            Label = config.Label,
            CanRequestFocus = config.CanRequestFocus,
        };

        // The corner, the pinned rows over the columns in the window, the pinned columns down the rows
        // in the window, and the window itself.
        for (var row = 0; row < pinnedRows; row++)
        {
            for (var column = 0; column < pinnedColumns; column++) Place(viewport, row, column);
            for (var column = _firstColumn; column <= _lastColumn; column++) Place(viewport, row, column);
        }
        for (var row = _firstRow; row <= _lastRow; row++)
        {
            for (var column = 0; column < pinnedColumns; column++) Place(viewport, row, column);
            for (var column = _firstColumn; column <= _lastColumn; column++) Place(viewport, row, column);
        }

        if (config.Marks is { } marks)
            for (var index = 0; index < marks.Count; index++)
                AddMark(viewport, marks[index], index, pinnedRows, pinnedColumns);
        return viewport;
    }

    private void Place(TableViewport viewport, int row, int column)
    {
        if (_config.CellBuilder(row, column) is { } child) viewport.Add(new TableViewCell(row, column, child));
    }

    /// <summary>A mark, one piece per band it reaches: the viewport draws a cell in the band of its first
    /// row and column, so a range that crosses a band's edge is cut there. Each piece is keyed by the
    /// mark's own key, or its place in <see cref="Marks"/> when it has none, and its own, so it keeps its
    /// element while the window, and with it the number of cells before it, changes; a keyed mark keeps it
    /// however many marks come before it, which is what lets it glide (<see cref="TableViewCell.Glide"/>).</summary>
    private static void AddMark(TableViewport viewport, TableViewCell mark, int index, int pinnedRows,
        int pinnedColumns)
    {
        var lastRow = mark.Row + Math.Max(1, mark.Rows);
        var lastColumn = mark.Column + Math.Max(1, mark.Columns);
        var piece = 0;
        foreach (var (rowFrom, rowTo) in Cut(mark.Row, lastRow, pinnedRows))
        {
            foreach (var (columnFrom, columnTo) in Cut(mark.Column, lastColumn, pinnedColumns))
            {
                viewport.Add(new TableViewCell(rowFrom, columnFrom, mark.Child, mark: true)
                {
                    Rows = rowTo - rowFrom,
                    Columns = columnTo - columnFrom,
                    Key = (mark.Key ?? "mark:" + index) + "/" + piece,
                    Glide = mark.Glide,
                });
                piece++;
            }
        }
    }

    /// <summary>A run from <paramref name="from"/> to before <paramref name="to"/>, cut where the pinned
    /// band ends.</summary>
    private static List<(int From, int To)> Cut(int from, int to, int pinned)
    {
        var pieces = new List<(int From, int To)>();
        if (from < pinned) pieces.Add((from, Math.Min(to, pinned)));
        if (to > pinned) pieces.Add((Math.Max(from, pinned), to));
        return pieces;
    }

    /// <summary>Whether the cells already built hold every row and column the window shows at
    /// <paramref name="offset"/>.</summary>
    private bool Covers(TableOffset offset)
    {
        if (_lastRow < _firstRow && _lastColumn < _firstColumn) return false;
        var (firstRow, lastRow, firstColumn, lastColumn) = Visible(offset);
        return firstRow >= _firstRow && lastRow <= _lastRow && firstColumn >= _firstColumn && lastColumn <= _lastColumn;
    }

    /// <summary>The window to build: the visible rows and columns, and the overscan past each edge,
    /// held inside the part of the grid that scrolls.</summary>
    private void Window(TableOffset offset)
    {
        var (firstRow, lastRow, firstColumn, lastColumn) = Visible(offset);
        var overscan = Math.Max(0, _config.Overscan);
        var pinnedRows = Math.Clamp(_config.PinnedRows, 0, _config.Rows.Count);
        var pinnedColumns = Math.Clamp(_config.PinnedColumns, 0, _config.Columns.Count);
        _firstRow = Math.Max(pinnedRows, firstRow - overscan);
        _lastRow = Math.Min(_config.Rows.Count - 1, lastRow + overscan);
        _firstColumn = Math.Max(pinnedColumns, firstColumn - overscan);
        _lastColumn = Math.Min(_config.Columns.Count - 1, lastColumn + overscan);
    }

    /// <summary>The first and last row and column of the scrolling part that the window shows at
    /// <paramref name="offset"/>.</summary>
    private (int FirstRow, int LastRow, int FirstColumn, int LastColumn) Visible(TableOffset offset)
    {
        var config = _config;
        var size = _viewport.Width > 0 && _viewport.Height > 0 ? _viewport : FirstGuess;
        var pinnedRows = Math.Clamp(config.PinnedRows, 0, config.Rows.Count);
        var pinnedColumns = Math.Clamp(config.PinnedColumns, 0, config.Columns.Count);
        var pinnedDown = config.Rows.OffsetOf(pinnedRows);
        var pinnedAcross = config.Columns.OffsetOf(pinnedColumns);
        var firstRow = Math.Max(pinnedRows, config.Rows.IndexAt(offset.Down + pinnedDown));
        var lastRow = config.Rows.IndexAt(offset.Down + Math.Max(pinnedDown, size.Height) - 0.5);
        var firstColumn = Math.Max(pinnedColumns, config.Columns.IndexAt(offset.Across + pinnedAcross));
        var lastColumn = config.Columns.IndexAt(offset.Across + Math.Max(pinnedAcross, size.Width) - 0.5);
        return (firstRow, lastRow, firstColumn, lastColumn);
    }

    private void Scrolled(TableOffset offset)
    {
        _config.OnScrolled?.Invoke(offset);
        if (!Covers(offset)) SetState(() => { });
    }

    private void ViewportChanged(Size viewport)
    {
        _config.OnViewportChanged?.Invoke(viewport);
        if (viewport == _viewport) return;
        SetState(() =>
        {
            _viewport = viewport;
            _lastRow = -1;
            _lastColumn = -1;
        });
    }
}
