using eQuantic.UI.Components;
using eQuantic.UI.Core;
using eQuantic.UI.Primitives;

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

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var page = new Column(gap: Space.S3) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        page.Add(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Padding = new EdgeInsets(Space.S4, Space.S4, 0, Space.S4),
        }, new Text("Click a cell, type, Enter — ⌘C/⌘V speak TSV with Excel, ⌘Z undoes.",
            TypeRole.BodyM, theme.TextSecondary, maxLines: 1)));
        page.Add(new Flexible(new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Padding = EdgeInsets.All(Space.S4),
        }, new Spreadsheet(_sheet) { Width = SizeValue.Fill, Height = SizeValue.Fill }), 1));

        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = SizeValue.Fill,
            Background = theme.Background,
        }, page);
    }
}
