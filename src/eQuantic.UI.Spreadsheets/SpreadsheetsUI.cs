using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;

namespace eQuantic.UI.Spreadsheets;

/// <summary>
/// The DECLARATIVE surface of the spreadsheet family: every component as a factory named exactly like
/// its type, so a screen reads without a single <c>new</c>. The SDK puts it in scope beside
/// <c>eQuantic.UI.Components.UI</c> and <c>eQuantic.UI.Charts.ChartsUI</c>
/// (<c>using static eQuantic.UI.Spreadsheets.SpreadsheetsUI;</c>), so a page that shows a sheet is:
/// <code>
/// Spreadsheet(new SheetController(rows: 200, cols: 8), height: SizeValue.Fill)
/// </code>
/// </summary>
public static class SpreadsheetsUI
{
    // `Spreadsheets.Spreadsheet` and not `Spreadsheet`: inside this class the bare name is the FACTORY
    // below, so a type is named through the enclosing namespace, as ChartsUI names its own.
    public static Spreadsheets.Spreadsheet Spreadsheet(SheetController controller, SizeValue width = default,
        SizeValue height = default, int overscan = 3) =>
        new(controller)
        {
            Width = width,
            Height = height,
            Overscan = overscan,
        };

    public static Spreadsheets.FormulaBar FormulaBar(SheetController controller) => new(controller);

    public static Spreadsheets.NameBox NameBox(SheetController controller) => new(controller);

    public static Spreadsheets.StatusBar StatusBar(SheetController controller) => new(controller);
}
