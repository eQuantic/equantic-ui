using System.Diagnostics;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Excel's selection and navigation chords at rest (#984), in both keyboard traditions: the rows and the
/// columns of the selection, the current region then the sheet, the last cell in use (D10), a screen of
/// columns, the active cell brought back, the range's next corner, Ctrl+Y (D13), add mode, and the column a
/// run of Tabs began in.
/// </summary>
public class SheetChordTests
{
    private static SheetController Sheet(int rows = 200, int cols = 30)
    {
        var sheet = new SheetController(rows, cols);
        SheetScrolls.Place(sheet);
        return sheet;
    }

    private static CellRef Cell(string a1)
    {
        SheetReference.TryParse(a1, 1_048_576, CellRef.MaxCols, out var range).Should().BeTrue();
        return range.TopLeft;
    }

    private static SheetRange Range(string from, string to) => new(Cell(from), Cell(to));

    private static bool Key(SheetController sheet, string key, KeyModifiers modifiers,
        KeyboardConvention convention = KeyboardConvention.Standard) =>
        ((ISheetSurfaceModel)sheet).HandleKey(key, modifiers, convention, null);

    /// <summary>The modifier Excel for Mac moves from ⌘ to ⌃ for the selection's columns, showing the active
    /// cell and the next corner; Ctrl, which <see cref="KeyModifiers.Command"/> stands for, elsewhere.</summary>
    private static KeyModifiers ControlOf(KeyboardConvention convention) =>
        convention == KeyboardConvention.Apple ? KeyModifiers.Control : KeyModifiers.Command;

    [Theory]
    [InlineData(KeyboardConvention.Standard)]
    [InlineData(KeyboardConvention.Apple)]
    public void ShiftSpace_SelectsTheWholeRows_AndTypesNothing(KeyboardConvention convention)
    {
        var sheet = Sheet();
        sheet.Selection = Range("B3", "C4");

        Key(sheet, " ", KeyModifiers.Shift, convention).Should().BeTrue("the key is the sheet's, so no space is typed");

        sheet.Selection.Should().Be(new SheetRange(new CellRef(2, 0), new CellRef(3, sheet.Document.Cols - 1)));
        sheet.ActiveCell.Should().Be(Cell("B3"), "the active cell, the range's anchor, stays");
        sheet.Editing.Should().BeFalse();
    }

    [Theory]
    [InlineData(KeyboardConvention.Standard)]
    [InlineData(KeyboardConvention.Apple)]
    public void CtrlSpace_SelectsTheWholeColumns(KeyboardConvention convention)
    {
        var sheet = Sheet();
        sheet.Selection = new SheetRange(Cell("B3"));

        Key(sheet, " ", ControlOf(convention), convention).Should().BeTrue();

        sheet.Selection.Should().Be(new SheetRange(new CellRef(0, 1), new CellRef(sheet.Document.Rows - 1, 1)));
    }

    [Fact]
    public void CommandSpace_OnAMac_IsNotTheSheets()
    {
        var sheet = Sheet();

        Key(sheet, " ", KeyModifiers.Command, KeyboardConvention.Apple).Should().BeFalse("⌘Space is the system's");
    }

    [Theory]
    [InlineData(KeyboardConvention.Standard)]
    [InlineData(KeyboardConvention.Apple)]
    public void CommandA_SelectsTheCurrentRegion_ThenTheSheet(KeyboardConvention convention)
    {
        var sheet = Sheet();
        for (var row = 1; row <= 4; row++)
            for (var col = 1; col <= 3; col++)
                sheet.Document.SetCell(new CellRef(row, col), "x");
        sheet.Selection = new SheetRange(Cell("C3"));

        Key(sheet, "a", KeyModifiers.Command, convention);
        sheet.Selection.Should().Be(Range("B2", "D5"));
        sheet.ActiveCell.Should().Be(Cell("C3"), "the active cell stays");

        Key(sheet, "a", KeyModifiers.Command, convention);
        sheet.Selection.Should().Be(new SheetRange(new CellRef(0, 0),
            new CellRef(sheet.Document.Rows - 1, sheet.Document.Cols - 1)));
    }

