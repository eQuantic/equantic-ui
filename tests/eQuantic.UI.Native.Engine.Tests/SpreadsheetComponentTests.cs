using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The write-once Spreadsheet component: headers, the virtualized row window, and — the part the
/// design hinges on — the surface living INSIDE the scroll, so a click on a scrolled grid selects
/// the cell actually under the pointer and the marks stay aligned at any offset.
/// </summary>
public class SpreadsheetComponentTests
{
    private static (PhotonHost Host, SheetController Sheet) Open(int rows = 1000)
    {
        var sheet = new SheetController(rows, cols: 6);
        for (var r = 0; r < rows; r++)
            sheet.Document.SetCell(new CellRef(r, 0), $"row {r}");
        var host = new PhotonHost(
            new Spreadsheet(sheet) { Width = SizeValue.Fill, Height = SizeValue.Fill },
            PhotonTheme.Instance, ThemeMode.Light, 700, 400)
        {
            SmoothScroll = false,
        };
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder(), 16);   // the viewport reported; window corrected
        return (host, sheet);
    }

    [Fact]
    public void TheWindow_MaterializesAScreenful_NotTheSheet()
    {
        // The grid's cells live inside the surface (one announced region for now), so the window
        // is proven two ways: the row headers OUTSIDE it, and the ListView ruler — a sheet twenty
        // times longer must emit exactly the same draw commands.
        var (host, _) = Open();
        var texts = host.Semantics().Where(s => s.Role == SemanticRole.StaticText).ToList();
        texts.Should().Contain(s => s.Label == "A", "column headers spell A..");
        texts.Should().Contain(s => s.Label == "1", "row headers count from 1");
        texts.Count(s => int.TryParse(s.Label, out _)).Should().BeLessThan(40,
            "row headers exist for the WINDOW, not the thousand rows");

        var big = new DisplayListBuilder();
        Open(rows: 1000).Host.RenderFrame(big, 32);
        var small = new DisplayListBuilder();
        Open(rows: 50).Host.RenderFrame(small, 32);
        big.Build().Count.Should().Be(small.Build().Count,
            "rows outside the window are a spacer, never cells");
    }

    [Fact]
    public void ColumnNames_SpellLikeExcel()
    {
        Spreadsheet.ColumnName(0).Should().Be("A");
        Spreadsheet.ColumnName(25).Should().Be("Z");
        Spreadsheet.ColumnName(26).Should().Be("AA");
        Spreadsheet.ColumnName(701).Should().Be("ZZ");
    }

    [Fact]
    public void AClickOnTheGrid_SelectsTheCellUnderIt()
    {
        var (host, sheet) = Open();

        // First grid cell: past the row-header column (44) and the fixed header row (26).
        host.PressDown(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);
        host.PressUp(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);

        sheet.ActiveCell.Should().Be(new CellRef(0, 0));
    }

    [Fact]
    public void AfterScrolling_AClickStillSelectsTheCellUnderThePointer()
    {
        var (host, sheet) = Open();

        // Scroll 10 rows down (280dp), settle the window, click the first visible cell.
        host.ScrollBy(300, 200, 10 * 28f);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.RenderFrame(new DisplayListBuilder(), 48);

        host.PressDown(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);
        host.PressUp(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);

        sheet.ActiveCell.Row.Should().Be(10,
            "the surface scrolled WITH the content, so the arithmetic and the pixels agree");
        sheet.ActiveCell.Col.Should().Be(0);
    }

    [Fact]
    public void TheKeyboard_ReachesTheSheet_ThroughTheComponent()
    {
        var (host, sheet) = Open();
        host.PressDown(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);
        host.PressUp(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);

        host.KeyDown("ArrowDown", KeyModifiers.None);
        host.KeyDown("ArrowRight", KeyModifiers.None);

        sheet.ActiveCell.Should().Be(new CellRef(1, 1));
    }
}
