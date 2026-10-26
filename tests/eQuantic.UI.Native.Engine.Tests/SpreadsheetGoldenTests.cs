using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;
using eQuantic.UI.Spreadsheets;

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
    private static (PhotonHost Host, SheetController Sheet) Open(ThemeMode mode = ThemeMode.Light)
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
            PhotonTheme.Instance, mode, 460, 260)
        {
            SmoothScroll = false,
            // A golden is the resting picture: the cell cursor's glide is pinned frame by frame elsewhere
            // (SpreadsheetComponentTests, TableViewportGlideTests).
            ReducedMotion = true,
        };
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder(), 16);
        return (host, sheet);
    }

    /// <summary>
    /// The controller is a model OUTSIDE the tree, and these tests change it directly rather than
    /// through the host's input: so the host is told, as an app's model changing from outside is
    /// told (<see cref="PhotonHost.Invalidate"/>), and the next frame builds the sheet again.
    /// </summary>
    private static void Changed(PhotonHost host) => host.Invalidate();

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
        PressCell(host, sheet, new CellRef(0, 0));
        // A2:B4 drawn up from B4, which is active, beside the fill handle.
        sheet.Selection = new SheetRange(new CellRef(3, 1), new CellRef(1, 0));
        Changed(host);
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
        Changed(host);

        Render(host, "sheet-fill-preview");
    }

    [Fact]
    public void HeaderBands_ShadeWithTheSelection()
    {
        var (host, sheet) = Open();
        PressCell(host, sheet, new CellRef(0, 0));
        sheet.SelectCols(1, 1, add: false);
        Changed(host);
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
        Changed(host);

        Render(host, "sheet-resized");
    }

    [Fact]
    public void TheCellsEditor_PaintsTheEntryAndItsCaret()
    {
        var (host, sheet) = Open();
        PressCell(host, sheet, new CellRef(0, 0));
        host.TextInput("Budget");
        sheet.Editing.Should().BeTrue();
        sheet.Entry.Document.Text.Should().Be("Budget");

        // The assertion beside the image: one caret, after the entry's last character, inside A1.
        var caret = ((ISheetSurfaceModel)sheet).Carets.Should().ContainSingle().Subject;
        var a1 = sheet.EditorBounds;
        sheet.EditorRange.Should().Be(new SheetRange(new CellRef(0, 0)));
        caret.X.Should().BeGreaterThan(a1.X + Spreadsheet.CellPadding.Start).And.BeLessThan(a1.Right);

        Render(host, "sheet-editing-entry");
    }

    [Fact]
    public void AnEntryWiderThanItsCell_CoversItsNeighbours()
    {
        var (host, sheet) = Open();
        PressCell(host, sheet, new CellRef(1, 0));
        host.TextInput("A description longer than its column");
        host.RenderFrame(new DisplayListBuilder(), 50);

        // The editor covers the columns the line needs, and the cells it covers keep their values.
        sheet.EditorRange.RightCol.Should().BeGreaterThan(0);
        sheet.Document.GetCell(new CellRef(1, 1)).Should().Be("7,00");

        Render(host, "sheet-editing-wide");
    }

    [Fact]
    public void AnEntryOfTwoLines_GrowsDown()
    {
        var (host, sheet) = Open();
        PressCell(host, sheet, new CellRef(1, 2));
        host.TextInput("first");
        host.KeyDown("Enter", KeyModifiers.Alt);
        host.TextInput("second");
        host.RenderFrame(new DisplayListBuilder(), 50);

        sheet.Entry.Document.LineCount.Should().Be(2);
        sheet.EditorRange.Should().Be(new SheetRange(new CellRef(1, 2), new CellRef(2, 2)));

        Render(host, "sheet-editing-two-lines");
    }

    [Fact]
    public void AComposition_IsUnderlinedInTheCell()
    {
        var (host, sheet) = Open();
        PressCell(host, sheet, new CellRef(2, 0));
        host.SetMarkedText("にほんご");
        host.RenderFrame(new DisplayListBuilder(), 50);

        sheet.Entry.Composition.Should().NotBeNull();
        sheet.Entry.Document.Text.Should().Be("にほんご");

        Render(host, "sheet-editing-composition");
    }

    [Fact]
    public void TheCellsEditor_InTheDark()
    {
        var (host, sheet) = Open(ThemeMode.Dark);
        PressCell(host, sheet, new CellRef(0, 1));
        host.TextInput("Total");
        host.KeyDown("ArrowLeft");
        host.KeyDown("ArrowLeft", KeyModifiers.Shift);
        host.RenderFrame(new DisplayListBuilder(), 50);

        // Enter mode: the arrow committed `Total` and moved to A1, and Shift stretched the selection back.
        sheet.Editing.Should().BeFalse();
        sheet.Document.GetCell(new CellRef(0, 1)).Should().Be("Total");
        PressCell(host, sheet, new CellRef(0, 1));
        host.KeyDown("F2");
        host.KeyDown("ArrowLeft", KeyModifiers.Shift);
        host.KeyDown("ArrowLeft", KeyModifiers.Shift);
        host.RenderFrame(new DisplayListBuilder(), 66);
        sheet.Entry.Document.TextIn(sheet.Entry.Selection).Should().Be("al");

        Render(host, "sheet-editing-selection-dark");
    }

    private static void PressCell(PhotonHost host, SheetController sheet, CellRef cell)
    {
        var x = 1 + sheet.Grid.HeaderWidth + cell.Col * 96f + 48f;
        var y = 1 + sheet.Grid.HeaderHeight + cell.Row * 28f + 14f;
        host.PressDown(x, y);
        host.PressUp(x, y);
    }

    [Fact]
    public void SelectionBand_Ring_AndFillHandle_InTheDark()
    {
        var (host, sheet) = Open(ThemeMode.Dark);
        PressCell(host, sheet, new CellRef(0, 0));
        // A2:B4 drawn up from B4, which is active, beside the fill handle.
        sheet.Selection = new SheetRange(new CellRef(3, 1), new CellRef(1, 0));
        Changed(host);
        host.RenderFrame(new DisplayListBuilder(), 50);

        host.CursorAt(Spreadsheet.HeaderWidth + 2 * 96f - 2f, Spreadsheet.HeaderHeight + 4 * 28f - 2f)
            .Should().Be(CursorShape.Crosshair);

        Render(host, "sheet-selection-band-dark");
    }

    [Fact]
    public void ARangeScrolledUnderTheHeaderRow_IsCutThere()
    {
        var (host, sheet) = Open();
        PressCell(host, sheet, new CellRef(0, 0));
        sheet.Selection = new SheetRange(new CellRef(5, 1), new CellRef(1, 0));
        Changed(host);
        host.RenderFrame(new DisplayListBuilder(), 50);
        var region = host.LastFrame!.TableRegions.Single();
        host.ScrollBy(region.Bounds.X + 200, region.Bounds.Y + 100, 0, 3 * 28f, stepped: false);
        host.RenderFrame(new DisplayListBuilder(), 66);

        // The assertion beside the image: the grid stands three rows down, so rows 2 and 3 of the band
        // are under the header row and its first row shown is row 4.
        host.LastFrame!.TableRegions.Single().Viewport.Scroll.Offset.Down.Should().Be(3 * 28);
        sheet.CellAt(new Point(Spreadsheet.HeaderWidth + 10, Spreadsheet.HeaderHeight + 1)).Should().Be(new CellRef(3, 0));

        Render(host, "sheet-band-under-header", 82);
    }

    /// <summary>The boxes the table's marks draw, with where they stand: the fills, the outlines and the ring.</summary>
    private static List<(Rect Bounds, BoxStyle Style)> MarkBoxes(PhotonHost host)
    {
        var boxes = new List<(Rect, BoxStyle)>();
        void Walk(Framework.LayoutNode node)
        {
            if (node.Source is TableViewCell { Mark: true } && node.Children.Count > 0 && node.Children[0].Source is Box box)
                boxes.Add((node.Children[0].Bounds, box.Style));
            foreach (var child in node.Children) Walk(child);
        }
        Walk(host.LastFrame!.Root);
        return boxes;
    }

    /// <summary>A cell's rectangle on the surface, the grid unscrolled.</summary>
    private static Rect CellBounds(SheetController sheet, int row, int col) =>
        new(1 + sheet.Grid.HeaderWidth + col * 96f, 1 + sheet.Grid.HeaderHeight + row * 28f, 96f, 28f);

    [Fact]
    public void ARange_IsOneOutlinedRectangle_WithTheActiveCellUnfilled()
    {
        var (host, sheet) = Open();
        PressCell(host, sheet, new CellRef(0, 0));
        // B2:C4 with B2 active: the anchor is the end the active cell stands at.
        sheet.Selection = new SheetRange(new CellRef(1, 1), new CellRef(3, 2));
        Changed(host);
        host.RenderFrame(new DisplayListBuilder(), 50);

        // The assertion beside the image: two fills (the rows under B2, and C2 beside it), none over B2, and one
        // outline around the whole range.
        var marks = MarkBoxes(host);
        var fills = marks.Where(mark => mark.Style.Background is not null).ToList();
        fills.Should().HaveCount(2);
        var b2 = CellBounds(sheet, 1, 1);
        fills.Should().NotContain(fill => fill.Bounds.Contains(b2.Center), "the active cell is left unfilled");
        marks.Should().ContainSingle(mark => mark.Style.BorderWidth == 1.5f)
            .Which.Bounds.Should().Be(new Rect(b2.X, b2.Y, 2 * 96f, 3 * 28f));

        Render(host, "sheet-range");
    }

    [Fact]
    public void SeveralRanges_EachAnOutlinedRectangle()
    {
        var (host, sheet) = Open();
        PressCell(host, sheet, new CellRef(1, 0));
        var shift = 1 + sheet.Grid.HeaderWidth + 0 * 96f + 48f;
        host.PressDown(shift, 1 + sheet.Grid.HeaderHeight + 2 * 28f + 14f, modifiers: KeyModifiers.Shift);   // A2:A3
        host.PressUp(shift, 1 + sheet.Grid.HeaderHeight + 2 * 28f + 14f);
        var c = 1 + sheet.Grid.HeaderWidth + 2 * 96f + 48f;
        host.PressDown(c, 1 + sheet.Grid.HeaderHeight + 4 * 28f + 14f, modifiers: KeyModifiers.Command);    // C5
        host.PointerMove(c, 1 + sheet.Grid.HeaderHeight + 6 * 28f + 14f);                                  // to C7
        host.PressUp(c, 1 + sheet.Grid.HeaderHeight + 6 * 28f + 14f);
        host.RenderFrame(new DisplayListBuilder(), 50);

        sheet.Ranges.Should().Equal(new SheetRange(new CellRef(1, 0), new CellRef(2, 0)),
            new SheetRange(new CellRef(4, 2), new CellRef(6, 2)));
        sheet.ActiveCell.Should().Be(new CellRef(4, 2), "the cell the ⌘ drag began on, C5, is active");
        MarkBoxes(host).Count(mark => mark.Style.BorderWidth == 1.5f).Should().Be(2, "one outline a range");

        Render(host, "sheet-ranges");
    }

    [Fact]
    public void TheSelection_IsDimmed_WhileTheGridHasNotTheKeyboard()
    {
        var (host, sheet) = Open();
        sheet.Selection = new SheetRange(new CellRef(1, 1), new CellRef(3, 2));
        Changed(host);
        host.RenderFrame(new DisplayListBuilder(), 50);

        // The assertion beside the image: nothing gave the sheet the keyboard, and its outline and ring are in
        // the theme's strong border, not its focus ring.
        sheet.HasKeyboard.Should().BeFalse();
        var dimmed = ((IAppTheme)PhotonTheme.Instance).BorderStrong;
        MarkBoxes(host).Where(mark => mark.Style.BorderWidth > 0).Should()
            .OnlyContain(mark => mark.Style.BorderColor == dimmed);

        Render(host, "sheet-unfocused");
    }

    [Fact]
    public void AWholeColumn_EmphasizesItsHeaderStrongly()
    {
        var (host, sheet) = Open();
        PressCell(host, sheet, new CellRef(0, 0));
        sheet.SelectCols(2, 2, add: false);
        Changed(host);
        var frame = host.RenderFrame(new DisplayListBuilder(), 50);

        // The assertion beside the image: C's header is drawn in the focus ring, B's in the plain header's colour.
        static IEnumerable<Framework.LayoutNode> All(Framework.LayoutNode node)
        {
            yield return node;
            foreach (var child in node.Children)
                foreach (var below in All(child)) yield return below;
        }
        // A header is a hairline-bordered box whose centred label is the column's name.
        Framework.LayoutNode HeaderOf(string name) => All(frame.Root)
            .Single(node => node.Source is Box { Style.BorderWidth: 0.5f }
                && All(node).Any(below => below.Source is Text { Content: var label } && label == name));
        ((Box)HeaderOf("C").Source!).Style.Background.Should().Be(((IAppTheme)PhotonTheme.Instance).FocusRing);
        ((Box)HeaderOf("B").Source!).Style.Background.Should().Be(((IAppTheme)PhotonTheme.Instance).SurfaceSubtle);

        Render(host, "sheet-whole-column");
    }
}
