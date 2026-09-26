using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A code block that draws ROWS (docs/CODE-EDITOR-PLAN.md, the shape, §9): a diff's padding, the
/// lines of the other document drawn between the lines that replaced them, what a patch left out,
/// and a folded run that opens on a press. Measured on Photon's layout, the same tree the web
/// realizes: every row is where the map puts it, and so is everything marked on it.
/// </summary>
public class CodeBlockRowsTests
{
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

    private static RealizeResult Settle(VisualNode root)
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 600, 800)
        {
            TextRasterizer = new FixedWidthRasterizer(),
        };
        host.RenderFrame(new DisplayListBuilder());
        return host.RenderFrame(new DisplayListBuilder());
    }

    private static CodeBlock Block(string code, CodeRows rows) =>
        new(code, "plaintext") { Rows = rows, ShowLineNumbers = false };

    private static Framework.LayoutNode? First(Framework.LayoutNode node, Func<Framework.LayoutNode, bool> predicate)
    {
        if (predicate(node)) return node;
        foreach (var child in node.Children)
            if (First(child, predicate) is { } found) return found;
        return null;
    }

    private static List<Framework.LayoutNode> All(Framework.LayoutNode node, Func<Framework.LayoutNode, bool> predicate)
    {
        var found = new List<Framework.LayoutNode>();
        void Walk(Framework.LayoutNode at)
        {
            if (predicate(at)) found.Add(at);
            foreach (var child in at.Children) Walk(child);
        }
        Walk(node);
        return found;
    }

    /// <summary>The top of the row a text is drawn in: the text itself is centred in it.</summary>
    private static float TopOf(RealizeResult frame, string text) =>
        First(frame.Root, node => node.Source is Row && node.Children.Count > 0
            && First(node, inner => inner.Source is Text { Content: var content } && content == text) is not null
            && node.Bounds.Height < 40)!.Bounds.Y;

    [Fact]
    public void AFillerRowStandsWhereTheMapPutsIt_AndTheLinesAfterItMoveDown()
    {
        // a, two rows of padding, b, c.
        var frame = Settle(Block("a\nb\nc", new CodeRows(3, [new CodeFiller(1, 2)], [])));

        var row = TopOf(frame, "c") - TopOf(frame, "b");
        row.Should().BeGreaterThan(0);
        (TopOf(frame, "b") - TopOf(frame, "a")).Should().BeApproximately(3 * row, 0.5f,
            "b is on row 3, after a and the two rows of padding");
    }

    [Fact]
    public void AFillerWithASourceLine_DrawsTheOtherDocumentsLine()
    {
        var frame = Settle(new CodeBlock("a\nb", "plaintext")
        {
            Rows = new CodeRows(2, [new CodeFiller(1, 1, SourceLine: 0)], []),
            FillerDocument = CodeDocument.FromText("removed"),
            ShowLineNumbers = false,
        });

        var row = TopOf(frame, "b") - TopOf(frame, "removed");
        (TopOf(frame, "removed") - TopOf(frame, "a")).Should().BeApproximately(row, 0.5f,
            "the removed line is drawn on row 1, between a and the line that replaced it");
    }

    [Fact]
    public void AFillersLabel_IsWhatItsRowSays()
    {
        var frame = Settle(Block("a\nb", new CodeRows(2, [new CodeFiller(1, 1, Label: "@@ -10,2 +10,3 @@")], [])));

        First(frame.Root, node => node.Source is Text { Content: "@@ -10,2 +10,3 @@" }).Should().NotBeNull();
    }

    [Fact]
    public void AFoldedRun_IsOneRowThatSaysWhatItHides_AndOpensOnAPress()
    {
        var opened = -1;
        var block = new CodeBlock("l0\nl1\nl2\nl3\nl4", "plaintext")
        {
            Rows = new CodeRows(5, [], [new CodeCollapse(1, 3, Label: "3 unchanged lines")]),
            ShowLineNumbers = false,
            OnPlaceholderPressed = line => opened = line,
        };
        var frame = Settle(block);

        First(frame.Root, node => node.Source is Text { Content: "l2" }).Should().BeNull("the fold hides it");
        var placeholder = frame.HitRegions.Should().ContainSingle(region => region.Node.Label == "3 unchanged lines").Subject;
        placeholder.Node.OnPressed!();

        opened.Should().Be(1, "a press opens the run from its first hidden line");
        (TopOf(frame, "l4") - TopOf(frame, "l0")).Should().BeApproximately(
            2 * (TopOf(frame, "l4") - TopOf(frame, "3 unchanged lines")), 0.5f, "l4 is on row 2, after the placeholder");
    }

    /// <summary>A fold the caller left unlabeled is named for what it hides, on screen and for
    /// assistive tech: a bare "⋯" was a control nothing could announce.</summary>
    [Fact]
    public void AFoldWithNoLabel_IsNamedForWhatItHides()
    {
        var frame = Settle(new CodeBlock("l0\nl1\nl2\nl3\nl4", "plaintext")
        {
            Rows = new CodeRows(5, [], [new CodeCollapse(1, 3)]),
            ShowLineNumbers = false,
            OnPlaceholderPressed = _ => { },
        });

        var name = SdkStrings.HiddenLines(3);
        frame.HitRegions.Should().ContainSingle(region => region.Node.Label == name);
        First(frame.Root, node => node.Source is Text { Content: var text } && text == name)
            .Should().NotBeNull("the row says it too");
    }

    [Fact]
    public void TheGutterNumbersTheLines_AndLeavesTheFillersBlank()
    {
        var frame = Settle(new CodeBlock("a\nb\nc", "plaintext")
        {
            Rows = new CodeRows(3, [new CodeFiller(1, 2)], []),
            FirstLineNumber = 1,
        });

        var numbers = All(frame.Root, node => node.Source is Text { Content: "1" or "2" or "3" or "4" or "5" })
            .Select(node => ((Text)node.Source).Content).ToList();
        numbers.Should().Equal("1", "2", "3");
        TopOf(frame, "2").Should().BeApproximately(TopOf(frame, "b"), 0.5f, "line b's number is on b's row");
    }

    [Fact]
    public void AMarkIsDrawnOnItsLinesRow_AndNotForALineAFoldHides()
    {
        var mark = new ColorToken(new Color(0x12, 0x34, 0x56, 0xFF));
        var frame = Settle(new CodeBlock("a\nb\nc\nd\ne\nf", "plaintext")
        {
            Rows = new CodeRows(6, [new CodeFiller(1, 2)], [new CodeCollapse(3, 4, Placeholder: true)]),
            ShowLineNumbers = false,
            Decorations =
            [
                new CodeDecoration(new CodeRange(new CodePosition(2, 0), new CodePosition(2, 1)), CodeDecorationKind.Highlight) { Color = mark },
                new CodeDecoration(new CodeRange(new CodePosition(3, 0), new CodePosition(3, 1)), CodeDecorationKind.Highlight) { Color = mark },
            ],
        });

        var marks = All(frame.Root, node => node.Source is Box { Style.Background: var background } && background == mark);
        marks.Should().ContainSingle("line d is folded, and a mark on it has no row to be drawn on");
        var top = TopOf(frame, "c");
        marks[0].Bounds.Y.Should().BeApproximately(top, 0.5f, "c's mark is on c's row, three rows down");
    }

    [Fact]
    public void ALineDecorationWashesTheWholeRow()
    {
        var wash = new ColorToken(new Color(0x65, 0x43, 0x21, 0xFF));
        var frame = Settle(new CodeBlock("short\na much longer line of code", "plaintext")
        {
            ShowLineNumbers = false,
            Decorations = [new CodeDecoration(new CodeRange(new CodePosition(0, 0), new CodePosition(0, 1)), CodeDecorationKind.Line) { Color = wash }],
        });

        var row = All(frame.Root, node => node.Source is Box { Style.Background: var background } && background == wash)
            .Should().ContainSingle().Subject;
        var longest = First(frame.Root, node => node.Source is Text { Content: "a much longer line of code" })!;
        row.Bounds.Right.Should().BeGreaterThanOrEqualTo(longest.Bounds.Right,
            "a changed line's wash crosses the row, however little of it the text takes");
        row.Bounds.Y.Should().BeApproximately(TopOf(frame, "short"), 0.5f);
    }

    [Fact]
    public void AFillerDecoration_MarksTheOtherDocumentsLineWhereItIsDrawn()
    {
        var removed = new ColorToken(new Color(0x11, 0x22, 0x33, 0xFF));
        var frame = Settle(new CodeBlock("a\nb", "plaintext")
        {
            Rows = new CodeRows(2, [new CodeFiller(1, 1, SourceLine: 4)], []),
            FillerDocument = CodeDocument.FromText("0\n1\n2\n3\nold line"),
            FillerDecorations = [new CodeDecoration(new CodeRange(new CodePosition(4, 0), new CodePosition(4, 3)), CodeDecorationKind.Highlight) { Color = removed }],
            ShowLineNumbers = false,
        });

        var mark = All(frame.Root, node => node.Source is Box { Style.Background: var background } && background == removed)
            .Should().ContainSingle().Subject;
        mark.Bounds.Y.Should().BeApproximately(TopOf(frame, "old line"), 0.5f, "the word is marked where its line is drawn");
    }

    /// <summary>
    /// The window counts rows: of 400 lines and 200 rows of padding, a viewport of a few rows builds a
    /// few rows, and the content is as tall as all 600, so the scrollbar tells the truth.
    /// </summary>
    [Fact]
    public void TheWindowAndItsSpacersCountRows()
    {
        var code = string.Join("\n", Enumerable.Range(0, 400).Select(i => $"line{i}"));
        var fillers = Enumerable.Range(0, 200).Select(i => new CodeFiller(i * 2, 1)).ToList();
        var whole = Settle(Block(code, new CodeRows(400, fillers, [])));
        // Rows: padding, line0, line1, padding, line2, line3, ...
        var row = TopOf(whole, "line1") - TopOf(whole, "line0");
        (TopOf(whole, "line2") - TopOf(whole, "line1")).Should().BeApproximately(2 * row, 0.5f);

        var windowed = Settle(new CodeBlock(code, "plaintext")
        {
            Rows = new CodeRows(400, fillers, []),
            ShowLineNumbers = false,
            ViewportOffset = 0,
            ViewportHeight = 10 * row,
        });

        All(windowed.Root, node => node.Source is Text { Content: var content } && content.StartsWith("line"))
            .Count.Should().BeLessThan(40, "only the rows in view and a margin are built");
        var content = First(windowed.Root, node => node.Source is Column && node.Bounds.Height > 100 * row)!;
        content.Bounds.Height.Should().BeApproximately(600 * row, row, "400 lines and 200 rows of padding");
    }
}
