namespace eQuantic.UI.Primitives;

/// <summary>
/// A window onto a grid of rows and columns larger than any screen, as Flutter's <c>TableViewport</c>
/// is: it places the <see cref="Cells"/> it is handed by row and column, from the extents of its
/// <see cref="Rows"/> and <see cref="Columns"/>, holds the first <see cref="PinnedRows"/> rows and
/// <see cref="PinnedColumns"/> columns still on the leading edges while the rest scroll, and draws a
/// scrollbar on each axis the grid overflows.
/// <para>
/// It scrolls on both axes and the offset is its own on every target, kept by the
/// <see cref="Scroll"/> model it carries: a browser lays an element out at most 16 777 214 points tall,
/// which a million rows pass, so the browser's own scrolling is not used. It builds nothing: which cells
/// it is handed is the business of the component above it (<c>TableView</c> hands it the cells in view),
/// which it tells where it stands (<see cref="OnScrolled"/>) and how large its window is
/// (<see cref="OnViewportChanged"/>).
/// </para>
/// <para>
/// Asked to fill an axis that has no bound, as a table in a scrolling page is, it takes the visible
/// extent of the scroller around it on that axis, and a development log says so.
/// </para>
/// </summary>
public sealed class TableViewport : VisualNode
{
    public sealed override string NodeKind => "tableViewport";

    public TableViewport(TableSpans rows, TableSpans columns, ITableScrollModel scroll)
    {
        Rows = rows;
        Columns = columns;
        Scroll = scroll;
    }

    /// <summary>The extents of the grid's rows.</summary>
    public TableSpans Rows { get; }

    /// <summary>The extents of the grid's columns.</summary>
    public TableSpans Columns { get; }

    /// <summary>The scroll, which a component owns and keeps across the rebuilds of this node.</summary>
    public ITableScrollModel Scroll { get; }

    /// <summary>How many leading rows stay at the top while the others scroll.</summary>
    public int PinnedRows { get; init; }

    /// <summary>How many leading columns stay at the leading edge while the others scroll.</summary>
    public int PinnedColumns { get; init; }

    public SizeValue Width { get; init; }
    public SizeValue Height { get; init; }

    /// <summary>A scroll to an offset, or a range to reveal, honoured once per version.</summary>
    public TableScrollRequest? Request { get; init; }

    /// <summary>Where the grid stands under the window, each time that changes.</summary>
    public Action<TableOffset>? OnScrolled { get; init; }

    /// <summary>The size of the part of the window the cells scroll in, each time that changes.</summary>
    public Action<Size>? OnViewportChanged { get; init; }

    /// <summary>Accessible name.</summary>
    public string? Label { get; init; }

    /// <summary>
    /// What the viewport is to a reader (#1081): a group of its cells, as it was, or a table or a grid
    /// read as rows of cells, each with its row and column in the whole grid and the viewport with the
    /// whole grid's count of each, so a reader knows where it is in a million rows of which forty are
    /// built. What each cell is, <see cref="TableViewCell.Role"/> says. Each row it holds a cell of holds
    /// one at every column the others do, an empty one included: a browser places a cell by its order
    /// among its row's cells, so a cell left out moves every later one onto the column before
    /// (<see cref="TableCellRole"/>), and <c>TableView</c> holds an empty cell for that reason.
    /// </summary>
    public TableRole Role { get; init; }

    /// <summary>
    /// Whether the viewport is a stop of the Tab order itself and takes the keys that scroll it (the
    /// arrows, the pages, the ends). False leaves the keyboard to the component that composes it, as a
    /// spreadsheet keeps it for its own cells (Flutter's <c>canRequestFocus</c>); the controls in its
    /// cells stay stops of the Tab order either way.
    /// </summary>
    public bool CanRequestFocus { get; init; } = true;

    /// <summary>The cells to place, and the marks to draw above them.</summary>
    public List<TableViewCell> Cells { get; } = new();

    public void Add(TableViewCell cell) => Cells.Add(cell);

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