    [Fact]
    public void CommandA_OnAnEmptyCellAlone_SelectsTheSheet()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(Cell("B2"), "far");
        sheet.Selection = new SheetRange(Cell("H9"));

        Key(sheet, "a", KeyModifiers.Command);

        sheet.Selection.Should().Be(new SheetRange(new CellRef(0, 0),
            new CellRef(sheet.Document.Rows - 1, sheet.Document.Cols - 1)));
    }

    [Fact]
    public void TheCurrentRegion_JoinsThroughCornersAndCrossesNoGap()
    {
        var sheet = Sheet();
        foreach (var a1 in new[] { "B2", "C3", "D4", "F4" }) sheet.Document.SetCell(Cell(a1), "x");

        sheet.CurrentRegion(Cell("B2")).Should().Be(Range("B2", "D4"),
            "B2, C3 and D4 touch by their corners, and F4 stands past an empty column");
    }

    [Theory]
    [InlineData(KeyboardConvention.Standard)]
    [InlineData(KeyboardConvention.Apple)]
    public void CommandEnd_GoesToTheLastCellInUse_AndShiftStretchesToIt(KeyboardConvention convention)
    {
        var sheet = Sheet(1000, 26);
        sheet.Document.SetCell(Cell("B3"), "1");
        sheet.Document.SetCell(Cell("E2"), "2");

        Key(sheet, "End", KeyModifiers.Command, convention);
        sheet.ActiveCell.Should().Be(Cell("E3"));

        sheet.Selection = new SheetRange(Cell("B2"));
        Key(sheet, "End", KeyModifiers.Command | KeyModifiers.Shift, convention);
        sheet.Selection.Should().Be(Range("B2", "E3"));
    }

    [Fact]
    public void CommandEnd_InAnEmptySheet_IsTheFirstCell()
    {
        var sheet = Sheet();
        sheet.Selection = new SheetRange(Cell("D4"));

        Key(sheet, "End", KeyModifiers.Command);

        sheet.ActiveCell.Should().Be(Cell("A1"));
    }

    [Fact]
    public void TheLastCellInUse_FollowsTheValues()
    {
        var sheet = Sheet();
        sheet.Document.SetCell(Cell("C5"), "x");
        sheet.LastCellInUse().Should().Be(Cell("C5"));

        sheet.Document.SetCell(Cell("C5"), "");
        sheet.LastCellInUse().Should().Be(Cell("A1"), "a cell emptied is no longer in use");
    }

    [Fact]
    public void AltPageDown_MovesAScreenOfColumns()
    {
        // 900 points of window, 44 of row numbers: eight whole columns of 96 show.
        var sheet = Sheet();

        Key(sheet, "PageDown", KeyModifiers.Alt).Should().BeTrue();
        sheet.ActiveCell.Should().Be(Cell("I1"));

        Key(sheet, "PageUp", KeyModifiers.Alt);
        sheet.ActiveCell.Should().Be(Cell("A1"));

        Key(sheet, "PageDown", KeyModifiers.Alt | KeyModifiers.Shift);
        sheet.Selection.Should().Be(Range("A1", "I1"));
    }

    [Theory]
    [InlineData(KeyboardConvention.Standard)]
    [InlineData(KeyboardConvention.Apple)]
    public void CtrlBackspace_BringsTheActiveCellBack_MovingNothing(KeyboardConvention convention)
    {
        var sheet = Sheet();
        sheet.Selection = new SheetRange(Cell("B2"));
        var version = sheet.RevealVersion;

        Key(sheet, "Backspace", ControlOf(convention), convention).Should().BeTrue();

        sheet.RevealVersion.Should().BeGreaterThan(version);
        sheet.RevealCell.Should().Be(Cell("B2"));
        sheet.Selection.Should().Be(new SheetRange(Cell("B2")));
        sheet.Editing.Should().BeFalse("it is not Backspace's edit");
    }

    [Theory]
    [InlineData(KeyboardConvention.Standard)]
    [InlineData(KeyboardConvention.Apple)]
    public void CtrlPeriod_WalksTheCornersClockwise(KeyboardConvention convention)
    {
        var sheet = Sheet();
        sheet.Selection = Range("B2", "D5");
        // The selection's anchor is B2, so B2 is active.
        sheet.ActiveCell.Should().Be(Cell("B2"));

        Key(sheet, ".", ControlOf(convention), convention);
        sheet.ActiveCell.Should().Be(Cell("D2"));
        Key(sheet, ".", ControlOf(convention), convention);
        sheet.ActiveCell.Should().Be(Cell("D5"));
        Key(sheet, ".", ControlOf(convention), convention);
        sheet.ActiveCell.Should().Be(Cell("B5"));
        sheet.Selection.Should().Be(Range("B2", "D5"));
    }

    [Theory]
    [InlineData(KeyboardConvention.Standard)]
    [InlineData(KeyboardConvention.Apple)]
    public void CommandY_Redoes(KeyboardConvention convention)
    {
        var sheet = Sheet();
        sheet.SetCell(Cell("B2"), "7");
        sheet.Undo();

        Key(sheet, "y", KeyModifiers.Command, convention).Should().BeTrue();

        sheet.Document.GetCell(Cell("B2")).Should().Be("7");
    }

    // ---- the Tab-Enter column -------------------------------------------------------------------------------

    [Fact]
    public void EnterAfterARunOfTabs_ReturnsToTheColumnTheRunBeganIn()
    {
        var sheet = Sheet();
        sheet.Selection = new SheetRange(Cell("B2"));
        ISheetSurfaceModel model = sheet;

        model.HandleText("1");
        Key(sheet, "Tab", KeyModifiers.None);
        model.HandleText("2");
        Key(sheet, "Tab", KeyModifiers.None);
        model.HandleText("3");
        Key(sheet, "Enter", KeyModifiers.None);

        sheet.Document.GetCell(Cell("B2")).Should().Be("1");
        sheet.Document.GetCell(Cell("C2")).Should().Be("2");
        sheet.Document.GetCell(Cell("D2")).Should().Be("3");
        sheet.ActiveCell.Should().Be(Cell("B3"));
    }

    [Fact]
    public void AMove_BreaksTheRun()
    {
        var sheet = Sheet();
        sheet.Selection = new SheetRange(Cell("B2"));

        Key(sheet, "Tab", KeyModifiers.None);
        Key(sheet, "ArrowDown", KeyModifiers.None);
        Key(sheet, "Enter", KeyModifiers.None);

        sheet.ActiveCell.Should().Be(Cell("C4"));
    }

    [Fact]
    public void ARunAtRest_ReturnsToo()
    {
        var sheet = Sheet();
        sheet.Selection = new SheetRange(Cell("C5"));

        Key(sheet, "Tab", KeyModifiers.None);
        Key(sheet, "Tab", KeyModifiers.None);
        Key(sheet, "Enter", KeyModifiers.None);

        sheet.ActiveCell.Should().Be(Cell("C6"));
        Key(sheet, "Enter", KeyModifiers.None);
        sheet.ActiveCell.Should().Be(Cell("C7"), "the run ended with the first Enter");
    }

    // ---- how much a region costs --------------------------------------------------------------------------

    [Fact]
    public void TheRegionOfADenseBlock_IsReadInAFewMilliseconds()
    {
        var sheet = new SheetController(2000, 200);
        for (var row = 0; row < 1000; row++)
            for (var col = 0; col < 100; col++)
                sheet.Document.SetCell(new CellRef(row, col), "x");
        sheet.Selection = new SheetRange(new CellRef(500, 50));

        sheet.CurrentRegion(new CellRef(500, 50));   // warm
        var clock = Stopwatch.StartNew();
        var region = sheet.CurrentRegion(new CellRef(500, 50));
        clock.Stop();

        region.Should().Be(new SheetRange(new CellRef(0, 0), new CellRef(999, 99)));
        // Measured at 0.1 ms: each line along a side stops at the first cell in use it meets.
        clock.Elapsed.TotalMilliseconds.Should().BeLessThan(5);
    }
}
