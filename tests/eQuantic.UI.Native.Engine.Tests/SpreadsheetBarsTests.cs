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
/// told. And the status bar under the sheet (#984), which sums what is selected in the page's language
/// and culture and says when add mode is on.
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
        page.Add(new StatusBar(sheet));
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
    public void ASelectionInTheBar_IsDrawnUnderTheBarsText()
    {
        var (host, sheet) = Open();
        sheet.Document.SetCell(B2, "Budget");
        Click(host, sheet, B2, 32);
        var bar = Region<SheetFormulaBar>(host);
        host.PressDown(bar.Right - 4, bar.Y + bar.Height / 2);
        host.PressUp(bar.Right - 4, bar.Y + bar.Height / 2);
        Frame(host, 48);

        host.KeyDown("ArrowLeft", KeyModifiers.Shift);
        host.KeyDown("ArrowLeft", KeyModifiers.Shift);
        var frame = Frame(host, 64);

        sheet.Entry.Document.TextIn(sheet.Entry.Selection).Should().Be("et");
        var selection = ((IAppTheme)PhotonTheme.Instance).Selection;
        var bands = Descendants(frame.Root)
            .Where(node => node.Source is Box { Style.Background: { } ink } && ink == selection)
            .Select(node => node.Bounds).ToList();
        bands.Should().ContainSingle("the bar draws the band, and the cell's editor, which holds no caret, does not");
        bands[0].Y.Should().BeInRange(bar.Y, bar.Bottom, "the band stands in the bar, on the bar's own grid");
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

    private static int NameBoxStop(PhotonHost host) =>
        host.Semantics().Single(node => node.Label == SdkStrings.NameBox).Id;

    [Fact]
    public void TabThenEnter_GivesTheNameBoxTheKeyboard_AndItGoesWhereItIsTold()
    {
        var (host, sheet) = Open();
        var box = NameBoxStop(host);
        for (var i = 0; i < 4 && host.FocusedId != box; i++) host.KeyDown("Tab");
        host.FocusedId.Should().Be(box, "Tab arrives at the name box");

        host.KeyDown("Enter").Should().BeTrue();
        Frame(host, 32);
        sheet.NameBox.Typing.Should().BeTrue("Enter enters the box, and the box heard it");
        host.TextInput("D4");
        host.KeyDown("Enter").Should().BeTrue();
        Frame(host, 48);
        Frame(host, 64);

        sheet.ActiveCell.Should().Be(new CellRef(3, 3));
    }

    [Fact]
    public void AReadersActivation_GivesTheNameBoxTheKeyboard()
    {
        var (host, sheet) = Open();

        host.Activate(NameBoxStop(host)).Should().BeTrue();
        Frame(host, 32);

        sheet.NameBox.Typing.Should().BeTrue("activating is entering, as a press is");
        host.TextInput("C3");
        sheet.NameBox.Entry.Document.Text.Should().Be("C3");
    }

    [Fact]
    public void APressInTheBlankOfAnEmptyBar_EditsTheCell()
    {
        var (host, sheet) = Open();
        Click(host, sheet, B2, 32);
        Frame(host, 48);

        // The bar runs from beside the name box to the page's edge; its text is empty.
        var bar = Region<SheetFormulaBar>(host);
        host.PressDown(650, bar.Y + bar.Height / 2);
        host.PressUp(650, bar.Y + bar.Height / 2);
        Frame(host, 64);

        host.CodeTarget.Should().NotBeNull("a press anywhere in the bar lands in it");
        sheet.EntryView.Should().Be(SheetEntryView.FormulaBar);
    }

    [Fact]
    public void AReferenceWiderThanTheNameBox_ScrollsToKeepTheCaretInView()
    {
        var (host, sheet) = Open();
        host.Activate(NameBoxStop(host)).Should().BeTrue();
        var time = 32f;
        Frame(host, time);

        foreach (var typed in "$A$100000:$CV$100000")
        {
            host.TextInput(typed.ToString());
            Frame(host, time += 16);
        }

        sheet.NameBox.Entry.Document.Text.Should().Be("$A$100000:$CV$100000");
        var caret = host.CaretRect().Should().NotBeNull().And.Subject.As<Rect?>()!.Value;
        caret.X.Should().BeInRange(0, NameBox.DefaultWidth, "the box scrolls its text to keep the caret in it");
    }

    // ---- the status bar ---------------------------------------------------------------------------------------

    private static T InCulture<T>(string culture, Func<T> run)
    {
        var (format, ui) = (System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CultureInfo.CurrentUICulture);
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
            return run();
        }
        finally
        {
            (System.Globalization.CultureInfo.CurrentCulture, System.Globalization.CultureInfo.CurrentUICulture) = (format, ui);
        }
    }

    [Fact]
    public void TheStatusBar_SumsADragInThePagesLanguageAndCulture()
    {
        var texts = InCulture("pt-BR", () =>
        {
            var (host, sheet) = Open();
            sheet.Document.SetCell(B2, "7,01");
            sheet.Document.SetCell(new CellRef(2, 1), "14,02");
            sheet.Document.SetCell(new CellRef(3, 1), "texto");
            var (x, y) = Center(host, sheet, B2);
            var (toX, toY) = Center(host, sheet, new CellRef(3, 1));
            host.PressDown(x, y);
            host.PointerMove(toX, toY);
            host.PressUp(toX, toY);
            return Texts(Frame(host, 32));
        });

        texts.Should().Contain(["Média: 10,515", "Contagem: 3", "Soma: 21,03"]);
    }

    [Fact]
    public void TheStatusBar_SaysNothingOfOneCell_AndFollowsAKey()
    {
        var (one, two) = InCulture("en-US", () =>
        {
            var (host, sheet) = Open();
            sheet.Document.SetCell(B2, "7");
            sheet.Document.SetCell(new CellRef(2, 1), "8");
            Click(host, sheet, B2, 32);
            var one = Texts(Frame(host, 40));
            host.KeyDown("ArrowDown", KeyModifiers.Shift);
            return (one, Texts(Frame(host, 48)));
        });

        one.Should().NotContain(text => text.StartsWith("Sum") || text.StartsWith("Count"), "one cell shows nothing");
        two.Should().Contain(["Average: 7.5", "Count: 2", "Sum: 15"], "a key that stretches the range refreshes the bar");
    }

    [Fact]
    public void TheStatusBar_CountsWords_AndSumsNothingOfThem()
    {
        var texts = InCulture("en-US", () =>
        {
            var (host, sheet) = Open();
            sheet.Document.SetCell(B2, "a");
            sheet.Document.SetCell(new CellRef(2, 1), "b");
            Click(host, sheet, B2, 32);
            host.KeyDown("ArrowDown", KeyModifiers.Shift);
            return Texts(Frame(host, 48));
        });

        texts.Should().Contain("Count: 2");
        texts.Should().NotContain(text => text.StartsWith("Sum") || text.StartsWith("Average"));
    }

    [Fact]
    public void ShiftF8_IsSaidOnTheStatusBar_UntilEscape()
    {
        var (host, sheet) = Open();
        Click(host, sheet, B2, 32);

        host.KeyDown("F8", KeyModifiers.Shift).Should().BeTrue();
        Texts(Frame(host, 48)).Should().Contain(SdkStrings.AddToSelection);

        host.KeyDown("Escape");
        Texts(Frame(host, 64)).Should().NotContain(SdkStrings.AddToSelection);
    }
}
