using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What a press MEANS on a surface drawn under a scale or a rotation: the caret, the code position,
/// the cell and the canvas's coordinates land under the press, and what the host places by a caret
/// stands where the caret is drawn (#658). Since #513 such a surface takes the pointer where it is
/// drawn, and the host still turned the point into a caret, a position or a cell by subtracting the
/// corner of the box it is drawn in: exact for a translation, and twice the column the press was on
/// for a field drawn twice as large.
/// <para>
/// Each surface is laid out at 200, 200 and drawn twice as large about its centre, so a point
/// <c>(x, y)</c> from its corner is drawn <c>(2x, 2y)</c> from the corner of its box on screen.
/// </para>
/// </summary>
public class TransformedSurfaceTests
{
    /// <summary>8dp per character, so a column is a number a test can name.</summary>
    private sealed class FixedWidthMeasurer : Framework.ITextMeasurer
    {
        public Framework.TextMeasurement Measure(string text, TypeStyle style, float typeScale,
            float maxWidth, int maxLines) =>
            new(text.Length * 8f, style.LineHeight, style.LineHeight,
                [new Framework.MeasuredLine(text.Length * 8f, false)]);
    }

    /// <inheritdoc cref="FixedWidthMeasurer"/>
    private sealed class FixedWidthRasterizer : Framework.ITextRasterizer
    {
        public Framework.TextRaster? Rasterize(string content, TypeStyle style, float typeScale,
            float maxWidth, int maxLines, float scale, TextAlignment align)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var width = Math.Max(1, (int)Math.Min(content.Length * 8 * scale, maxWidth * scale));
            var height = Math.Max(1, (int)(style.LineHeight * scale));
            return new Framework.TextRaster(width, height, new byte[width * height]);
        }
    }

    /// <summary><paramref name="surface"/> in a box of <paramref name="width"/> by
    /// <paramref name="height"/> at 200, 200, drawn under <paramref name="transform"/>.</summary>
    private static PhotonHost Mount(VisualNode surface, Transform2D transform, float width = 200, float height = 100)
    {
        var turned = new Box(new BoxStyle { Width = width, Height = height, Transform = transform }, surface);
        var page = new Box(new BoxStyle { Padding = EdgeInsets.All(200) }, turned);
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 800, 700, new FixedWidthMeasurer())
        {
            TextRasterizer = new FixedWidthRasterizer(),
        };
        // Two frames: a code surface learns how wide its viewport is from the frame before.
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    private static void Click(PhotonHost host, float x, float y)
    {
        host.PressDown(x, y);
        host.PressUp(x, y);
        host.RenderFrame(new DisplayListBuilder());
    }

    private static void Drag(PhotonHost host, float fromX, float fromY, float toX, float toY)
    {
        host.PressDown(fromX, fromY);
        host.PointerMove(toX, toY);
        host.RenderFrame(new DisplayListBuilder());
        host.PressUp(toX, toY);
        host.RenderFrame(new DisplayListBuilder());
    }

    // ---- a field --------------------------------------------------------------------------------

    /// <summary>The caret a press <paramref name="localX"/> into the field puts it at, with the field
    /// drawn under <paramref name="transform"/>, read beside the box the field is drawn in.</summary>
    private static int CaretAfterAPressAt(Transform2D transform, float localX, float scale)
    {
        var host = Mount(new TextEntry("abcdefghij", _ => { }), transform);
        var field = host.LastFrame!.TextRegions.Single().Bounds;
        Click(host, field.X + scale * localX, field.Y + field.Height / 2);
        return host.CaretIndex;
    }

    [Fact]
    public void AFieldDrawnTwiceAsLarge_PutsTheCaretUnderThePress()
    {
        var plain = CaretAfterAPressAt(Transform2D.Translate(0, 0), localX: 25, scale: 1);
        plain.Should().BeInRange(1, 9, "25dp in, the press is between two characters of the field");

        CaretAfterAPressAt(Transform2D.Scale(2), localX: 25, scale: 2)
            .Should().Be(plain, "the press is on the same character, drawn twice as large");
    }

    [Fact]
    public void ADragAcrossAFieldDrawnTwiceAsLarge_SelectsTheCharactersItCrossed()
    {
        (int, int) SelectionAfterADrag(Transform2D transform, float scale)
        {
            var host = Mount(new TextEntry("abcdefghij", _ => { }), transform);
            var field = host.LastFrame!.TextRegions.Single().Bounds;
            var y = field.Y + field.Height / 2;
            Drag(host, field.X + scale * 25, y, field.X + scale * 49, y);
            return host.Selection;
        }

        var plain = SelectionAfterADrag(Transform2D.Translate(0, 0), scale: 1);
        plain.Item2.Should().BeGreaterThan(plain.Item1, "the drag crossed three characters");

        SelectionAfterADrag(Transform2D.Scale(2), scale: 2).Should().Be(plain);
    }

    [Fact]
    public void TheCandidateWindowsAnchor_IsAFieldsCaretAsDrawn()
    {
        var host = Mount(new TextEntry("abcdefghij", _ => { }), Transform2D.Scale(2));
        var field = host.LastFrame!.TextRegions.Single().Bounds;
        Click(host, field.X + 2 * 25, field.Y + field.Height / 2);
        var caret = host.CaretIndex;

        var anchor = host.CaretRect()!.Value;

        anchor.X.Should().BeApproximately(field.X + 2 * caret * 8, 0.01f,
            "the caret is drawn after its characters, each 8dp wide and drawn 16");
        anchor.Width.Should().BeApproximately(4, 0.01f, "the 2dp caret is drawn 4 wide");
    }

    // ---- a code surface -------------------------------------------------------------------------

    private static (PhotonHost Host, CodeSurface Surface, Rect Bounds) OpenCode(string code)
    {
        var host = Mount(new CodeEditor(code, "csharp") { ShowLineNumbers = false }, Transform2D.Scale(2));
        var region = host.LastFrame!.CodeRegions.Single();
        return (host, region.Surface, region.Bounds);
    }

    [Fact]
    public void ACodeSurfaceDrawnTwiceAsLarge_PutsTheCaretOnTheLineAndColumnPressed()
    {
        var (host, surface, bounds) = OpenCode("var a = 1;\nvar b = 2;\nvar c = 3;");
        var grid = surface.Grid();

        // Line 1, column 4, dead centre of a character, so no rounding argument.
        Click(host, bounds.X + 2 * (grid.Origin.X + 4 * grid.Cell.Width),
            bounds.Y + 2 * (grid.Origin.Y + 1.5f * grid.Cell.Height));

        surface.Engine().Caret.Should().Be(new CodePosition(1, 4));
    }

    [Fact]
    public void ADragAcrossACodeSurfaceDrawnTwiceAsLarge_SelectsWhatItCrossed()
    {
        var (host, surface, bounds) = OpenCode("one two three");
        var grid = surface.Grid();
        var y = bounds.Y + 2 * (grid.Origin.Y + grid.Cell.Height / 2);

        Drag(host, bounds.X + 2 * grid.Origin.X, y, bounds.X + 2 * (grid.Origin.X + 7 * grid.Cell.Width), y);

        surface.Engine().Document.TextIn(surface.Engine().Selection).Should().Be("one two");
    }

    [Fact]
    public void TheCandidateWindowsAnchor_IsACodeSurfacesCaretAsDrawn()
    {
        var (host, surface, bounds) = OpenCode("var a = 1;\nvar b = 2;");
        var grid = surface.Grid();
        Click(host, bounds.X + 2 * (grid.Origin.X + 4 * grid.Cell.Width),
            bounds.Y + 2 * (grid.Origin.Y + 1.5f * grid.Cell.Height));
        var caret = surface.Model.Carets[0];

        host.CaretRect().Should().Be(new Rect(bounds.X + 2 * caret.X, bounds.Y + 2 * caret.Y,
            2 * caret.Width, 2 * caret.Height));
    }

    // ---- what a code surface offers ---------------------------------------------------------------

    /// <summary>Offers what it was made with, whatever was typed: the list is the view's to test.</summary>
    private sealed class ListProvider(params CodeCompletionItem[] items) : ICodeCompletionProvider
    {
        public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
            CodeCompletionContext context, CancellationToken cancellation) =>
            Task.FromResult(new CodeCompletionList(items));
    }

    /// <summary>A code editor in a box of 300 by 200 at 200, 200, turned <paramref name="degrees"/>
    /// about its centre, under a pointer, whose rows are no taller than the list.</summary>
    private static PhotonHost MountList(float degrees)
    {
        var host = Mount(new CodeEditor("var Column = 1;\n        \n\n\n\n\n\n", "csharp")
        {
            ShowLineNumbers = false,
            Completions = [new ListProvider(new CodeCompletionItem("Column"), new CodeCompletionItem("ColorToken"))],
        }, Transform2D.Rotate(degrees), width: 300, height: 200);
        host.Density = Density.Compact;
        return host;
    }

    /// <summary>Where <paramref name="point"/>, laid out in the box of <see cref="MountList"/>, is
    /// drawn with the box turned <paramref name="degrees"/> about its centre, (350, 300).</summary>
    private static Point Turned(Point point, float degrees)
    {
        var drawn = Matrix2D.Rotation(degrees * MathF.PI / 180).Transform(new Point(point.X - 350, point.Y - 300));
        return new Point(350 + drawn.X, 300 + drawn.Y);
    }

    /// <summary>Types <paramref name="text"/> and settles the frames its list needs.</summary>
    private static CodeRegion Type(PhotonHost host, string text)
    {
        foreach (var c in text) host.TextInput(c.ToString());
        host.RenderFrame(new DisplayListBuilder());
        return host.RenderFrame(new DisplayListBuilder()).CodeRegions.Single();
    }

    /// <summary>
    /// What a code surface offers at its caret takes the pointer in its own shape, as the surface does
    /// (Copilot on #690): the box around a turned list holds corners no row is drawn in, and the code
    /// drawn there lost its beam and its presses to the list.
    /// </summary>
    [Fact]
    public void AListOnATurnedCodeSurface_TakesThePointerInItsOwnShape()
    {
        // Laid out unturned first, to read where the code and its list stand in the space they keep.
        var plain = MountList(0);
        var region = plain.LastFrame!.CodeRegions.Single();
        var code = region.Bounds;
        var grid = region.Surface.Grid();
        var word = new Point(code.X + grid.Origin.X + 9.5f * grid.Cell.Width,
            code.Y + grid.Origin.Y + 1.5f * grid.Cell.Height);   // past the end of line 1
        Click(plain, word.X, word.Y);
        var list = Type(plain, "Co").Offered!.Value;

        const float degrees = 30;
        var host = MountList(degrees);
        var surface = host.LastFrame!.CodeRegions.Single().Surface;
        var at = Turned(word, degrees);
        Click(host, at.X, at.Y);
        var box = Type(host, "Co").Offered!.Value;
        surface.Engine().Caret.Should().Be(new CodePosition(1, 10), "the press put the caret after line 1 and the word followed");

        // The corner of the box around the turned list that lies farthest from the list, once taken
        // back into the surface's space, and inside the code: no row reaches it, even with a target's
        // margin.
        float Outside(Point local) =>
            MathF.Max(MathF.Max(list.Left - local.X, local.X - list.Right), MathF.Max(list.Top - local.Y, local.Y - list.Bottom));
        var corner = new Point[]
            {
                new(box.Left + 2, box.Top + 2), new(box.Right - 2, box.Top + 2),
                new(box.Left + 2, box.Bottom - 2), new(box.Right - 2, box.Bottom - 2),
            }
            .Where(p => code.Contains(Turned(p, -degrees)))
            .MaxBy(p => Outside(Turned(p, -degrees)));
        Outside(Turned(corner, -degrees)).Should().BeGreaterThan(8, "the corner is well outside the list, in its own space");

        host.CursorAt(corner.X, corner.Y).Should().Be(CursorShape.Text, "the code is drawn there, and no row of the list");
        Click(host, corner.X, corner.Y);
        surface.Engine().Caret.Should().NotBe(new CodePosition(1, 10), "the press landed on the code and moved its caret");
    }

    // ---- a spreadsheet --------------------------------------------------------------------------

    private const float Col = SheetDocument.DefaultColWidth;
    private const float Row = SheetDocument.DefaultRowHeight;

    private static (PhotonHost Host, SheetController Sheet, Rect Bounds) OpenSheet(Transform2D transform)
    {
        var sheet = new SheetController(rows: 10, cols: 4);
        var host = Mount(new SheetSurface(new Box(new BoxStyle { Width = 200, Height = 200 }), sheet),
            transform, height: 200);
        return (host, sheet, host.LastFrame!.SheetRegions.Single().Bounds);
    }

    [Fact]
    public void ASheetDrawnTwiceAsLarge_SelectsTheCellPressed()
    {
        var (host, sheet, bounds) = OpenSheet(Transform2D.Scale(2));

        // The middle of row 2, column 1.
        Click(host, bounds.X + 2 * 1.5f * Col, bounds.Y + 2 * 2.5f * Row);

        sheet.Selection.Focus.Should().Be(new CellRef(2, 1));
    }

    [Fact]
    public void ADragAcrossASheetDrawnTwiceAsLarge_StretchesTheRangeToTheCellUnderThePointer()
    {
        var (host, sheet, bounds) = OpenSheet(Transform2D.Scale(2));

        Drag(host, bounds.X + 2 * 1.5f * Col, bounds.Y + 2 * 2.5f * Row,
            bounds.X + 2 * 0.5f * Col, bounds.Y + 2 * 3.5f * Row);

        sheet.Selection.Should().Be(new SheetRange(new CellRef(2, 1), new CellRef(3, 0)));
    }

    [Fact]
    public void ASheetDrawnTwiceAsLarge_GivesItsFillHandleWhereTheHandleIsDrawn()
    {
        var (host, sheet, bounds) = OpenSheet(Transform2D.Scale(2));

        // The bottom-right corner of the selected cell, A1, as drawn.
        host.PressDown(bounds.X + 2 * Col, bounds.Y + 2 * Row);

        sheet.Filling.Should().BeTrue("the press grabbed the handle drawn at the cell's corner");
    }

    [Fact]
    public void AFillDraggedOnASheetDrawnTwiceAsLarge_ReachesTheCellUnderThePointer()
    {
        var (host, sheet, bounds) = OpenSheet(Transform2D.Scale(2));

        host.PressDown(bounds.X + 2 * Col, bounds.Y + 2 * Row);
        host.PointerMove(bounds.X + 2 * 0.5f * Col, bounds.Y + 2 * 2.5f * Row);

        sheet.FillTarget.Should().Be(new SheetRange(new CellRef(1, 0), new CellRef(2, 0)),
            "the pointer is over row 2 of the first column, as drawn");
    }

    [Fact]
    public void ASheetTurnedAQuarter_SelectsTheCellPressed_AlongItsOwnAxes()
    {
        // A square sheet turned about its centre covers the same square, its rows now running down
        // the screen's columns: a press is tested along the sheet's own axes, not the screen's.
        var (host, sheet, bounds) = OpenSheet(Transform2D.Rotate(90));
        var local = new Point(1.5f * Col, 2.5f * Row);   // the middle of row 2, column 1
        var fromCentre = new Point(local.X - 100, local.Y - 100);
        var drawn = Matrix2D.Rotation(MathF.PI / 2).Transform(fromCentre);

        Click(host, bounds.Center.X + drawn.X, bounds.Center.Y + drawn.Y);

        sheet.Selection.Focus.Should().Be(new CellRef(2, 1));
    }

    // ---- a reveal -------------------------------------------------------------------------------

    /// <summary>
    /// How far a scroll view scrolled to reveal the sixth of ten controls Tab reached, drawn under
    /// <paramref name="transform"/>. A scroll offset is a distance in the space the scroll view was
    /// laid out in, so a scroll view drawn twice as large scrolls exactly as far: measured on screen,
    /// it scrolled twice as far as it had to.
    /// </summary>
    private static float ScrolledToTheSixthControl(Transform2D transform)
    {
        var column = new Column(gap: 0) { Width = SizeValue.Fill };
        for (var i = 0; i < 10; i++)
            column.Add(new Pressable(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 40 }), () => { }));
        var host = Mount(new ScrollView(column) { Width = SizeValue.Fill, Height = SizeValue.Fixed(100) }, transform);

        for (var i = 0; i < 6; i++)
        {
            host.KeyDown("Tab");
            host.RenderFrame(new DisplayListBuilder());
        }
        return host.ScrollOffsetOf(host.LastFrame!.ScrollRegions.Single(r => r.MaxOffset > 0).Path);
    }

    [Fact]
    public void AScrollViewDrawnTwiceAsLarge_RevealsAFocusedControlAsFarAsItWouldUnscaled()
    {
        var plain = ScrolledToTheSixthControl(Transform2D.Translate(0, 0));
        plain.Should().BeGreaterThan(0, "the sixth control is below the view");

        ScrolledToTheSixthControl(Transform2D.Scale(2)).Should().BeApproximately(plain, 0.01f);
    }

    /// <summary>How far a code editor's own view scrolled to follow a caret twenty lines down,
    /// with the editor drawn under <paramref name="transform"/>, <paramref name="scale"/> times as
    /// large.</summary>
    private static float ScrolledToFollowTheCaret(Transform2D transform, float scale)
    {
        var code = string.Join("\n", Enumerable.Range(0, 80).Select(i => $"line {i}"));
        var host = Mount(new CodeEditor(code, "csharp") { ShowLineNumbers = false, MaxHeight = 100 }, transform,
            width: 300);
        var region = host.LastFrame!.CodeRegions.Single();
        var grid = region.Surface.Grid();
        Click(host, region.Bounds.X + scale * (grid.Origin.X + grid.Cell.Width),
            region.Bounds.Y + scale * (grid.Origin.Y + grid.Cell.Height / 2));

        for (var i = 0; i < 20; i++)
        {
            host.KeyDown("ArrowDown");
            host.RenderFrame(new DisplayListBuilder());
        }
        host.RenderFrame(new DisplayListBuilder());
        var viewport = host.LastFrame!.ScrollRegions.First(r => r.Axis == ScrollAxis.Vertical
            && region.Path.StartsWith(r.Path, StringComparison.Ordinal) && r.MaxOffset > 0);
        return host.ScrollOffsetOf(viewport.Path);
    }

    [Fact]
    public void ACodeEditorDrawnTwiceAsLarge_FollowsItsCaretAsFarAsItWouldUnscaled()
    {
        var plain = ScrolledToFollowTheCaret(Transform2D.Translate(0, 0), scale: 1);
        plain.Should().BeGreaterThan(0, "the caret went twenty lines down a view of five");

        ScrolledToFollowTheCaret(Transform2D.Scale(2), scale: 2).Should().BeApproximately(plain, 0.01f);
    }

    // ---- a canvas -------------------------------------------------------------------------------

    [Fact]
    public void ACanvasDrawnTwiceAsLarge_HearsThePointerInItsOwnCoordinates()
    {
        var seen = new List<CanvasPointer>();
        var host = Mount(new Canvas(_ => { }) { OnPointerDown = seen.Add, OnPointerUp = seen.Add },
            Transform2D.Scale(2), width: 100, height: 100);
        var bounds = host.LastFrame!.CanvasRegions.Single().Bounds;

        Click(host, bounds.X + 2 * 30, bounds.Y + 2 * 40);

        seen.Select(p => (p.X, p.Y)).Should().Equal([(30f, 40f), (30f, 40f)],
            "the press is 30 and 40 into the canvas as drawn, twice as large");
    }
}
