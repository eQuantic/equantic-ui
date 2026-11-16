using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The design system's Table (wave 3b): tabular TEXT data over the S4 Grid — a bold Label header
/// row on a BorderStrong hairline, 44dp body rows separated by hairlines, equal flex columns.
/// Write-once by construction: the SAME Grid lowers to CSS Grid on web and the track-sizing pass
/// on Photon. Data changes are the caller's re-render (controlled, like everything).
/// v1 fences: column widths/alignment, sorting, row selection, rich cells (VisualNode), pinned
/// header (compose with Pinned yourself), virtualization (the List recycling fence).
/// </summary>
public sealed class Table : StatelessComponent
{
    public Table(IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public IReadOnlyList<string> Columns { get; init; }
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var tracks = GridTrack.Repeat(Columns.Count, GridTrack.Flex());
        // The theme's Table slot, then the component's own (§3.10).
        var slot = theme.Table;
        var gap = slot?.ColumnGap ?? Space.S3;
        var padding = EdgeInsets.Symmetric(slot?.CellPaddingX ?? Space.S3, slot?.CellPaddingY ?? Space.S2);

        var header = new Grid(tracks, gap: gap) { Width = SizeValue.Fill };
        foreach (var column in Columns)
            header.Add(new Text(column, TypeRole.Label, slot?.TableHeaderColor ?? theme.TextSecondary, maxLines: 1));

        var table = new Column(gap: 0) { Width = SizeValue.Fill };
        table.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = padding,
            BorderWidth = 0,
        }, header));
        table.Add(new Divider());

        for (var r = 0; r < Rows.Count; r++)
        {
            var row = new Grid(tracks, gap: gap) { Width = SizeValue.Fill };
            var cells = Rows[r];
            for (var c = 0; c < Columns.Count; c++)
                row.Add(new Text(c < cells.Count ? cells[c] : string.Empty, TypeRole.BodyM, maxLines: 1));

            table.Add(new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                MinHeight = slot?.RowMinHeight ?? 44,
                Padding = padding,
                Hover = new StyleDiff { Background = slot?.HoverFill ?? theme.SurfaceSubtle },
            }, row));
            if (r < Rows.Count - 1) table.Add(new Divider());
        }

        return table;
    }
}
