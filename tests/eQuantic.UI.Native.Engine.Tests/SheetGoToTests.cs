using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Excel's Go To and its name box (#983): what may be typed to go somewhere, the selection it lands, the
/// name box's model, and the keys that bring the keyboard to it.
/// </summary>
public class SheetGoToTests
{
    private const int ExcelRows = 1_048_576;

    private static SheetRange Range(int topRow, int leftCol, int bottomRow, int rightCol) =>
        new(new CellRef(topRow, leftCol), new CellRef(bottomRow, rightCol));

    [Theory]
    [InlineData("B7", 6, 1, 6, 1)]
    [InlineData("$B$7", 6, 1, 6, 1)]
    [InlineData("b7", 6, 1, 6, 1)]
    [InlineData(" B7 ", 6, 1, 6, 1)]
    [InlineData("B7:D9", 6, 1, 8, 3)]
    [InlineData("d9:b7", 6, 1, 8, 3)]
    [InlineData("$B$7:$D$9", 6, 1, 8, 3)]
    [InlineData("3:5", 2, 0, 4, CellRef.MaxCols - 1)]
    [InlineData("$5:3", 2, 0, 4, CellRef.MaxCols - 1)]
    [InlineData("B:D", 0, 1, ExcelRows - 1, 3)]
    [InlineData("xfd1048576", ExcelRows - 1, CellRef.MaxCols - 1, ExcelRows - 1, CellRef.MaxCols - 1)]
    public void AReference_NamesTheRangeExcelWould(string text, int top, int left, int bottom, int right)
    {
        SheetReference.TryParse(text, ExcelRows, CellRef.MaxCols, out var range).Should().BeTrue();
        range.Should().Be(Range(top, left, bottom, right));
    }

    [Theory]
    [InlineData("")]
    [InlineData("B0")]
    [InlineData("XFE1")]
    [InlineData("A1048577")]
    [InlineData("A99999999999")]
    [InlineData("1A")]
    [InlineData("A")]
    [InlineData("ABCD1")]
    [InlineData("B7:")]
    [InlineData(":")]
    [InlineData("A1:B")]
    [InlineData("Budget")]
    [InlineData("B 7")]
    public void TextThatNamesNothing_IsRefused(string text)
    {
        SheetReference.TryParse(text, ExcelRows, CellRef.MaxCols, out _).Should().BeFalse();
    }

    [Fact]
    public void AReferencePastADeclaredGrid_IsRefused()
    {
        SheetReference.TryParse("A1000", 1000, 26, out _).Should().BeTrue();
        SheetReference.TryParse("A1001", 1000, 26, out _).Should().BeFalse();
        SheetReference.TryParse("AA1", 1000, 26, out _).Should().BeFalse();
        SheetReference.TryParse("Z:AA", 1000, 26, out _).Should().BeFalse();
    }

    // ---- Go To --------------------------------------------------------------------------------------------

    [Fact]
    public void GoingFarAway_SelectsRevealsAndGivesTheGridTheKeyboard()
    {
        var sheet = new SheetController(ExcelRows, CellRef.MaxCols);
        var reveal = sheet.RevealVersion;
        var focus = sheet.FocusVersion;

        sheet.GoTo("xfd1048576").Should().BeTrue();

        var last = new CellRef(ExcelRows - 1, CellRef.MaxCols - 1);
        sheet.Selection.Should().Be(new SheetRange(last));
        sheet.ActiveCell.Should().Be(last);
        sheet.RevealVersion.Should().BeGreaterThan(reveal);
        sheet.RevealCell.Should().Be(last);
        sheet.FocusVersion.Should().BeGreaterThan(focus);
    }

    [Fact]
    public void GoingToColumns_SelectsThemWithTheirFirstCellActive()
    {
        var sheet = new SheetController(200, 8);

        sheet.GoTo("B:D").Should().BeTrue();

        sheet.Selection.TopRow.Should().Be(0);
        sheet.Selection.BottomRow.Should().Be(199);
        sheet.Selection.LeftCol.Should().Be(1);
        sheet.Selection.RightCol.Should().Be(3);
        sheet.ActiveCell.Should().Be(new CellRef(0, 1));
    }

    [Fact]
    public void ANameThatNamesNothing_ChangesNothing()
    {
        var sheet = new SheetController(200, 8) { Selection = new SheetRange(new CellRef(3, 3)) };

        sheet.GoTo("B0").Should().BeFalse();

        sheet.Selection.Should().Be(new SheetRange(new CellRef(3, 3)));
    }

    [Fact]
    public void GoingSomewhereWhileEditing_CommitsFirst()
    {
        var sheet = new SheetController(200, 8) { Selection = new SheetRange(new CellRef(1, 1)) };
        sheet.HandleText("7");

        sheet.GoTo("D4");

        sheet.Document.GetCell(new CellRef(1, 1)).Should().Be("7");
        sheet.Editing.Should().BeFalse();
        sheet.ActiveCell.Should().Be(new CellRef(3, 3));
    }

    // ---- the name box's model ------------------------------------------------------------------------------

    private static (SheetController Sheet, ICodeSurfaceModel Box) Boxed()
    {
        var sheet = new SheetController(ExcelRows, CellRef.MaxCols) { Selection = new SheetRange(new CellRef(1, 1)) };
        return (sheet, sheet.NameBox);
    }

