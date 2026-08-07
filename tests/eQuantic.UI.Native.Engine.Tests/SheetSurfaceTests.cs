using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The spreadsheet SURFACE: a click resolves a cell by prefix-sum arithmetic, a drag grows the
/// range, and the keyboard speaks Excel through the shared controller — conducted end-to-end
/// through the host, the way a window drives it.
/// </summary>
public class SheetSurfaceTests
{
    private const float HeaderW = 40f;
    private const float HeaderH = 24f;

    private sealed class Page : Primitives.StatefulComponent
    {
        public SheetController Sheet { get; } = new(rows: 100, cols: 26);

        public override VisualNode Build(ComponentContext context) =>
            new SheetSurface(
                new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }),
                Sheet)
            {
                HeaderWidth = HeaderW,
                HeaderHeight = HeaderH,
                OnChanged = () => SetState(() => { }),
            };
    }

    private sealed class FakeClipboard : ITextClipboard
    {
        public string? Held;
        public void Write(string text) => Held = text;
        public string? Read() => Held;
    }

    private static (PhotonHost Host, Page Page) Open()
    {
        var page = new Page();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 600, 400);
        host.RenderFrame(new DisplayListBuilder());
        return (host, page);
    }

    // Default cells are 96×28; the grid starts past the headers.
    private static (float X, float Y) CenterOf(int row, int col) =>
        (HeaderW + col * 96f + 48f, HeaderH + row * 28f + 14f);

    // ---- the contract ----------------------------------------------------------------------------

    [Fact]
    public void AClick_SelectsTheCellUnderIt()
    {
        var (host, page) = Open();
        var (x, y) = CenterOf(2, 1);

        host.PressDown(x, y);
        host.PressUp(x, y);

        page.Sheet.ActiveCell.Should().Be(new CellRef(2, 1));
        page.Sheet.Selection.IsSingleCell.Should().BeTrue();
    }

    [Fact]
    public void ADrag_GrowsTheRange_AnchorHeld()
    {
        var (host, page) = Open();
        var (x0, y0) = CenterOf(1, 1);
        var (x1, y1) = CenterOf(3, 2);

        host.PressDown(x0, y0);
        host.PointerMove(x1, y1);
        host.PressUp(x1, y1);

        page.Sheet.Selection.Should().Be(new SheetRange(new CellRef(1, 1), new CellRef(3, 2)));
    }

    [Fact]
    public void TheKeyboard_SpeaksExcel()
    {
        var (host, page) = Open();
        var (x, y) = CenterOf(0, 0);
        host.PressDown(x, y);
        host.PressUp(x, y);

        host.KeyDown("ArrowDown", KeyModifiers.None).Should().BeTrue();
        page.Sheet.ActiveCell.Should().Be(new CellRef(1, 0));

        host.KeyDown("ArrowRight", KeyModifiers.Shift);
        page.Sheet.Selection.ColCount.Should().Be(2, "Shift+arrow extends");

        page.Sheet.Document.SetCell(new CellRef(1, 0), "a");
        page.Sheet.Document.SetCell(new CellRef(2, 0), "b");
        page.Sheet.Selection = new SheetRange(new CellRef(1, 0));
        host.KeyDown("ArrowDown", KeyModifiers.Command);
        page.Sheet.ActiveCell.Should().Be(new CellRef(2, 0), "⌘+arrow jumps to the data edge");
    }

    [Fact]
    public void DeleteClears_AndUndoRestores_ThroughTheKeyboard()
    {
        var (host, page) = Open();
        page.Sheet.Document.SetCell(new CellRef(0, 0), "gone");
        var (x, y) = CenterOf(0, 0);
        host.PressDown(x, y);
        host.PressUp(x, y);

        host.KeyDown("Delete", KeyModifiers.None);
        page.Sheet.Document.GetCell(new CellRef(0, 0)).Should().Be("");

        host.KeyDown("z", KeyModifiers.Command);
        page.Sheet.Document.GetCell(new CellRef(0, 0)).Should().Be("gone");
    }

    [Fact]
    public void CopyAndPaste_AreTsvThroughTheClipboard()
    {
        var (host, page) = Open();
        var clipboard = new FakeClipboard();
        host.Clipboard = clipboard;
        page.Sheet.Document.SetCell(new CellRef(0, 0), "a");
        page.Sheet.Document.SetCell(new CellRef(0, 1), "b");

        var (x0, y0) = CenterOf(0, 0);
        host.PressDown(x0, y0);
        var (x1, y1) = CenterOf(0, 1);
        host.PointerMove(x1, y1);
        host.PressUp(x1, y1);

        host.KeyDown("c", KeyModifiers.Command);
        clipboard.Held.Should().Be("a\tb");

        var (x2, y2) = CenterOf(4, 3);
        host.PressDown(x2, y2);
        host.PressUp(x2, y2);
        host.KeyDown("v", KeyModifiers.Command);

        page.Sheet.Document.GetCell(new CellRef(4, 3)).Should().Be("a");
        page.Sheet.Document.GetCell(new CellRef(4, 4)).Should().Be("b");
    }

    [Fact]
    public void ADrag_LeavesTheRangeInTheController_ForTheComponentToPaint()
    {
        // Marks moved into the shared COMPONENT (write-once — both targets paint identically);
        // the surface's job is input, and what a drag leaves behind is controller state.
        var (host, page) = Open();
        var (x, y) = CenterOf(1, 1);
        host.PressDown(x, y);
        host.PointerMove(CenterOf(2, 2).X, CenterOf(2, 2).Y);
        host.PressUp(CenterOf(2, 2).X, CenterOf(2, 2).Y);

        page.Sheet.Selection.RowCount.Should().Be(2);
        page.Sheet.Selection.ColCount.Should().Be(2);
    }
}
