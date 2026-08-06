using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The three things that separate an editing surface from an editor you would use: finding your way
/// through a file, seeing which bracket closes the one you are on, and opening a file too long to
/// build a row for every line of.
/// </summary>
public class CodeEditorFinishTests
{
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

    private static (PhotonHost Host, CodeSurface Surface) Open(CodeEditor editor,
        float width = 500, float height = 400)
    {
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, width, height)
        {
            TextRasterizer = new FixedWidthRasterizer(),
        };
        var frame = host.RenderFrame(new DisplayListBuilder());
        return (host, frame.CodeRegions.Single().Surface);
    }

    /// <summary>The rectangles a frame drew that are exactly one line tall — the marks.</summary>
    private static DrawCommand[] Marks(PhotonHost host, float lineHeight)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 0);
        return builder.Build().Commands.ToArray()
            .Where(c => c.Kind == DrawCommandKind.FillRRect
                && MathF.Abs(c.Shape.Rect.Height - lineHeight) < 0.01f)
            .ToArray();
    }

    // ---- brackets --------------------------------------------------------------------------------

    /// <summary>
    /// A caret sits BETWEEN characters, so it belongs to the bracket on either side of it — and the
    /// one BEHIND wins, because having just typed `)` that is the one you mean.
    /// </summary>
    [Fact]
    public void TheBracketUnderTheCaretFindsItsPair()
    {
        var editor = new CodeEditor("f(a(b), c)", "csharp") { ShowLineNumbers = false };
        editor.Editor.Selection = new CodeRange(new CodePosition(0, 2));   // just after `(`

        var pair = editor.Editor.BracketAtCaret();

        pair.Should().NotBeNull();
        pair!.Value.Here.Column.Should().Be(1, "the bracket behind the caret is the one it is on");
        pair.Value.There.Column.Should().Be(9);
    }

    [Fact]
    public void AndNothingIsMarkedWhereThereIsNoBracket()
    {
        var editor = new CodeEditor("plain text here", "csharp");
        editor.Editor.Selection = new CodeRange(new CodePosition(0, 6));

        editor.Editor.BracketAtCaret().Should().BeNull();
    }

    [Fact]
    public void TheMatchingPairIsOUTLINED_NotWashed()
    {
        // The caret is placed BEFORE the host opens: reaching into the controller from outside does
        // not ask for a rebuild, and it is not supposed to — the surface's own seam does that.
        var editor = new CodeEditor("f(a)", "csharp") { ShowLineNumbers = false };
        editor.Editor.Selection = new CodeRange(new CodePosition(0, 2));
        var (host, surface) = Open(editor);

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 0);
        // A 1dp border strokes INSIDE the box, so the recorded rect is a dp narrower than asked.
        var outlines = builder.Build().Commands.ToArray()
            .Where(c => c.Kind == DrawCommandKind.StrokeRRect
                && MathF.Abs(c.Shape.Rect.Width - surface.ColumnWidth) < 1.5f)
            .ToArray();

        // A wash would hide the character the mark is pointing at, which is the whole point of it.
        outlines.Should().HaveCount(2, "the bracket and its pair");
        outlines.Select(o => MathF.Round(o.Shape.Rect.X)).Distinct().Should().HaveCount(2);
    }

    // ---- find ------------------------------------------------------------------------------------

    [Fact]
    public void EveryMatchIsMarked_AndTheCurrentOneDiffers()
    {
        var editor = new CodeEditor("one two one two one", "csharp")
        {
            ShowLineNumbers = false,
            Search = "one",
            MatchBrackets = false,
        };
        editor.Editor.Selection = new CodeRange(new CodePosition(0, 0), new CodePosition(0, 3));
        var (host, surface) = Open(editor);

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, 0);
        var commands = builder.Build().Commands.ToArray();

        var washes = commands.Where(c => c.Kind == DrawCommandKind.FillRRect
            && MathF.Abs(c.Shape.Rect.Width - 3 * surface.ColumnWidth) < 0.5f).ToArray();
        var outlines = commands.Where(c => c.Kind == DrawCommandKind.StrokeRRect
            && MathF.Abs(c.Shape.Rect.Width - 3 * surface.ColumnWidth) < 1.5f).ToArray();

        washes.Should().HaveCount(2, "the matches that are not the current one");
        outlines.Should().HaveCount(1, "…and the one the caret is on, so 'next' moves something visible");
    }

    [Fact]
    public void FindWalksForwardAndWraps()
    {
        var editor = new CodeEditor("one two one", "csharp");
        var controller = editor.Editor;
        controller.Selection = new CodeRange(CodePosition.Start);

        controller.FindNext("one")!.Value.Start.Column.Should().Be(0);
        controller.Selection = new CodeRange(new CodePosition(0, 3));
        controller.FindNext("one")!.Value.Start.Column.Should().Be(8);
        controller.Selection = new CodeRange(controller.Document.End);
        controller.FindNext("one")!.Value.Start.Column.Should().Be(0, "past the last it wraps");
    }

    [Fact]
    public void CommandFOpensTheFindBar()
    {
        var editor = new CodeEditor("searchable text", "csharp") { ShowLineNumbers = false };
        var (host, _) = Open(editor);

        host.RenderFrame(new DisplayListBuilder()).TextRegions.Should().BeEmpty("no bar yet");

        host.KeyDown("f", KeyModifiers.Command);
        var frame = host.RenderFrame(new DisplayListBuilder());

        frame.TextRegions.Should().ContainSingle("the find field is the bar");
        frame.TextRegions.Single().Entry.Placeholder.Should().Be("Find");
    }

    // ---- virtualization --------------------------------------------------------------------------

    /// <summary>
    /// A file too long to build a row per line. The window is what the viewport can show plus a
    /// margin; the rest of the document's height is a spacer, so the content is still as tall as the
    /// file and the scrollbar tells the truth.
    /// </summary>
    [Fact]
    public void ALongFileBuildsOnlyTheLinesYouCanSee()
    {
        var text = string.Join("\n", Enumerable.Range(0, 4000).Select(i => $"var line{i} = {i};"));
        var editor = new CodeEditor(text, "csharp") { MaxHeight = 300, ShowLineNumbers = false };
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, 500, 400)
        {
            TextRasterizer = new FixedWidthRasterizer(),
        };

        // The first frame has no viewport yet and builds everything; the second knows how tall the
        // box turned out to be, and narrows.
        host.RenderFrame(new DisplayListBuilder());
        var frame = host.RenderFrame(new DisplayListBuilder());

        var rows = Count(frame.Root, node => node.Source is Row);
        rows.Should().BeLessThan(200, "a 4000-line file must not build 4000 rows");
        rows.Should().BeGreaterThan(5, "…and it must build the ones on screen");
    }

    [Fact]
    public void AndTheDocumentIsSTILLAsTallAsItIs()
    {
        var text = string.Join("\n", Enumerable.Range(0, 500).Select(i => $"line {i}"));
        var editor = new CodeEditor(text, "csharp") { MaxHeight = 200, ShowLineNumbers = false };
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, 500, 300)
        {
            TextRasterizer = new FixedWidthRasterizer(),
        };
        host.RenderFrame(new DisplayListBuilder());
        var frame = host.RenderFrame(new DisplayListBuilder());

        // The scroll region's travel is the content's height minus the viewport's: a window that
        // dropped the spacers would report almost none, and the scrollbar would lie.
        var scroll = frame.ScrollRegions.First(r => r.Axis == ScrollAxis.Vertical);
        scroll.MaxOffset.Should().BeGreaterThan(500 * 10f - 200,
            "the file is 500 lines tall whether or not they are all built");
    }

    [Fact]
    public void AShortFileIsNotWindowedAtAll()
    {
        var editor = new CodeEditor("one\ntwo\nthree", "csharp") { ShowLineNumbers = false };
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, 500, 300)
        {
            TextRasterizer = new FixedWidthRasterizer(),
        };
        host.RenderFrame(new DisplayListBuilder());
        var frame = host.RenderFrame(new DisplayListBuilder());

        Count(frame.Root, node => node.Source is Spacer { Flex: 0 }).Should().Be(0,
            "there is nothing to stand in for");
    }

    private static int Count(Framework.LayoutNode node, Func<Framework.LayoutNode, bool> predicate)
    {
        var total = predicate(node) ? 1 : 0;
        foreach (var child in node.Children) total += Count(child, predicate);
        return total;
    }
}
