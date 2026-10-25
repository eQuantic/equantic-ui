using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Headers read by the engine (#984, D14): a press on a column's header or a row's number reaches the sheet
/// with its modifiers, so a drag across headers, Shift and ⌘ or Ctrl on a header mean what they mean on a
/// cell, and the resize grip on a header's edge resizes and selects nothing.
/// </summary>
public class SheetHeaderTests
{
    private const float Grip = 6f;

    private static SheetController Sheet(double across = 0, double down = 0)
    {
        var sheet = new SheetController(200, 30);
        SheetScrolls.Place(sheet, across, down);
        sheet.Grid = sheet.Grid with { ResizeGrip = Grip };
        return sheet;
    }

    /// <summary>The middle of a column's header, as the grid shows it at no offset.</summary>
    private static Point ColumnHeader(int col) =>
        new(SheetScrolls.HeaderWidth + col * 96f + 48f, SheetScrolls.HeaderHeight / 2);

    private static Point RowHeader(int row) =>
        new(SheetScrolls.HeaderWidth / 2, SheetScrolls.HeaderHeight + row * 28f + 14f);

    private static bool Pointer(SheetController sheet, PointerPhase phase, Point at,
        KeyModifiers modifiers = KeyModifiers.None, PointerKind kind = PointerKind.Mouse) =>
        ((ISheetSurfaceModel)sheet).HandlePointer(phase, at, modifiers, 1, kind);

    private static void Press(SheetController sheet, Point at, KeyModifiers modifiers = KeyModifiers.None)
    {
        Pointer(sheet, PointerPhase.Down, at, modifiers);
        Pointer(sheet, PointerPhase.Up, at, modifiers);
    }

    private static SheetRange Columns(SheetController sheet, int from, int to) =>
        new(new CellRef(0, from), new CellRef(sheet.Document.Rows - 1, to));

    private static SheetRange Rows(SheetController sheet, int from, int to) =>
        new(new CellRef(from, 0), new CellRef(to, sheet.Document.Cols - 1));

    [Fact]
    public void APressOnAColumnsHeader_SelectsTheWholeColumn()
    {
        var sheet = Sheet();

        Press(sheet, ColumnHeader(2));

        sheet.Ranges.Should().Equal(Columns(sheet, 2, 2));
        sheet.ActiveCell.Should().Be(new CellRef(0, 2));
    }

    [Fact]
    public void ADragAcrossColumnHeaders_StretchesTheBand()
    {
        var sheet = Sheet();

        Pointer(sheet, PointerPhase.Down, ColumnHeader(1));
        Pointer(sheet, PointerPhase.Move, ColumnHeader(3)).Should().BeTrue();
        Pointer(sheet, PointerPhase.Up, ColumnHeader(3));

        sheet.Ranges.Should().Equal(Columns(sheet, 1, 3));
        sheet.ActiveCell.Should().Be(new CellRef(0, 1), "the active cell stays where the press left it");
    }

    [Fact]
    public void ShiftOnARowsNumber_StretchesFromTheAnchorsRow_AndKeepsTheActiveCell()
    {
        var sheet = Sheet();
        sheet.Selection = new SheetRange(new CellRef(2, 1));

        Press(sheet, RowHeader(5), KeyModifiers.Shift);

        sheet.Selection.TopRow.Should().Be(2);
        sheet.Selection.BottomRow.Should().Be(5);
        sheet.Selection.LeftCol.Should().Be(0);
        sheet.Selection.RightCol.Should().Be(sheet.Document.Cols - 1);
        sheet.ActiveCell.Should().Be(new CellRef(2, 1));
    }

    [Fact]
    public void CommandOnAColumnsHeader_AddsTheColumn()
    {
        var sheet = Sheet();
        sheet.Selection = new SheetRange(new CellRef(1, 1));

        Press(sheet, ColumnHeader(4), KeyModifiers.Command);

        sheet.Ranges.Should().Equal(new SheetRange(new CellRef(1, 1)), Columns(sheet, 4, 4));
    }

    [Fact]
    public void ADragAcrossRowNumbers_StretchesRows()
    {
        var sheet = Sheet();

        Pointer(sheet, PointerPhase.Down, RowHeader(2));
        Pointer(sheet, PointerPhase.Move, RowHeader(6));
        Pointer(sheet, PointerPhase.Up, RowHeader(6));

        sheet.Ranges.Should().Equal(Rows(sheet, 2, 6));
    }

    [Fact]
    public void TheCorner_SelectsTheSheet()
    {
        var sheet = Sheet();

        Press(sheet, new Point(SheetScrolls.HeaderWidth / 2, SheetScrolls.HeaderHeight / 2));

        sheet.Selection.Should().Be(new SheetRange(new CellRef(0, 0),
            new CellRef(sheet.Document.Rows - 1, sheet.Document.Cols - 1)));
    }

    [Fact]
    public void AResizeGrip_ResizesAndSelectsNothing()
    {
        var sheet = Sheet();
        sheet.Selection = new SheetRange(new CellRef(1, 1));
        // Column C's header ends at 44 + 3 × 96: its grip is the six points before.
        var grip = new Point(SheetScrolls.HeaderWidth + 3 * 96f - Grip / 2, SheetScrolls.HeaderHeight / 2);

        Pointer(sheet, PointerPhase.Down, grip).Should().BeFalse("the grip's press is the grip's");
        Pointer(sheet, PointerPhase.Move, grip with { X = grip.X + 30 }).Should().BeFalse();
        Pointer(sheet, PointerPhase.Up, grip with { X = grip.X + 30 });

        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1)));
    }

    [Fact]
    public void AScrolledGrid_ReadsTheHeaderItShows_AndActivatesTheFirstRowShown()
    {
        // Two columns across and ten rows down.
        var sheet = Sheet(across: 2 * 96, down: 10 * 28);

        Press(sheet, ColumnHeader(0));

        sheet.Ranges.Should().Equal(Columns(sheet, 2, 2));
        sheet.ActiveCell.Should().Be(new CellRef(10, 2));
    }

    [Fact]
    public void AFingerOnAHeader_SelectsWhenItLifts()
    {
        var sheet = Sheet();
        sheet.Selection = new SheetRange(new CellRef(1, 1));

        Pointer(sheet, PointerPhase.Down, ColumnHeader(3), kind: PointerKind.Touch);
        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1)), "a finger's press may still be a pan");
        Pointer(sheet, PointerPhase.Up, ColumnHeader(3), kind: PointerKind.Touch);

        sheet.Ranges.Should().Equal(Columns(sheet, 3, 3));
    }

    [Fact]
    public void AFingerThatPans_SelectsNoHeader()
    {
        var sheet = Sheet();
        sheet.Selection = new SheetRange(new CellRef(1, 1));

        Pointer(sheet, PointerPhase.Down, ColumnHeader(3), kind: PointerKind.Touch);
        Pointer(sheet, PointerPhase.Cancel, ColumnHeader(3), kind: PointerKind.Touch);

        sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1)));
    }

    [Fact]
    public void ADragAlongTheColumnHeaders_ScrollsAcrossOnly()
    {
        var sheet = Sheet();
        var scroll = (TableScroll)sheet.Grid.Scroll!;

        Pointer(sheet, PointerPhase.Down, ColumnHeader(1));
        // Over the headers, inside the window across: the headers' height is no edge past the cells' top.
        Pointer(sheet, PointerPhase.Move, ColumnHeader(4)).Should().BeTrue();
        scroll.Advance(0);
        scroll.Advance(500).Should().BeFalse("a drag inside the window along the headers scrolls nothing");
        scroll.Offset.Should().Be(TableOffset.Zero);

        // Past the window's right edge it scrolls across, and only across.
        Pointer(sheet, PointerPhase.Move, new Point(950, SheetScrolls.HeaderHeight / 2));
        scroll.Advance(1000);
        scroll.Advance(1500);
        scroll.Offset.Across.Should().BeGreaterThan(0);
        scroll.Offset.Down.Should().Be(0);
    }
}