    [Fact]
    public void TakingTheKeyboard_SelectsTheAddress_AndTheFirstPressKeepsItSelected()
    {
        var (sheet, box) = Boxed();

        box.FocusChanged(true);
        box.HandlePointer(PointerPhase.Down, new Point(12, 8), KeyModifiers.None, 1);
        box.HandlePointer(PointerPhase.Up, new Point(12, 8), KeyModifiers.None, 1);

        sheet.NameBox.Typing.Should().BeTrue();
        var entry = sheet.NameBox.Entry;
        entry.Document.Text.Should().Be("B2");
        entry.Document.TextIn(entry.Selection).Should().Be("B2", "what is typed replaces the address");
    }

    [Fact]
    public void EnterGoesWhereItWasTold_AndHandsTheKeyboardBack()
    {
        var (sheet, box) = Boxed();
        box.FocusChanged(true);
        var focus = sheet.FocusVersion;

        foreach (var c in "xfd1048576") box.HandleText(c.ToString());
        box.HandleKey("Enter", KeyModifiers.None, KeyboardConvention.Standard, null).Should().BeTrue();

        sheet.ActiveCell.Should().Be(new CellRef(ExcelRows - 1, CellRef.MaxCols - 1));
        sheet.FocusVersion.Should().BeGreaterThan(focus);
        sheet.NameBox.Invalid.Should().BeFalse();
    }

    [Fact]
    public void ANameThatNamesNothing_StaysInTheBox_MarkedInvalid()
    {
        var (sheet, box) = Boxed();
        box.FocusChanged(true);

        box.HandleText("B");
        box.HandleText("0");
        box.HandleKey("Enter", KeyModifiers.None, KeyboardConvention.Standard, null).Should().BeTrue();

        sheet.NameBox.Invalid.Should().BeTrue();
        sheet.NameBox.Typing.Should().BeTrue();
        sheet.NameBox.Entry.Document.Text.Should().Be("B0");
        sheet.ActiveCell.Should().Be(new CellRef(1, 1));

        box.HandleText("1");
        sheet.NameBox.Invalid.Should().BeFalse("typing again clears the mark");
    }

    [Fact]
    public void LeavingTheBox_ClearsTheInvalidMark()
    {
        var (sheet, box) = Boxed();
        box.FocusChanged(true);
        box.HandleText("B0");
        box.HandleKey("Enter", KeyModifiers.None, KeyboardConvention.Standard, null);
        sheet.NameBox.Invalid.Should().BeTrue();

        box.FocusChanged(false);

        sheet.NameBox.Invalid.Should().BeFalse("at rest the box shows the address, unmarked");
    }

    [Theory]
    [InlineData(KeyModifiers.Shift)]
    [InlineData(KeyModifiers.Alt)]
    [InlineData(KeyModifiers.Command)]
    public void EnterWithAModifier_GoesThereToo(KeyModifiers modifiers)
    {
        var (sheet, box) = Boxed();
        box.FocusChanged(true);
        box.HandleText("D4");

        box.HandleKey("Enter", modifiers, KeyboardConvention.Standard, null).Should().BeTrue();

        sheet.ActiveCell.Should().Be(new CellRef(3, 3));
        sheet.NameBox.Entry.Document.LineCount.Should().Be(1, "no line break went into the box");
    }

    [Fact]
    public void Escape_PutsTheAddressBack_AndHandsTheKeyboardBack()
    {
        var (sheet, box) = Boxed();
        box.FocusChanged(true);
        box.HandleText("Q");
        var focus = sheet.FocusVersion;

        box.HandleKey("Escape", KeyModifiers.None, KeyboardConvention.Standard, null).Should().BeTrue();

        sheet.NameBox.Typing.Should().BeFalse("the box shows the address again");
        sheet.FocusVersion.Should().BeGreaterThan(focus);
    }

    [Fact]
    public void ADrag_IsSelecting_WhileItDrawsMoreThanOneCell()
    {
        var sheet = new SheetController(200, 8);
        ISheetSurfaceModel model = sheet;
        Point Center(int row, int col) => new(col * 96f + 48f, row * 28f + 14f);

        model.HandlePointer(PointerPhase.Down, Center(1, 1), KeyModifiers.None, 1, PointerKind.Mouse);
        sheet.Selecting.Should().BeFalse("one cell is no range");
        model.HandlePointer(PointerPhase.Move, Center(5, 3), KeyModifiers.None, 1, PointerKind.Mouse);

        sheet.Selecting.Should().BeTrue();
        sheet.Selection.RowCount.Should().Be(5);
        sheet.Selection.ColCount.Should().Be(3);

        model.HandlePointer(PointerPhase.Up, Center(5, 3), KeyModifiers.None, 1, PointerKind.Mouse);
        sheet.Selecting.Should().BeFalse();
    }

    // ---- the keys that bring the keyboard to it ---------------------------------------------------------------

    [Theory]
    [InlineData("F5", KeyModifiers.None, KeyboardConvention.Standard)]
    [InlineData("g", KeyModifiers.Command, KeyboardConvention.Standard)]
    [InlineData("F5", KeyModifiers.None, KeyboardConvention.Apple)]
    [InlineData("g", KeyModifiers.Control, KeyboardConvention.Apple)]
    public void GoTo_GivesTheNameBoxTheKeyboard_OnlyWhileOneIsPlaced(string key, KeyModifiers modifiers,
        KeyboardConvention convention)
    {
        var sheet = new SheetController(200, 8);
        ISheetSurfaceModel model = sheet;

        model.HandleKey(key, modifiers, convention, null).Should().BeFalse("with no name box the key is the host's");

        sheet.NameBox.Placed();
        var version = sheet.NameBox.FocusVersion;
        model.HandleKey(key, modifiers, convention, null).Should().BeTrue();
        sheet.NameBox.FocusVersion.Should().BeGreaterThan(version);

        sheet.NameBox.Removed();
        model.HandleKey(key, modifiers, convention, null).Should().BeFalse();
    }
}
