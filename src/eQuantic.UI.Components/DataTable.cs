using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// One column of a <see cref="DataTable"/>: its heading, how wide it sits, which way its cells read,
/// and whether the heading is a sort control. A column that cannot be sorted must not look like it
/// can, so <see cref="Sortable"/> is opt-in per column rather than a table-wide flag.
/// <para>
/// A column whose <see cref="Track"/> is a width (<see cref="GridTrack.Fixed"/>) is one a person can resize
/// when the table reports it (<see cref="DataTable.OnColumnResized"/>), between <see cref="MinWidth"/> and
/// <see cref="MaxWidth"/>, in dp (#1538). One whose width the layout decides has no width to start a drag
/// from, and takes what the others leave.
/// </para>
/// </summary>
public sealed record DataColumn(
    string Header,
    GridTrack Track,
    TextAlignment Align = TextAlignment.Start,
    bool Sortable = false,
    float MinWidth = 64,
    float MaxWidth = 1024);

/// <summary>
/// One row: RICH cells, because a real table holds an avatar beside a name, an icon beside a method
/// and a status pill — not strings. <see cref="Key"/> is the row's identity for selection; without
/// one, selection would follow the row's POSITION and break the moment the table re-sorts. The cells are
/// in the order the table DECLARES its columns, whatever order it draws them in.
/// </summary>
public sealed record DataRow(string Key, IReadOnlyList<VisualNode> Cells);

/// <summary>Which way a sorted column reads — and <see cref="None"/>, so a table can be unsorted.</summary>
public enum SortDirection : byte
{
    None = 0,
    Ascending = 1,
    Descending = 2,
}

/// <summary>
/// The DENSE data surface (spec B18) — everything <see cref="Table"/> fences off: per-column widths
/// and alignment, sortable headings, row selection, rich cells, hovered rows and pending rows.
/// <see cref="Table"/> stays the right answer for a handful of strings; this is for the screen whose
/// job IS the table.
/// <para>
/// CONTROLLED throughout: the caller owns the sort, the selection, the data, the columns' order and
/// their widths. The table renders what it is told and reports intent — it never sorts an array behind
/// your back, because the rows on screen are usually a page of a much larger set that only the server can
/// order, and it never keeps a moved column or a width the app did not keep.
/// </para>
/// <para>
/// A person reorders the columns by dragging a heading or with the keyboard while
/// <see cref="OnColumnsReordered"/> is set, and resizes a column of known width by its heading's edge while
/// <see cref="OnColumnResized"/> is set (#1538). Every column is named by its position in
/// <see cref="Columns"/>, in <see cref="SortColumn"/> and in both reports alike.
/// </para>
/// <para>
/// Row hover is a declarative <see cref="BoxStyle.Hover"/> diff, not a state callback: a hover that
/// re-rendered the table would make a 200-row grid stutter under the pointer. Pending rows are
/// skeletons in the real column tracks, so nothing shifts when the data lands. The table's own state is
/// where a carried column would land, which it draws.
/// </para>
/// </summary>
public sealed class DataTable : StatefulComponent
{
    /// <summary>How far one arrow press moves a column's edge, in dp.</summary>
    private const float ResizeStep = 16;

    /// <summary>The width of a resize handle, the edge a pointer grabs, in dp.</summary>
    private const float HandleWidth = 8;

    /// <summary>The width of the line a handle shows and of the line a carried column lands on, in dp.</summary>
    private const float EdgeLineWidth = 2;

    /// <summary>The header row's name among the sortables, and the group it is alone in.</summary>
    private const string HeaderListId = "columns";

    public DataTable(IReadOnlyList<DataColumn> columns, IReadOnlyList<DataRow> rows)
    {
        Columns = columns;
        Rows = rows;
        _config = this;
    }

    /// <summary>The configuration the table draws: its own, until a parent's build hands it a fresh one. The
    /// instance is kept across the parent's builds for the carried column it holds, so it keeps the fresh one
    /// whole, Flutter's <c>State.widget</c>, and every setting reads from it.</summary>
    private DataTable _config;

    public override void AdoptConfig(UiComponent next)
    {
        if (next is DataTable fresh) _config = fresh;
    }

    public IReadOnlyList<DataColumn> Columns { get; init; }
    public IReadOnlyList<DataRow> Rows { get; init; }

