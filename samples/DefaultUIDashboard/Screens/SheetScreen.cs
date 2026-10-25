using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;

namespace eQuantic.Console;

/// <summary>
/// The write-once spreadsheet, live in the browser: click a cell, type (Excel's replace-and-go
/// rhythm), Enter commits stepping down, ⌘C/⌘V speak TSV with the real Excel/Sheets, ⌘Z undoes.
/// The SAME component, controller and keymap run natively on Photon.
/// </summary>
[Page("/sheet", Title = "Spreadsheet — eQuantic Console")]
public sealed class SheetScreen : StatefulComponent
{
    private readonly SheetController _sheet = Seed();

    private static SheetController Seed()
    {
        var sheet = new SheetController(rows: 200, cols: 8);
        sheet.Document.SetCell(new CellRef(0, 0), "Ledger");
        sheet.Document.SetCell(new CellRef(0, 1), "Amount");
        sheet.Document.SetCell(new CellRef(0, 2), "Status");
        for (var r = 1; r <= 40; r++)
        {
            sheet.Document.SetCell(new CellRef(r, 0), $"Entry {r}");
            sheet.Document.SetCell(new CellRef(r, 1), $"{r * 7},{r % 100:D2}");
            sheet.Document.SetCell(new CellRef(r, 2), r % 3 == 0 ? "pending" : "settled");
        }
        return sheet;
    }

    /// <summary>Whether the compact drawer is up — page state wherever the page is.</summary>
    private bool _navOpen;

    /// <summary>
    /// The frame, then this screen inside it. Every route wraps itself: there is no separate
    /// layout, and none is needed — the reconciler matches the frame position for position across
    /// a navigation, so the sidebar is never rebuilt and only the middle changes.
    /// </summary>
    public override VisualNode Build(ComponentContext context) =>
        ConsoleShell.Frame(context.Theme, "/sheet", "Spreadsheet", Content(context),
            _navOpen, () => SetState(() => _navOpen = !_navOpen));

    private VisualNode Content(ComponentContext context)
    {
        var theme = context.Theme;
        var page = new Column(gap: Space.S3) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        page.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = new EdgeInsets(Space.S4, Space.S4, 0, Space.S4),
        }, new Text("Click a cell and type, F2 edits in place, Alt+Enter breaks a line, F5 or Ctrl+G goes to a cell — ⌘C/⌘V speak TSV with Excel, ⌘Z undoes.",
            TypeRole.BodyM, theme.TextSecondary, maxLines: 1)));
        page.Add(new Flexible(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        }, Excel(_sheet)), 1));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        }, page);
    }

    /// <summary>Excel's layout, composed by the page: the name box and the formula bar over the grid, all
    /// three views of the one controller.</summary>
    private static VisualNode Excel(SheetController sheet)
    {
        var bars = new Row(gap: Space.S2) { Width = SizeValue.Fill };
        bars.Add(NameBox(sheet));
        bars.Add(new Flexible(FormulaBar(sheet), 1));
        var layout = new Column(gap: Space.S2) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        layout.Add(bars);
        layout.Add(new Flexible(Spreadsheet(sheet, width: SizeValue.Fill, height: SizeValue.Fill), 1));
        return layout;
    }
}
