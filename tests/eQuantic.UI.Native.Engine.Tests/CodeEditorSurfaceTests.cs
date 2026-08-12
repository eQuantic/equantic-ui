using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// EDITING code with a keyboard and a mouse, through the host — the half the model cannot test.
/// <para>
/// Every test renders a frame between the events, and that is the point rather than ceremony: each
/// keystroke calls back into the app, the app calls SetState, and Build hands back brand new nodes.
/// Anything the host remembers as an OBJECT is pointing at a corpse by the time the next key
/// arrives, which is why the surface is resolved by PATH and carries a controller that is not.
/// </para>
/// </summary>
public class CodeEditorSurfaceTests
{
    /// <summary>8dp per character, so a column is a number a test can name.</summary>
    private sealed class FixedWidthRasterizer : Framework.ITextRasterizer
    {
        public Framework.TextRaster? Rasterize(string content, TypeStyle style, float typeScale,
            float maxWidth, int maxLines, float scale)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var width = Math.Max(1, (int)Math.Min(content.Length * 8 * scale, maxWidth * scale));
            var height = Math.Max(1, (int)(style.LineHeight * scale));
            return new Framework.TextRaster(width, height, new byte[width * height]);
        }
    }

    private sealed class FixedWidthMeasurer : Framework.ITextMeasurer
    {
        public Framework.TextMeasurement Measure(string text, TypeStyle style, float typeScale,
            float maxWidth, int maxLines) =>
            new(text.Length * 8f, style.LineHeight, style.LineHeight,
                [new Framework.MeasuredLine(text.Length * 8f, false)]);
    }

    private static (PhotonHost Host, CodeSurface Surface, Rect Bounds) Open(string code)
    {
        var editor = new CodeEditor(code, "csharp") { ShowLineNumbers = false };
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, 400, 300,
            new FixedWidthMeasurer())
        {
            TextRasterizer = new FixedWidthRasterizer(),
        };
        // TWO frames. How wide the viewport turned out to be is layout's answer, not the author's,
        // so it comes back the way its height does — reported from the realized frame and folded in
        // on the next one. The first frame is as wide as the CODE; from the second the surface is
        // as wide as the space you can click in, which is what a pointer test is about. A host
        // renders continuously, so this is the steady state, not a special case.
        host.RenderFrame(new DisplayListBuilder());
        var frame = host.RenderFrame(new DisplayListBuilder());
        var region = frame.CodeRegions.Single();
        return (host, region.Surface, region.Bounds);
    }

    private static void Press(PhotonHost host, string key, KeyModifiers modifiers = KeyModifiers.None)
    {
        host.KeyDown(key, modifiers);
        host.RenderFrame(new DisplayListBuilder());
    }

    private static void Type(PhotonHost host, string text)
    {
        foreach (var c in text)
        {
            host.TextInput(c.ToString());
            host.RenderFrame(new DisplayListBuilder());
        }
    }

    // ---- reaching it -----------------------------------------------------------------------------

    [Fact]
    public void AClickPutsTheCaretWhereItWasAimed()
    {
        var (host, surface, bounds) = Open("var a = 1;\nvar b = 2;\nvar c = 3;");

        // Line 1, column 4 — dead centre of a character, so no rounding argument.
        host.PressDown(bounds.X + surface.ContentLeft + 4 * surface.ColumnWidth,
            bounds.Y + surface.ContentTop + 1 * surface.LineHeight + surface.LineHeight / 2);
        host.PressUp(bounds.X, bounds.Y);
        host.RenderFrame(new DisplayListBuilder());

        surface.Editor.Caret.Should().Be(new CodePosition(1, 4));
    }

    [Fact]
    public void APointerPastTheEndOfALineLandsAtItsEnd_NotOutsideTheDocument()
    {
        var (host, surface, bounds) = Open("short\nlonger line here");

        host.PressDown(bounds.X + surface.ContentLeft + 200, bounds.Y + surface.ContentTop + 2);
        host.PressUp(bounds.X, bounds.Y);
        host.RenderFrame(new DisplayListBuilder());

        surface.Editor.Caret.Should().Be(new CodePosition(0, "short".Length));
    }

    [Fact]
    public void DraggingDrawsASelection_AndItSurvivesTheRebuildEachFrameCauses()
    {
        var (host, surface, bounds) = Open("one two three");
        var y = bounds.Y + surface.ContentTop + surface.LineHeight / 2;

        host.PressDown(bounds.X + surface.ContentLeft, y);
        host.PointerMove(bounds.X + surface.ContentLeft + 7 * surface.ColumnWidth, y);
        host.RenderFrame(new DisplayListBuilder());
        host.PressUp(bounds.X + surface.ContentLeft + 7 * surface.ColumnWidth, y);

        surface.Editor.Document.TextIn(surface.Editor.Selection).Should().Be("one two");
    }

    // ---- typing ----------------------------------------------------------------------------------

    [Fact]
    public void TypingReachesTheDocument_AndTheAppHearsAboutIt()
    {
        var changes = new List<string>();
        var editor = new CodeEditor("", "csharp")
        {
            ShowLineNumbers = false,
            OnChanged = changes.Add,
        };
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, 400, 300)
        {
            TextRasterizer = new FixedWidthRasterizer(),
        };
        var frame = host.RenderFrame(new DisplayListBuilder());
        var region = frame.CodeRegions.Single();
        host.PressDown(region.Bounds.X + 4, region.Bounds.Y + 4);
        host.PressUp(region.Bounds.X + 4, region.Bounds.Y + 4);

        Type(host, "var x");

        region.Surface.Editor.Document.Text.Should().Be("var x");
        // One per character, plus the click that put the caret there: the seam reports MOVEMENT as
        // well as change, because a status bar showing line:column needs both.
        changes.Should().HaveCount(6);
        changes[^1].Should().Be("var x");
    }

    [Fact]
    public void EnterMakesALine_AndKeepsTheIndentation()
    {
        var (host, surface, bounds) = Open("    if (x) {");
        Focus(host, surface, bounds);
        Press(host, "End");
        Press(host, "Enter");

        surface.Editor.Document.Lines.Should().Equal(["    if (x) {", "        "],
            "a block opened, so the body steps in");
    }

    [Fact]
    public void TabIndentsInsteadOfLeaving()
    {
        var (host, surface, bounds) = Open("one\ntwo");
        Focus(host, surface, bounds);
        Press(host, "a", KeyModifiers.Command);   // everything
        Press(host, "Tab");

        surface.Editor.Document.Lines.Should().Equal("    one", "    two");
    }

    [Fact]
    public void UndoTakesBackARunOfTyping_ThroughTheHost()
    {
        var (host, surface, bounds) = Open("");
        Focus(host, surface, bounds);
        Type(host, "hello");

        Press(host, "z", KeyModifiers.Command);

        surface.Editor.Document.Text.Should().BeEmpty();

        Press(host, "z", KeyModifiers.Command | KeyModifiers.Shift);
        surface.Editor.Document.Text.Should().Be("hello", "and redo puts it back");
    }

    [Fact]
    public void ArrowsMoveTheCaret_AndShiftExtendsFromWhereItWas()
    {
        var (host, surface, bounds) = Open("abcdef");
        Focus(host, surface, bounds);
        Press(host, "ArrowRight");
        Press(host, "ArrowRight");
        Press(host, "ArrowRight", KeyModifiers.Shift);
        Press(host, "ArrowRight", KeyModifiers.Shift);

        surface.Editor.Document.TextIn(surface.Editor.Selection).Should().Be("cd");
    }

    [Fact]
    public void EscapeLEAVESTheEditor_RatherThanBeingSwallowed()
    {
        var (host, surface, bounds) = Open("code");
        Focus(host, surface, bounds);
        host.FocusedPath.Should().NotBeNull();

        Press(host, "Escape");
        host.FocusedPath.Should().BeNull("an editor that traps Escape is one you cannot get out of");
    }

    [Fact]
    public void AReadOnlyEditorTakesTheCaretButNotTheKeys()
    {
        var editor = new CodeEditor("fixed", "csharp") { ShowLineNumbers = false, ReadOnly = true };
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, 400, 300)
        {
            TextRasterizer = new FixedWidthRasterizer(),
        };
        var frame = host.RenderFrame(new DisplayListBuilder());
        var region = frame.CodeRegions.Single();
        host.PressDown(region.Bounds.X + 4, region.Bounds.Y + 4);
        host.PressUp(region.Bounds.X + 4, region.Bounds.Y + 4);

        Type(host, "x");
        Press(host, "Enter");

        region.Surface.Editor.Document.Text.Should().Be("fixed", "reading is not writing");
        // …but the caret is there, because selecting and copying are reading.
        host.FocusedPath.Should().NotBeNull();
    }

    // ---- what is DRAWN ----------------------------------------------------------------------------

    [Fact]
    public void TheCaretAndTheSelectionAreDrawnWhereTheModelSaysTheyAre()
    {
        var (host, surface, bounds) = Open("one two three\nsecond line");
        Focus(host, surface, bounds);
        Press(host, "ArrowRight");
        Press(host, "ArrowRight");
        Press(host, "ArrowRight", KeyModifiers.Shift);

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 0);
        var commands = builder.Build().Commands.ToArray();

        var caret = commands.Last(c => c.Kind == DrawCommandKind.FillRRect
            && MathF.Abs(c.Shape.Rect.Width - 2f) < 0.01f);
        caret.Shape.Rect.X.Should().BeApproximately(
            bounds.X + surface.ContentLeft + 3 * surface.ColumnWidth, 0.01f);
        caret.Shape.Rect.Y.Should().BeApproximately(bounds.Y + surface.ContentTop, 0.01f);

        var band = commands.First(c => c.Kind == DrawCommandKind.FillRRect
            && MathF.Abs(c.Shape.Rect.Width - surface.ColumnWidth) < 0.01f
            && MathF.Abs(c.Shape.Rect.Height - surface.LineHeight) < 0.01f);
        band.Shape.Rect.X.Should().BeApproximately(
            bounds.X + surface.ContentLeft + 2 * surface.ColumnWidth, 0.01f);
    }

    /// <summary>
    /// A selection across lines is a BAND PER LINE, not one rectangle: a single rectangle over the
    /// range would cover the indentation of lines the range never touched.
    /// </summary>
    [Fact]
    public void AMultiLineSelectionDrawsOneBandPerLine()
    {
        var (host, surface, bounds) = Open("first line\nsecond\nthird line");
        Focus(host, surface, bounds);
        Press(host, "a", KeyModifiers.Command);

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 0);
        // Past the gutter: the active-line WASH is the same height and spans the whole row, and
        // it is not what this is about.
        var bands = builder.Build().Commands.ToArray()
            .Where(c => c.Kind == DrawCommandKind.FillRRect
                && MathF.Abs(c.Shape.Rect.Height - surface.LineHeight) < 0.01f
                && c.Shape.Rect.X >= bounds.X + surface.ContentLeft - 0.01f)
            .ToArray();

        bands.Select(b => MathF.Round(b.Shape.Rect.Y)).Distinct().Should().HaveCount(3,
            "one per line of the range");
        bands[0].Shape.Rect.Y.Should().BeApproximately(bounds.Y + surface.ContentTop, 0.01f);
        bands[2].Shape.Rect.Y.Should().BeApproximately(
            bounds.Y + surface.ContentTop + 2 * surface.LineHeight, 0.01f);
    }

    /// <summary>
    /// NOTHING SCROLLS INSIDE THE SURFACE. The caret and the selection are painted against the
    /// surface's own box, so anything that scrolled beneath it would move the code out from under
    /// them: scroll a long line sideways and the text travels while the marks stay put, and a
    /// pointer is read at the column it would have hit had nothing scrolled. It shipped that way —
    /// the block scrolled itself, and every mark in an editor was a line and a half off once you
    /// went right. The viewport belongs OUTSIDE the surface, so the surface travels with the code
    /// and the arithmetic is true wherever the file is scrolled to.
    /// </summary>
    [Fact]
    public void TheViewportIsOutsideTheSurface_SoTheMarksTravelWithTheCode()
    {
        var editor = new CodeEditor("short\n" + new string('x', 400), "csharp") { MaxHeight = 200 };
        var built = editor.Build(new ComponentContext(PhotonTheme.Instance));

        var surface = FindSurface(built);
        surface.Should().NotBeNull("the editor is built around one");
        ScrollersUnder(surface!.Child).Should().Be(0,
            "a scroll view under the surface puts the code in one coordinate space and the marks "
            + "in another — CodeBlock.Standalone is what keeps it out");

        // …and the scrolling did not simply disappear: both axes are still there, above it.
        ScrollersUnder(built).Should().BeGreaterThanOrEqualTo(2, "sideways for long lines, down for a long file");
    }

    /// <summary>
    /// THE GUTTER IS OUTSIDE THE SIDEWAYS SCROLL, and the code is inside it.
    /// <para>
    /// Two arrangements were tried and both were wrong in the same way. Inside the scroll, the
    /// numbers left with the code and a reader lost the number of the line they were reading.
    /// Overlaid on top of it, the code slid UNDER an opaque column and real characters disappeared —
    /// `using` became `eQuantic.UI.Core;`.
    /// </para>
    /// <para>
    /// Beside it, both stay true with nothing compensating for anything, and the price is the one
    /// paid here: ContentLeft counts from where the CODE begins, so the caret, the selection and
    /// every decoration all start there. That is why this test and the ones above travel together.
    /// </para>
    /// </summary>
    [Fact]
    public void TheGutterIsOutsideTheSidewaysScroll_SoTheCodeNeverSlidesUnderIt()
    {
        var editor = new CodeEditor("short\n" + new string('x', 400), "csharp");
        var built = editor.Build(new ComponentContext(PhotonTheme.Instance));

        var sideways = FindScroll(built, ScrollAxis.Horizontal);
        sideways.Should().NotBeNull("long lines scroll");
        FindGutter(sideways!).Should().BeNull(
            "inside the scroll the numbers leave with the code");
        FindGutter(built).Should().NotBeNull("…but they are somewhere, beside it");
    }

    /// <summary>Column zero counts from the CODE, not from the gutter — the change that made a
    /// pinned gutter possible, stated where it can fail.</summary>
    [Fact]
    public void ColumnZeroCountsFromTheCode()
    {
        var narrow = new CodeBlock.CodeMetrics(TypeStyle.OfSize(12, FontWeight.Regular), 17, 7, GutterWidth: 20);
        var wide = narrow with { GutterWidth = 200 };

        // A file with a thousand lines has a wider gutter than one with ten, and column zero is in
        // the same place in both: it counts from the CODE. Were it still measured past the gutter,
        // ten times the gutter would move every caret, band and decoration ten times as far.
        wide.ContentLeft.Should().Be(narrow.ContentLeft);
    }

    private static ScrollView? FindScroll(VisualNode node, ScrollAxis axis) => node switch
    {
        ScrollView view when view.Axis == axis => view,
        _ => Children(node).Select(child => FindScroll(child, axis)).FirstOrDefault(f => f is not null),
    };

    /// <summary>A gutter cell is the only Text carrying a LINE NUMBER — tabular, and a bare int.</summary>
    private static Text? FindGutter(VisualNode node) => node switch
    {
        Text text when text.Tabular && int.TryParse(text.Content, out _) => text,
        _ => Children(node).Select(FindGutter).FirstOrDefault(found => found is not null),
    };

    private static CodeSurface? FindSurface(VisualNode node) => node switch
    {
        CodeSurface surface => surface,
        _ => Children(node).Select(FindSurface).FirstOrDefault(found => found is not null),
    };

    private static int ScrollersUnder(VisualNode node) =>
        (node is ScrollView ? 1 : 0) + Children(node).Sum(ScrollersUnder);

    /// <summary>
    /// Every child, whatever the wrapper. A walker that stops at a node it does not know reports
    /// "not found" and "none below" with equal confidence — the first shape this test did not know
    /// (a Row) turned an invariant about scroll views into a test that walked three levels and gave
    /// up.
    /// </summary>
    private static IEnumerable<VisualNode> Children(VisualNode node) => node switch
    {
        CodeSurface surface => [surface.Child],
        ScrollView view => [view.Child],
        Box box => box.Child is { } child ? [child] : [],
        Stack stack => stack.Children,
        Positioned positioned => [positioned.Child],
        Shortcut shortcut => [shortcut.Child],
        Flexible flexible => [flexible.Child],
        FlexNode flex => flex,
        UiComponent component => [component.Build(new ComponentContext(PhotonTheme.Instance))],
        _ => [],
    };

    private static void Focus(PhotonHost host, CodeSurface surface, Rect bounds)
    {
        host.PressDown(bounds.X + surface.ContentLeft, bounds.Y + surface.ContentTop + 2);
        host.PressUp(bounds.X + surface.ContentLeft, bounds.Y + surface.ContentTop + 2);
        host.RenderFrame(new DisplayListBuilder());
        surface.Editor.Selection = new CodeRange(CodePosition.Start);
    }
}