    /// <summary>Index into <see cref="Columns"/>; -1 = unsorted.</summary>
    public int SortColumn { get; init; } = -1;
    public SortDirection SortDirection { get; init; } = SortDirection.None;

    /// <summary>Reports the heading the user pressed — the CALLER decides what that means.</summary>
    public Action<int>? OnSort { get; init; }

    /// <summary>Keys of the selected rows. Non-null turns the selection column on.</summary>
    public IReadOnlyCollection<string>? Selection { get; init; }
    public Action<DataRow>? OnToggleRow { get; init; }

    /// <summary>Header checkbox: select all, or clear when anything is already selected.</summary>
    public Action? OnToggleAll { get; init; }

    public Action<DataRow>? OnRowPressed { get; init; }

    /// <summary>Rows still in flight, drawn as skeletons in the real tracks. 0 = none.</summary>
    public int PendingRows { get; init; }

    /// <summary>Shown INSTEAD of the body when there is nothing to show and nothing is pending.</summary>
    public VisualNode? Empty { get; init; }

    /// <summary>
    /// The order the columns are drawn in, as positions into <see cref="Columns"/>; null draws them as
    /// declared. Read leniently: a position out of range or already drawn is skipped, and a column the
    /// order leaves out is drawn after the others, so an order kept from before a column was added still
    /// draws every column.
    /// </summary>
    public IReadOnlyList<int>? ColumnOrder { get; init; }

    /// <summary>
    /// The order a person left the columns in, by dragging a heading or with the keyboard, as positions into
    /// <see cref="Columns"/>: what the app keeps and hands back as <see cref="ColumnOrder"/>. Set, the
    /// headings can be moved; never called for a drop that leaves the order as it was.
    /// </summary>
    public Action<IReadOnlyList<int>>? OnColumnsReordered { get; init; }

    /// <summary>
    /// A column's position in <see cref="Columns"/> and the width in dp a person gave it, live while the
    /// pointer drags its edge and once per arrow press: the app keeps it by giving the column back with that
    /// width as its track. Set, every column whose track is <see cref="GridTrack.Fixed"/> has a handle.
    /// </summary>
    public Action<int, float>? OnColumnResized { get; init; }

    /// <summary>The column being carried over the header, by its position in <see cref="Columns"/>; -1 when
    /// none is.</summary>
    private int _carried = -1;

    /// <summary>Where the carried column would land, a position among the other columns; -1 when nowhere
    /// that changes the order.</summary>
    private int _landing = -1;

    private bool Selectable => _config.Selection is not null;

    private bool Reorders => _config.OnColumnsReordered is not null;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var order = DrawnOrder();
        var tracks = Tracks(theme.Table, order);

        var table = new Column(gap: 0) { Width = SizeValue.Fill };
        table.Add(Header(theme, tracks, order));

        if (_config.Rows.Count == 0 && _config.PendingRows == 0 && _config.Empty is { } empty)
        {
            table.Add(empty);
            return table;
        }

        foreach (var row in _config.Rows) table.Add(Body(theme, tracks, order, row));
        for (var i = 0; i < _config.PendingRows; i++) table.Add(Pending(theme, tracks, order, i));

