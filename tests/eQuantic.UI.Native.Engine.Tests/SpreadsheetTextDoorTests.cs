using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using eQuantic.UI.Spreadsheets;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The spreadsheet's text door on Photon (#983, D5): what the shells' text input clients report, the marked
/// text of a composition, its commit and an emoji, reaches a sheet's entry through the host; the input
/// method's window is anchored at the active cell and then at the caret; a sheet asks for the keyboard back;
/// and Escape then Tab leaves a sheet in a page and in a dialog (#732). Driven through the host the way a
/// window drives it, with the real component.
/// </summary>
public class SpreadsheetTextDoorTests
{
    private static readonly CellRef B2 = new(1, 1);

    private static (PhotonHost Host, SheetController Sheet) Open(VisualNode? around = null, SheetController? sheet = null)
    {
        sheet ??= new SheetController(rows: 40, cols: 6);
        var host = new PhotonHost(around ?? new Spreadsheet(sheet) { Width = SizeValue.Fill, Height = SizeValue.Fill },
            PhotonTheme.Instance, ThemeMode.Light, 600, 300)
        {
            SmoothScroll = false,
        };
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder(), 16);
        return (host, sheet);
    }

    /// <summary>The middle of a sheet's cell, through the component's own headers and its border.</summary>
    private static (float X, float Y) Center(SheetController sheet, CellRef cell)
    {
        var grid = sheet.Grid;
        return (1 + grid.HeaderWidth + cell.Col * 96f + 48f, 1 + grid.HeaderHeight + cell.Row * 28f + 14f);
    }

    private static void Click(PhotonHost host, SheetController sheet, CellRef cell, float time)
    {
        var (x, y) = Center(sheet, cell);
        host.PressDown(x, y);
        host.PressUp(x, y);
        host.RenderFrame(new DisplayListBuilder(), time);
    }

    [Fact]
    public void AComposition_ShowsInTheCell_AndCommitsAsOneEdit()
    {
        var (host, sheet) = Open();
        Click(host, sheet, B2, 32);

        host.SetMarkedText("にほんご").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 48);

        sheet.Editing.Should().BeTrue("a composition at rest begins an entry, as typing does");
        sheet.Entry.Document.Text.Should().Be("にほんご");
        sheet.Entry.Composition.Should().NotBeNull();
        host.MarkedText.Should().Be("にほんご");

        host.CommitText("日本語").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 64);

        sheet.Entry.Document.Text.Should().Be("日本語");
        sheet.Entry.Composition.Should().BeNull();
        host.MarkedText.Should().BeEmpty();
        host.KeyDown("z", KeyModifiers.Command);
        sheet.Entry.Document.Text.Should().BeEmpty("one undo while editing takes the committed word back");
    }

    [Fact]
    public void ACancelledComposition_LeavesTheEntryAsItWas()
    {
        var (host, sheet) = Open();
        Click(host, sheet, B2, 32);
        host.TextInput("a");
        host.TextInput("b");

        host.SetMarkedText("x");
        host.SetMarkedText("");
        host.RenderFrame(new DisplayListBuilder(), 48);

        sheet.Entry.Document.Text.Should().Be("ab");
        sheet.Entry.Caret.Column.Should().Be(2);
    }

    [Fact]
    public void ADeadKeysCommit_AndAnEmoji_LandInTheCell()
    {
        var (host, sheet) = Open();
        Click(host, sheet, B2, 32);

        // A dead key marks its accent, and the next key commits the composed letter, as AppKit reports it.
        host.SetMarkedText("´");
        host.CommitText("á");
        host.SetMarkedText("˜");
        host.CommitText("ã");
        host.TextInput("ç");
        host.TextInput("\U0001F44D\U0001F3FD");

        sheet.Entry.Document.Text.Should().Be("áãç\U0001F44D\U0001F3FD");
        host.KeyDown("Backspace");
        sheet.Entry.Document.Text.Should().Be("áãç", "one Backspace takes the thumbs-up with its skin tone");
        host.KeyDown("Enter");
        sheet.Document.GetCell(B2).Should().Be("áãç");
    }

    [Fact]
    public void TheInputMethodsWindow_OpensAtTheActiveCell_ThenAtTheCaret()
    {
        var (host, sheet) = Open();
        Click(host, sheet, B2, 32);

        var atRest = host.CaretRect().Should().NotBeNull().And.Subject.As<Rect?>()!.Value;
        atRest.Width.Should().Be(96, "before an edit the window anchors on the active cell");
        atRest.Height.Should().Be(28);

        host.TextInput("abc");
        host.RenderFrame(new DisplayListBuilder(), 48);

        var caret = host.CaretRect()!.Value;
        caret.Width.Should().Be(2, "while editing it anchors on the caret");
        caret.X.Should().BeGreaterThan(atRest.X).And.BeLessThan(atRest.Right);
        caret.Y.Should().BeGreaterThanOrEqualTo(atRest.Y);
    }

    [Fact]
    public void TheEntrysCaret_IsPaintedOverTheCell()
    {
        var (host, sheet) = Open();
        Click(host, sheet, B2, 32);
        host.TextInput("abc");
        host.RenderFrame(new DisplayListBuilder(), 48);

        ((ISheetSurfaceModel)sheet).Carets.Should().ContainSingle();
        host.CaretVisible.Should().BeTrue("a key restarts the blink, solid first");
    }

    [Fact]
    public void ASheetThatAsks_TakesTheKeyboardBack()
    {
        var (host, sheet) = Open();
        Click(host, sheet, B2, 32);
        host.EndEditing();
        host.SheetTarget.Should().BeNull();

        sheet.RequestFocus();
        host.Invalidate();
        host.RenderFrame(new DisplayListBuilder(), 48);

        host.SheetTarget.Should().NotBeNull();
    }

    // ---- the keyboard leaves a sheet (#732) -------------------------------------------------------------------

    private static Pressable Button(string label) =>
        new(new Box(new BoxStyle { Width = SizeValue.Fixed(80), Height = SizeValue.Fixed(32) }), () => { })
        {
            Label = label,
        };

    private static VisualNode Sheet(SheetController sheet) =>
        new Spreadsheet(sheet) { Width = SizeValue.Fixed(400), Height = SizeValue.Fixed(200) };

    [Fact]
    public void EscapeThenTab_LeavesASheetInAPage()
    {
        var sheet = new SheetController(rows: 40, cols: 6);
        var page = new Column(gap: 8);
        page.Add(Sheet(sheet));
        page.Add(Button("After"));
        var (host, _) = Open(page, sheet);
        Click(host, sheet, B2, 32);

        host.KeyDown("Escape").Should().BeFalse("nothing to cancel and nothing to close: the key is unclaimed");
        host.KeyDown("Tab").Should().BeTrue();

        (host.Focused?.Label).Should().Be("After");
        sheet.ActiveCell.Should().Be(B2, "the Tab moved the keyboard, not the active cell");
    }

    /// <summary>A dialog that does not close on Escape, holding a sheet between two buttons, over a button
    /// behind it.</summary>
    private static (PhotonHost Host, SheetController Sheet) Dialog()
    {
        var sheet = new SheetController(rows: 40, cols: 6);
        var content = new Column(gap: 8);
        content.Add(Button("Before"));
        content.Add(Sheet(sheet));
        content.Add(Button("After"));
        var page = new Stack();
        page.Add(Button("Behind"));
        page.Add(new Overlay(content) { Label = "Budget" });
        var (host, _) = Open(page, sheet);
        // The dialog places the sheet where it centres its content: a press on the sheet's own region
        // gives it the keyboard, wherever that is.
        var region = host.LastFrame!.SheetRegions.Single().Bounds;
        host.PressDown(region.X + region.Width / 2, region.Y + region.Height / 2);
        host.PressUp(region.X + region.Width / 2, region.Y + region.Height / 2);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.SheetTarget.Should().NotBeNull();
        return (host, sheet);
    }

    [Fact]
    public void EscapeThenTab_LeavesASheetInADialog_ForTheControlAfterIt()
    {
        var (host, sheet) = Dialog();
        var active = sheet.ActiveCell;

        host.KeyDown("Escape");
        host.KeyDown("Tab").Should().BeTrue();

        (host.Focused?.Label).Should().Be("After");
        sheet.ActiveCell.Should().Be(active);
    }

    [Fact]
    public void EscapeThenShiftTab_LeavesASheetInADialog_ForTheControlBeforeIt()
    {
        var (host, sheet) = Dialog();
        var active = sheet.ActiveCell;

        host.KeyDown("Escape");
        host.KeyDown("Tab", KeyModifiers.Shift).Should().BeTrue();

        (host.Focused?.Label).Should().Be("Before");
        sheet.ActiveCell.Should().Be(active);
    }

    [Fact]
    public void WithoutEscape_TabStaysInTheGrid()
    {
        var sheet = new SheetController(rows: 40, cols: 6);
        var page = new Column(gap: 8);
        page.Add(Sheet(sheet));
        page.Add(Button("After"));
        var (host, _) = Open(page, sheet);
        Click(host, sheet, B2, 32);

        host.KeyDown("Tab").Should().BeTrue();

        sheet.ActiveCell.Should().Be(new CellRef(1, 2));
        host.SheetTarget.Should().NotBeNull();
    }
}
