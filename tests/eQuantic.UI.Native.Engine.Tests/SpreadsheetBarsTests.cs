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
/// The formula bar and the name box as components on Photon (#983), placed over a sheet as Excel lays them
/// out: the bar shows the cell's entry keystroke by keystroke, edits the cell from there and hands the
/// keyboard back, the name box shows where the selection is, takes the keyboard on F5 and goes where it is
/// told.
/// </summary>
public class SpreadsheetBarsTests
{
    private static readonly CellRef B2 = new(1, 1);

    private static (PhotonHost Host, SheetController Sheet) Open()
    {
        var sheet = new SheetController(rows: 40, cols: 6);
        var bars = new Row(gap: 4);
        bars.Add(new NameBox(sheet));
        bars.Add(new Flexible(new FormulaBar(sheet), 1));
        var page = new Column(gap: 4);
        page.Add(bars);
        page.Add(new Flexible(new Spreadsheet(sheet) { Width = SizeValue.Fill, Height = SizeValue.Fill }, 1));
        var host = new PhotonHost(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }, page),
            PhotonTheme.Instance, ThemeMode.Light, 700, 400)
        {
            SmoothScroll = false,
        };
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder(), 16);
        return (host, sheet);
    }

    private static RealizeResult Frame(PhotonHost host, float time) => host.RenderFrame(new DisplayListBuilder(), time);

    private static IEnumerable<Framework.LayoutNode> Descendants(Framework.LayoutNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var below in Descendants(child)) yield return below;
    }

    private static List<string> Texts(RealizeResult frame) =>
        [.. Descendants(frame.Root).Select(node => node.Source).OfType<Text>().Select(text => text.Content)];

    private static Rect Region<TModel>(PhotonHost host) =>
        host.LastFrame!.CodeRegions.Single(region => region.Surface.Model is TModel).Bounds;

    private static Rect Sheet(PhotonHost host) => host.LastFrame!.SheetRegions.Single().Bounds;

    /// <summary>The middle of a cell of the sheet, through its row numbers, its column headers and its border.</summary>
    private static (float X, float Y) Center(PhotonHost host, SheetController sheet, CellRef cell)
    {
        var at = Sheet(host);
        return (at.X + sheet.Grid.HeaderWidth + cell.Col * 96f + 48f, at.Y + sheet.Grid.HeaderHeight + cell.Row * 28f + 14f);
    }

    private static void Click(PhotonHost host, SheetController sheet, CellRef cell, float time)
    {
        var (x, y) = Center(host, sheet, cell);
        host.PressDown(x, y);
        host.PressUp(x, y);
        Frame(host, time);
    }

    [Fact]
    public void TheBarAndTheCell_ShowTheSameEntry_KeystrokeByKeystroke()
    {
        var (host, sheet) = Open();
        Click(host, sheet, B2, 32);

        var time = 48f;
        foreach (var typed in new[] { "a", "ab", "abc" })
        {
            host.TextInput(typed[^1..]);
            var frame = Frame(host, time += 16);
            Texts(frame).Count(text => text == typed).Should().Be(2, $"the bar and the cell's editor both draw `{typed}`");
        }
    }

    [Fact]
    public void APressInTheBar_EditsTheCell_AndEnterHandsTheKeyboardBack()
    {
        var (host, sheet) = Open();
        sheet.Document.SetCell(B2, "10");
        Click(host, sheet, B2, 32);

        var bar = Region<SheetFormulaBar>(host);
        host.PressDown(bar.Right - 4, bar.Y + bar.Height / 2);
        host.PressUp(bar.Right - 4, bar.Y + bar.Height / 2);
        Frame(host, 48);
        host.CodeTarget.Should().NotBeNull("the bar has the keyboard");
        sheet.EntryView.Should().Be(SheetEntryView.FormulaBar);

        host.TextInput("0");
        host.KeyDown("Enter").Should().BeTrue();
        Frame(host, 64);

        sheet.Document.GetCell(B2).Should().Be("100");
        sheet.ActiveCell.Should().Be(new CellRef(2, 1));
        host.SheetTarget.Should().NotBeNull("the grid took the keyboard back");
    }

    [Fact]
    public void TheNameBox_ShowsTheAddress_AndTheSizeOfADrag()
    {
        var (host, sheet) = Open();
        Click(host, sheet, B2, 32);
        Texts(Frame(host, 40)).Should().Contain("B2");

        var (x, y) = Center(host, sheet, B2);
        var (toX, toY) = Center(host, sheet, new CellRef(5, 3));
        host.PressDown(x, y);
        host.PointerMove(toX, toY);
        Texts(Frame(host, 48)).Should().Contain("5R x 3C");

        host.PressUp(toX, toY);
        Texts(Frame(host, 64)).Should().Contain("D6", "after the release the box shows the active cell, where the drag ended");
    }

    [Fact]
    public void F5_GivesTheNameBoxTheKeyboard_AndEnterGoesThere()
    {
        var (host, sheet) = Open();
        Click(host, sheet, B2, 32);

        host.KeyDown("F5").Should().BeTrue();
        Frame(host, 48);

        host.CodeTarget.Should().NotBeNull("the name box took the keyboard");
        sheet.NameBox.Typing.Should().BeTrue();
        sheet.NameBox.Entry.Document.TextIn(sheet.NameBox.Entry.Selection).Should().Be("B2");

        host.TextInput("D4");
        host.KeyDown("Enter").Should().BeTrue();
        Frame(host, 64);
        Frame(host, 80);

        sheet.ActiveCell.Should().Be(new CellRef(3, 3));
        host.SheetTarget.Should().NotBeNull("the grid took the keyboard back");
    }

    [Fact]
    public void ANameThatNamesNothing_KeepsTheBox_MarkedInvalid()
    {
        var (host, sheet) = Open();
        Click(host, sheet, B2, 32);
        host.KeyDown("F5");
        Frame(host, 48);

        host.TextInput("B0");
        host.KeyDown("Enter");
        Frame(host, 64);

        sheet.NameBox.Invalid.Should().BeTrue();
        host.CodeTarget.Should().NotBeNull("the box keeps the keyboard");
        sheet.ActiveCell.Should().Be(B2);
    }
}