        return table;
    }

    /// <summary>The columns in the order they are drawn, as positions into <see cref="Columns"/>: the
    /// app's <see cref="ColumnOrder"/> read leniently, then every column it left out.</summary>
    private List<int> DrawnOrder()
    {
        var order = new List<int>();
        if (_config.ColumnOrder is { } given)
        {
            foreach (var position in given)
            {
                if (position >= 0 && position < _config.Columns.Count && !order.Contains(position))
                    order.Add(position);
            }
        }
        for (var i = 0; i < _config.Columns.Count; i++)
        {
            if (!order.Contains(i)) order.Add(i);
        }
        return order;
    }

    /// <summary>The column tracks in drawn order, the leading selection column's first when the table is
    /// selectable.</summary>
    private GridTrack[] Tracks(TableTheme? slot, List<int> order)
    {
        var tracks = new GridTrack[order.Count + (Selectable ? 1 : 0)];
        var offset = 0;
        if (Selectable) tracks[offset++] = GridTrack.Fixed(slot?.SelectionColumnWidth ?? 44);
        for (var i = 0; i < order.Count; i++) tracks[offset + i] = _config.Columns[order[i]].Track;
        return tracks;
    }

    // ---- Header ----------------------------------------------------------------------------

    private VisualNode Header(IAppTheme theme, GridTrack[] tracks, List<int> order)
    {
        // The theme's Table slot, then the component's own (§3.10).
        var slot = theme.Table;
        var grid = new Grid(tracks, gap: 0) { Width = SizeValue.Fill };

        if (Selectable)
        {
            var anySelected = _config.Selection!.Count > 0;
            // Some-but-not-all is INDETERMINATE — a full tick would claim more than is true.
            grid.Add(Cell(new Box(new BoxStyle(), new Checkbox(anySelected, _config.OnToggleAll)
            {
                Indeterminate = anySelected && _config.Selection.Count < _config.Rows.Count,
            }), TextAlignment.Center, slot));
        }

        var others = _carried >= 0 ? Without(order, _carried) : order;
        for (var i = 0; i < order.Count; i++)
        {
            var index = order[i];
            var heading = Heading(theme, slot, index);
            var lineAtStart = _landing >= 0 && _landing < others.Count && others[_landing] == index;
            var lineAtEnd = _landing >= 0 && _landing == others.Count && others[others.Count - 1] == index;
            var resizes = Resizes(_config.Columns[index]);
            if (!lineAtStart && !lineAtEnd && !resizes)
            {
                grid.Add(heading);
                continue;
            }

            // The handle and the line lie OVER the heading, beside its sortable item rather than in it, so a
            // press on the handle never picks the column up.
            var cell = new Stack { Width = SizeValue.Fill, Height = SizeValue.Fill };
            cell.Add(heading);
            if (lineAtStart) cell.Add(new Positioned(DropLine(theme, slot), top: 0, bottom: 0, start: 0));
            if (lineAtEnd) cell.Add(new Positioned(DropLine(theme, slot), top: 0, bottom: 0, end: 0));
            if (resizes) cell.Add(new Positioned(ResizeHandle(theme, slot, index), top: 0, bottom: 0, end: 0));
            grid.Add(cell);
        }

        // While the app keeps the order, the header row is a horizontal sortable list of its headings, alone
        // in its group, saying each keyboard step in the SDK's words.
        VisualNode row = !Reorders
            ? grid
            : new Sortable(grid, HeaderListId, Dropped, SdkStrings.Sorting)
            {
                Axis = DragAxis.Horizontal,
                Label = SdkStrings.TableColumns,
                OnHovered = Hovered,
            };

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = slot?.HeaderFill ?? theme.SurfaceSubtle,
            BorderWidth = slot?.BorderWidth ?? 1,
            BorderColor = slot?.BorderColor ?? theme.Border,
        }, row);
    }

    /// <summary>One column's heading: its label, the chevron when it is the sorted one, a press that sorts
    /// when it sorts, and a sortable item when the columns can be moved.</summary>
    private VisualNode Heading(IAppTheme theme, TableTheme? slot, int index)
    {
        var column = _config.Columns[index];
        var sorted = index == _config.SortColumn && _config.SortDirection != SortDirection.None;

        var label = new Row(gap: slot?.HeaderIconGap ?? Space.S1)
        {
            Width = SizeValue.Fill,
            Cross = CrossAlign.Center,
            Main = column.Align == TextAlignment.Start ? MainAlign.Start : MainAlign.End,
        };
        var sortedColor = slot?.SortedHeaderColor ?? theme.TextPrimary;
        label.Add(new Text(slot?.HeaderUppercase ?? true ? column.Header.ToUpperInvariant() : column.Header,
            TypeRole.Caption, sorted ? sortedColor : slot?.DataHeaderColor ?? theme.TextMuted, maxLines: 1));
        if (sorted)
        {
            label.Add(new Icon(
                _config.SortDirection == SortDirection.Ascending ? Icons.ChevronUp : Icons.ChevronDown,
                slot?.SortIconSize ?? IconSize.Sm, sortedColor));
        }

        var onSort = _config.OnSort;
        VisualNode heading = column.Sortable && onSort is not null
            ? new Pressable(Cell(label, column.Align, slot), () => onSort(index))
            {
                Label = SdkStrings.SortByColumn(column.Header),
            }
            : Cell(label, column.Align, slot);
        return Reorders ? new SortableItem(heading, ColumnId(index)) : heading;
    }

    /// <summary>Whether a column has a resize handle: the app keeps widths, and this one's is a number.</summary>
    private bool Resizes(DataColumn column) =>
        _config.OnColumnResized is not null && column.Track.Kind == SizeKind.Fixed && !column.Track.Repeats;

    /// <summary>
    /// A column's resize handle, at its heading's end edge: a separator the arrows step by
    /// <see cref="ResizeStep"/>, around an edge a pointer drags, whose gesture starts at the column's width
    /// and reports it live, kept between the column's bounds.
    /// </summary>
    private VisualNode ResizeHandle(IAppTheme theme, TableTheme? slot, int index)
    {
        var column = _config.Columns[index];
        var min = column.MinWidth;
        var max = Math.Max(min, column.MaxWidth);
        var width = column.Track.Value;
        var color = slot?.ResizeHandleColor ?? theme.FocusRing;
        var resize = _config.OnColumnResized!;

        var edge = new Box(new BoxStyle
        {
            Width = SizeValue.Fixed(HandleWidth),
            Height = SizeValue.Fill,
            Cursor = PointerCursor.ColResize,
            BorderSides = BorderSides.End,
            BorderWidth = 0,
            BorderColor = color,
            Hover = new StyleDiff { BorderWidth = EdgeLineWidth },
            Focus = new StyleDiff { BorderWidth = EdgeLineWidth },
        });

        // The gesture's offset IS the width: it starts at the width the column has and adds the pointer's
        // travel, clamped to the column's bounds. Nothing follows the pointer: the app's width moves the edge.
        var surface = new Draggable(edge)
        {
            Axis = DragAxis.Horizontal,
            Follows = false,
            RestOffset = width,
            Min = min,
            Max = max,
            OnMoved = next => resize(index, next),
        };

        return new Adjustable(surface, direction =>
        {
            var next = Math.Min(max, Math.Max(min, width + direction * ResizeStep));
            if (next != width) resize(index, next);
        })
        {
            Role = AdjustableRole.Separator,
            Axis = DragAxis.Horizontal,
            Label = SdkStrings.ResizeColumn(column.Header),
            Value = new RangeValue(width, min, max),
        };
    }

    /// <summary>The line that says where a carried column lands.</summary>
    private static VisualNode DropLine(IAppTheme theme, TableTheme? slot) => new Box(new BoxStyle
    {
        Width = SizeValue.Fixed(EdgeLineWidth),
        Height = SizeValue.Fill,
        Background = slot?.ColumnDropColor ?? theme.FocusRing,
    });

    /// <summary>A column's name in the header's sortable list: its position in <see cref="Columns"/>.</summary>
    private static string ColumnId(int index) => $"{index}";

    /// <summary>The column a sortable item's id names, by its position in <see cref="Columns"/>; -1 for none.</summary>
    private int ColumnOf(string id)
    {
        for (var i = 0; i < _config.Columns.Count; i++)
        {
            if (ColumnId(i) == id) return i;
        }
        return -1;
    }

    /// <summary>The order without one column.</summary>
    private static List<int> Without(List<int> order, int column)
    {
        var others = new List<int>();
        foreach (var index in order)
        {
            if (index != column) others.Add(index);
        }
        return others;
    }

    /// <summary>The order a drop leaves: the column taken out and inserted at the drop's index among the
    /// others, the rule <see cref="SortDrop.Index"/> is given in.</summary>
    private static List<int> Reordered(List<int> order, int column, int at)
    {
        var next = Without(order, column);
        next.Insert(Math.Min(next.Count, Math.Max(0, at)), column);
        return next;
    }

    private static bool SameOrder(List<int> a, List<int> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    /// <summary>Where a carried column would land, drawn; nothing for a place that keeps the order.</summary>
    private void Hovered(SortDrop? drop)
    {
        var carried = drop is null ? -1 : ColumnOf(drop.Item);
        var landing = -1;
        if (drop is not null && carried >= 0)
        {
            var order = DrawnOrder();
            // Among the others, which are one fewer than the columns: the last place is after all of them.
            if (!SameOrder(Reordered(order, carried, drop.Index), order))
                landing = Math.Min(order.Count - 1, Math.Max(0, drop.Index));
        }
        if (carried == _carried && landing == _landing) return;
        SetState(() =>
        {
            _carried = carried;
            _landing = landing;
        });
    }

    /// <summary>A heading dropped: the order it leaves goes to the app, unless it is the order it was.</summary>
    private void Dropped(SortDrop drop)
    {
        var column = ColumnOf(drop.Item);
        if (column < 0) return;
        var order = DrawnOrder();
        var next = Reordered(order, column, drop.Index);
        if (!SameOrder(next, order)) _config.OnColumnsReordered?.Invoke(next);
    }

    // ---- Body ------------------------------------------------------------------------------

    private VisualNode Body(IAppTheme theme, GridTrack[] tracks, List<int> order, DataRow row)
    {
        var selected = _config.Selection?.Contains(row.Key) == true;
        var slot = theme.Table;
        var grid = new Grid(tracks, gap: 0) { Width = SizeValue.Fill };

        if (Selectable)
        {
            var toggle = _config.OnToggleRow;
            grid.Add(Cell(new Box(new BoxStyle(),
                new Checkbox(selected, () => toggle?.Invoke(row))
                {
                    Disabled = toggle is null,
                }), TextAlignment.Center, slot));
        }

        // Each cell under its column, read through the drawn order: a row's cells are in declared order. A
        // row short of a cell keeps an empty one in its place, so the cells after it stay under their columns.
        foreach (var index in order)
        {
            grid.Add(Cell(index < row.Cells.Count ? row.Cells[index] : new Box(new BoxStyle()),
                _config.Columns[index].Align, slot));
        }

        var box = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            MinHeight = slot?.DataRowHeight ?? 52,
            Background = selected ? theme.Colors(Variant.Primary).Subtle : null,
            BorderWidth = slot?.BorderWidth ?? 1,
            BorderColor = slot?.BorderColor ?? theme.Border,
            // Declarative, so hovering a row never re-renders the table.
            Hover = new StyleDiff { Background = slot?.HoverFill ?? theme.SurfaceSubtle },
            Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
        }, grid)
        {
            Key = row.Key,
        };

        var onRowPressed = _config.OnRowPressed;
        return onRowPressed is null
            ? box
            : new Pressable(box, () => onRowPressed(row));
    }

    /// <summary>
    /// A row still loading, laid out in the REAL tracks: the table's shape is already known, so the
    /// data landing must not move anything. Widths alternate so the placeholder reads as text rather
    /// than as a progress bar.
    /// </summary>
    private VisualNode Pending(IAppTheme theme, GridTrack[] tracks, List<int> order, int index)
    {
        var slot = theme.Table;
        var grid = new Grid(tracks, gap: 0) { Width = SizeValue.Fill };
        if (Selectable)
            grid.Add(Cell(new Box(new BoxStyle(), new Skeleton(SkeletonShape.Block, 16, 16)),
                TextAlignment.Center, slot));

        for (var i = 0; i < order.Count; i++)
        {
            grid.Add(Cell(new Box(new BoxStyle(), new Skeleton(SkeletonShape.Line, i % 2 == 0 ? 96 : 64)),
                _config.Columns[order[i]].Align, slot));
        }

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            MinHeight = slot?.DataRowHeight ?? 52,
            BorderWidth = slot?.BorderWidth ?? 1,
            BorderColor = slot?.BorderColor ?? theme.Border,
        }, grid)
        {
            Key = $"pending-{index}",
        };
    }

    /// <summary>Every cell gets the same inset, the theme's Table slot's, and the same alignment
    /// discipline.</summary>
    private static VisualNode Cell(VisualNode child, TextAlignment align, TableTheme? slot)
    {
        var row = new Row(gap: 0)
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Cross = CrossAlign.Center,
            Main = align switch
            {
                TextAlignment.Center => MainAlign.Center,
                TextAlignment.End => MainAlign.End,
                _ => MainAlign.Start,
            },
        };
        row.Add(child);

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = EdgeInsets.Symmetric(slot?.CellPaddingX ?? Space.S3, slot?.CellPaddingY ?? Space.S2),
        }, row);
    }
}
