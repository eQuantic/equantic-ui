using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;

namespace eQuantic.Console;

/// <summary>
/// Excel's grid, live: 1 048 576 rows by 16 384 columns, each cell showing its own address, the header
/// row and the row numbers pinned. Only the cells in view are built, the wheel and a trackpad scroll
/// both axes, Shift turns the wheel sideways, and the scrollbars reach the last row. It sits in the
/// console's scrolling page and fills it, which a table takes as the visible height of that page.
/// </summary>
[Page("/table", Title = "Table — eQuantic Console")]
public sealed class TableScreen : StatefulComponent
{
    // Excel's sheet, plus the header row and the column of row numbers in front of it.
    private static readonly TableSpans Rows = new UniformTableSpans(1_048_576 + 1, 28);
    private static readonly TableSpans Columns = new UniformTableSpans(16_384 + 1, 96);

    /// <summary>Whether the compact drawer is up — page state wherever the page is.</summary>
    private bool _navOpen;

    public override VisualNode Build(ComponentContext context) =>
        ConsoleShell.Frame(context.Theme, "/table", "Table", Content(context),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));

    private static VisualNode Content(ComponentContext context)
    {
        var theme = context.Theme;
        var table = new TableView(Rows, Columns, (row, column) => Cell(theme, row, column))
        {
            PinnedRows = 1,
            PinnedColumns = 1,
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Label = "Excel's grid",
            // Read as a table: the letters head their columns, the numbers their rows.
            Role = TableRole.Table,
            CellRole = (row, column) => row == 0 ? TableCellRole.ColumnHeader
                : column == 0 ? TableCellRole.RowHeader
                : TableCellRole.Cell,
        };
        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
            Background = theme.Background,
        }, table);
    }

    /// <summary>A header across the top, a number down the side, and an address everywhere else.</summary>
    private static VisualNode Cell(IAppTheme theme, int row, int column)
    {
        var header = row == 0 || column == 0;
        var label = row == 0 && column == 0 ? ""
            : row == 0 ? CellRef.ColumnName(column - 1)
            : column == 0 ? $"{row}"
            : $"{CellRef.ColumnName(column - 1)}{row}";
        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = header ? theme.SurfaceSubtle : theme.Surface,
            BorderColor = theme.Border,
            BorderWidth = 0.5f,
            Padding = EdgeInsets.All(6),
        }, new Text(label, TypeRole.Caption, header ? theme.TextMuted : theme.TextPrimary, maxLines: 1));
    }
}
