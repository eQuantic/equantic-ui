using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// One column of a <see cref="DataTable"/>: its heading, how wide it sits, which way its cells read,
/// and whether the heading is a sort control. A column that cannot be sorted must not look like it
/// can, so <see cref="Sortable"/> is opt-in per column rather than a table-wide flag.
/// </summary>
public sealed record DataColumn(
    string Header,
    GridTrack Track,
    TextAlignment Align = TextAlignment.Start,
    bool Sortable = false);

/// <summary>
/// One row: RICH cells, because a real table holds an avatar beside a name, an icon beside a method
/// and a status pill — not strings. <see cref="Key"/> is the row's identity for selection; without
/// one, selection would follow the row's POSITION and break the moment the table re-sorts.
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
/// CONTROLLED throughout: the caller owns the sort, the selection and the data. The table renders
/// what it is told and reports intent — it never sorts an array behind your back, because the rows
/// on screen are usually a page of a much larger set that only the server can order.
/// </para>
/// <para>
/// Row hover is a declarative <see cref="BoxStyle.Hover"/> diff, not a state callback: a hover that
/// re-rendered the table would make a 200-row grid stutter under the pointer. Pending rows are
/// skeletons in the real column tracks, so nothing shifts when the data lands.
/// </para>
/// </summary>
public sealed class DataTable : StatelessComponent
{
    /// <summary>Body row height — dense enough for a pointer, still a comfortable read.</summary>
    public const float RowHeight = 52;

    /// <summary>The leading selection column, when the table is selectable.</summary>
    private const float CheckboxTrack = 44;

    public DataTable(IReadOnlyList<DataColumn> columns, IReadOnlyList<DataRow> rows)
    {
        Columns = columns;
        Rows = rows;
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

    private bool Selectable => Selection is not null;

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var tracks = Tracks();

        var table = new Column(gap: 0) { Width = SizeValue.Fill };
        table.Add(Header(theme, tracks));

        if (Rows.Count == 0 && PendingRows == 0 && Empty is { } empty)
        {
            table.Add(empty);
            return table;
        }

        foreach (var row in Rows) table.Add(Body(theme, tracks, row));
        for (var i = 0; i < PendingRows; i++) table.Add(Pending(theme, tracks, i));

        return table;
    }

    private GridTrack[] Tracks()
    {
        var tracks = new GridTrack[Columns.Count + (Selectable ? 1 : 0)];
        var offset = 0;
        if (Selectable) tracks[offset++] = GridTrack.Fixed(CheckboxTrack);
        for (var i = 0; i < Columns.Count; i++) tracks[offset + i] = Columns[i].Track;
        return tracks;
    }

    // ---- Header ----------------------------------------------------------------------------

    private VisualNode Header(IAppTheme theme, GridTrack[] tracks)
    {
        var grid = new Grid(tracks, gap: 0) { Width = SizeValue.Fill };

        if (Selectable)
        {
            var anySelected = Selection!.Count > 0;
            // Some-but-not-all is INDETERMINATE — a full tick would claim more than is true.
            grid.Add(Cell(new Box(new BoxStyle(), new Checkbox(anySelected, OnToggleAll)
            {
                Indeterminate = anySelected && Selection.Count < Rows.Count,
            }), TextAlignment.Center));
        }

        for (var i = 0; i < Columns.Count; i++)
        {
            var column = Columns[i];
            var sorted = i == SortColumn && SortDirection != SortDirection.None;

            var label = new Row(gap: Space.S1)
            {
                Width = SizeValue.Fill,
                Cross = CrossAlign.Center,
                Main = column.Align == TextAlignment.Start ? MainAlign.Start : MainAlign.End,
            };
            label.Add(new Text(column.Header.ToUpperInvariant(), TypeRole.Caption,
                sorted ? theme.TextPrimary : theme.TextMuted, maxLines: 1));
            if (sorted)
            {
                label.Add(new Icon(
                    SortDirection == SortDirection.Ascending ? Icons.ChevronUp : Icons.ChevronDown,
                    IconSize.Sm, theme.TextPrimary));
            }

            var index = i;
            grid.Add(column.Sortable && OnSort is not null
                ? new Pressable(Cell(label, column.Align), () => OnSort(index))
                {
                    Label = $"Sort by {column.Header}",
                }
                : Cell(label, column.Align));
        }

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Background = theme.SurfaceSubtle,
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, grid);
    }

    // ---- Body ------------------------------------------------------------------------------

    private VisualNode Body(IAppTheme theme, GridTrack[] tracks, DataRow row)
    {
        var selected = Selection is { } selection && selection.Contains(row.Key);
        var grid = new Grid(tracks, gap: 0) { Width = SizeValue.Fill };

        if (Selectable)
        {
            var toggle = OnToggleRow;
            grid.Add(Cell(new Box(new BoxStyle(),
                new Checkbox(selected, () => toggle?.Invoke(row))
                {
                    Disabled = toggle is null,
                }), TextAlignment.Center));
        }

        for (var i = 0; i < Columns.Count && i < row.Cells.Count; i++)
            grid.Add(Cell(row.Cells[i], Columns[i].Align));

        var box = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            MinHeight = RowHeight,
            Background = selected ? theme.Colors(Variant.Primary).Subtle : null,
            BorderWidth = 1,
            BorderColor = theme.Border,
            // Declarative, so hovering a row never re-renders the table.
            Hover = new StyleDiff { Background = theme.SurfaceSubtle },
            Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
        }, grid)
        {
            Key = row.Key,
        };

        return OnRowPressed is null
            ? box
            : new Pressable(box, () => OnRowPressed(row));
    }

    /// <summary>
    /// A row still loading, laid out in the REAL tracks: the table's shape is already known, so the
    /// data landing must not move anything. Widths alternate so the placeholder reads as text rather
    /// than as a progress bar.
    /// </summary>
    private VisualNode Pending(IAppTheme theme, GridTrack[] tracks, int index)
    {
        var grid = new Grid(tracks, gap: 0) { Width = SizeValue.Fill };
        if (Selectable)
            grid.Add(Cell(new Box(new BoxStyle(), new Skeleton(SkeletonShape.Block, 16, 16)),
                TextAlignment.Center));

        for (var i = 0; i < Columns.Count; i++)
        {
            grid.Add(Cell(new Box(new BoxStyle(), new Skeleton(SkeletonShape.Line, i % 2 == 0 ? 96 : 64)),
                Columns[i].Align));
        }

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            MinHeight = RowHeight,
            BorderWidth = 1,
            BorderColor = theme.Border,
        }, grid)
        {
            Key = $"pending-{index}",
        };
    }

    /// <summary>Every cell gets the same inset and the same alignment discipline.</summary>
    private static VisualNode Cell(VisualNode child, TextAlignment align)
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
            Padding = EdgeInsets.Symmetric(Space.S3, Space.S2),
        }, row);
    }
}
