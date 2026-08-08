using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Goldens for the spreadsheet's INTERACTIVE states — the pixels Excel users read at a glance:
/// the selection band with its ring and fill handle, the fill-drag preview, header bands, a
/// resized column, and the in-cell draft. Each image ships with an assertion beside it (a golden
/// proves geometry, not state — the lesson is written down), and together they are the aliasing
/// guard for the sheet's paint path.
/// </summary>
public class SpreadsheetGoldenTests
{
    private static (PhotonHost Host, SheetController Sheet) Open()
    {
        var sheet = new SheetController(rows: 30, cols: 4);
        sheet.Document.SetCell(new CellRef(0, 0), "Ledger");
        sheet.Document.SetCell(new CellRef(0, 1), "Amount");
        sheet.Document.SetCell(new CellRef(0, 2), "Status");
        for (var r = 1; r <= 8; r++)
        {
            sheet.Document.SetCell(new CellRef(r, 0), $"Entry {r}");
            sheet.Document.SetCell(new CellRef(r, 1), $"{r * 7},00");
            sheet.Document.SetCell(new CellRef(r, 2), r % 2 == 0 ? "pending" : "settled");
        }
        var host = new PhotonHost(
            new Spreadsheet(sheet) { Width = SizeValue.Fill, Height = SizeValue.Fill },
            PhotonTheme.Instance, ThemeMode.Light, 460, 260)
        {
            SmoothScroll = false,
        };
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder(), 16);
        return (host, sheet);
    }

    private static void Render(PhotonHost host, string golden, float timeMs = 100)
    {
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface((int)host.Width, (int)host.Height);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs);
        backend.Render(builder.Build(), surface);
        GoldenImage.Match(surface, golden);
    }

    [Fact]
    public void SelectionBand_Ring_AndFillHandle()
    {
        var (host, sheet) = Open();
        sheet.Selection = new SheetRange(new CellRef(1, 0), new CellRef(3, 1));
        host.RenderFrame(new DisplayListBuilder(), 50);

        // The assertion beside the image: the handle's crosshair region exists at the band's
        // bottom-right corner — the pixels can't be a leftover of some earlier frame.
        host.CursorAt(Spreadsheet.HeaderWidth + 2 * 96f - 2f, Spreadsheet.HeaderHeight + 4 * 28f - 2f)
            .Should().Be(CursorShape.Crosshair);

        Render(host, "sheet-selection-band");
    }

    [Fact]
    public void FillDragPreview_MarksTheTargetStretch()
    {
        var (host, sheet) = Open();
        sheet.Selection = new SheetRange(new CellRef(1, 1));
        sheet.BeginFill();
        sheet.UpdateFill(new CellRef(4, 1));
        sheet.FillTarget.Should().Be(new SheetRange(new CellRef(2, 1), new CellRef(4, 1)));

        Render(host, "sheet-fill-preview");
    }

    [Fact]
    public void HeaderBands_ShadeWithTheSelection()
    {
        var (host, sheet) = Open();
        sheet.SelectCols(1, 1);
        host.RenderFrame(new DisplayListBuilder(), 50);
        sheet.Selection.BottomRow.Should().Be(sheet.Document.Rows - 1);

        Render(host, "sheet-column-band");
    }

    [Fact]
    public void ResizedColumnAndRow_MoveEveryEdgeAfterThem()
    {
        var (host, sheet) = Open();
        sheet.Resize(SheetAxis.Cols, 0, 140f);
        sheet.Resize(SheetAxis.Rows, 1, 44f);
        sheet.Document.ColWidth(0).Should().Be(140f);

        Render(host, "sheet-resized");
    }

    [Fact]
    public void TheEditingDraft_PaintsWithItsCaret()
    {
        var (host, sheet) = Open();
        host.PressDown(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);
        host.PressUp(Spreadsheet.HeaderWidth + 48f, Spreadsheet.HeaderHeight + 14f);
        host.TextInput("Budget");
        sheet.Editing.Should().BeTrue();
        sheet.Draft.Should().Be("Budget");

        Render(host, "sheet-editing-draft");
    }
}
