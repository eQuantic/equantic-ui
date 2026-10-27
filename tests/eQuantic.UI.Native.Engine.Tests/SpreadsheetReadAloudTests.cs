using System.Globalization;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using eQuantic.UI.Spreadsheets;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The spreadsheet read aloud on Photon (#1081): its table a grid named after the sheet, the letters and the
/// numbers heading their columns and rows, the ranges' cells selected and the active cell current, each move
/// and each growing range announced through the frames as a shell posts it, and a reader's activation of a
/// cell or a header answered as a tap. The web's twin is <c>spreadsheet-read-aloud-web.spec.ts</c>.
/// </summary>
public class SpreadsheetReadAloudTests
{
    /// <summary>A sheet of 50 rows by 8 columns with 14,02 in B3, laid out and seeded.</summary>
    private static (PhotonHost Host, SheetController Sheet) Open()
    {
        var sheet = new SheetController(50, 8);
        sheet.Document.SetCell(new CellRef(2, 1), "14,02");
        var host = new PhotonHost(new Spreadsheet(sheet) { Width = SizeValue.Fill, Height = SizeValue.Fill },
            PhotonTheme.Instance, ThemeMode.Light, 900, 500)
        {
            SmoothScroll = false,
        };
        host.RenderFrame(new DisplayListBuilder(), 0);
        host.RenderFrame(new DisplayListBuilder(), 16);
        host.TakeAnnouncements();
        return (host, sheet);
    }

    /// <summary>A click on the sheet's cell at a row and a column.</summary>
    private static void Click(PhotonHost host, int row, int column, KeyModifiers modifiers = KeyModifiers.None)
    {
        var x = Spreadsheet.HeaderWidth + column * 96f + 48f;
        var y = Spreadsheet.HeaderHeight + row * 28f + 14f;
        host.PressDown(x, y, 1, modifiers);
        host.PressUp(x, y);
    }

    private static IReadOnlyList<LiveAnnouncement> Frame(PhotonHost host, float timeMs)
    {
        host.RenderFrame(new DisplayListBuilder(), timeMs);
        return host.TakeAnnouncements();
    }

    /// <summary>The node of the table's cell at a place, one past the sheet's headers.</summary>
    private static SemanticNode At(PhotonHost host, int row, int column) =>
        host.Semantics().Single(node => node.Place == new TableRange(row, column));

    [Fact]
    public void AReaderFindsTheGrid_ItsHeaders_AndACellWhereTheSheetHasIt()
    {
        var (host, _) = Open();

        var grid = host.Semantics().Single(node => node.Role == SemanticRole.Grid);
        grid.Label.Should().Be("Spreadsheet");
        grid.Extent.Should().Be(new TableRange(0, 0, 51, 9), "the sheet's rows and columns and the headers' band of each");
        At(host, 0, 2).Should().Match<SemanticNode>(node => node.Role == SemanticRole.ColumnHeader && node.Label == "B");
        At(host, 3, 0).Should().Match<SemanticNode>(node => node.Role == SemanticRole.RowHeader && node.Label == "3");
        At(host, 3, 2).Should().Match<SemanticNode>(node => node.Role == SemanticRole.GridCell && node.Label == "14,02");
        At(host, 0, 0).Should().Match<SemanticNode>(node => node.Role == SemanticRole.ColumnHeader && node.Label.Length == 0,
            "the corner heads the row numbers' column, and says nothing");
    }

    [Fact]
    public void TheRangesCellsAreSelected_AndTheActiveCellCurrent()
    {
        var (host, sheet) = Open();
        Click(host, 1, 1);
        host.KeyDown("ArrowDown", KeyModifiers.Shift);
        host.KeyDown("ArrowDown", KeyModifiers.Shift);
        Click(host, 5, 3, KeyModifiers.Command);
        Frame(host, 100);
        sheet.Ranges.Should().HaveCount(2);

        var selected = host.Semantics().Where(node => node.Selected == true).Select(node => node.Place).ToList();
        selected.Should().BeEquivalentTo(new TableRange?[]
        {
            new TableRange(2, 2), new TableRange(3, 2), new TableRange(4, 2), new TableRange(6, 4),
        });
        host.Semantics().Where(node => node.Current).Select(node => node.Place).Should().Equal(new TableRange(6, 4));
    }

    [Fact]
    public void AnArrow_ABlankCell_AndAGrowingRange_AreAnnounced()
    {
        var (host, _) = Open();
        Click(host, 1, 1);
        Frame(host, 100).Single().Text.Should().Be("B2, blank");

        host.KeyDown("ArrowDown");
        Frame(host, 1200).Single().Text.Should().Be("B3, 14,02");
        host.Semantics().Single(node => node.Role == SemanticRole.CodeField).Value.Should().Be("B3, 14,02",
            "a reader arriving on the sheet's field hears where it is");

        host.KeyDown("ArrowRight");
        Frame(host, 2300).Single().Text.Should().Be("C3, blank");

        host.KeyDown("ArrowRight", KeyModifiers.Shift);
        host.KeyDown("ArrowDown", KeyModifiers.Shift);
        host.KeyDown("ArrowDown", KeyModifiers.Shift);
        Frame(host, 3400).Single().Text.Should().Be("C3:D5, 3 rows by 2 columns");
    }

    [Fact]
    public void AGrowingRange_IsAnnouncedInThePagesCulture()
    {
        var (culture, interface_) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
            var (host, _) = Open();
            Click(host, 2, 1);
            Frame(host, 100).Single().Text.Should().Be("B3, 14,02");

            host.KeyDown("ArrowRight", KeyModifiers.Shift);
            host.KeyDown("ArrowDown", KeyModifiers.Shift);
            host.KeyDown("ArrowDown", KeyModifiers.Shift);
            Frame(host, 1200).Single().Text.Should().Be("B3:C5, 3 linhas por 2 colunas");
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, interface_);
        }
    }

    [Fact]
    public void QuickArrows_AnnounceTheFirstMove_ThenWhereTheyEnded()
    {
        var (host, _) = Open();
        Click(host, 1, 1);
        Frame(host, 100);

        host.KeyDown("ArrowRight");
        Frame(host, 1200).Single().Text.Should().Be("C2, blank", "the first move is said at once");
        host.KeyDown("ArrowRight");
        Frame(host, 1400).Should().BeEmpty();
        host.KeyDown("ArrowRight");
        Frame(host, 1600).Should().BeEmpty();

        Frame(host, 2200).Single().Text.Should().Be("E2, blank", "where the third arrow left it, when the second ends");
        Frame(host, 3500).Should().BeEmpty("and the second move is never said");
    }

    [Fact]
    public void AnOpenEntry_SilencesTheGrid_EvenAMoveStillOwed()
    {
        var (host, sheet) = Open();
        Click(host, 1, 1);
        Frame(host, 100);

        host.KeyDown("ArrowRight");
        Frame(host, 1200).Single().Text.Should().Be("C2, blank");
        host.KeyDown("ArrowRight");
        Frame(host, 1400).Should().BeEmpty("the second move is owed to the quiet window");
        host.KeyDown("F2");
        sheet.Editing.Should().BeTrue();
        Frame(host, 1600).Should().BeEmpty();

        Frame(host, 2300).Should().BeEmpty("an entry is open, and the grid says nothing of its own over it");
        host.KeyDown("Escape");
        sheet.Editing.Should().BeFalse();
        Frame(host, 3500).Single().Text.Should().Be("D2, blank", "the cell the entry closed on, once it closed");
    }

    [Fact]
    public void AReadersActivationOfACell_MakesItTheActiveCell()
    {
        var (host, sheet) = Open();

        host.Activate(At(host, 6, 4).Id).Should().BeTrue();

        sheet.ActiveCell.Should().Be(new CellRef(5, 3));
        sheet.Ranges.Should().Equal(new SheetRange(new CellRef(5, 3)));
    }

    [Fact]
    public void AReadersActivationOfAHeader_SelectsItsColumn()
    {
        var (host, sheet) = Open();

        host.Activate(At(host, 0, 3).Id).Should().BeTrue();

        sheet.Ranges.Should().Equal(new SheetRange(new CellRef(0, 2), new CellRef(sheet.Document.Rows - 1, 2)));
        Frame(host, 100).Single().Text.Should().Be("C1:C50, 50 rows by 1 column",
            "a band a header selects is said as the range");
    }
}
