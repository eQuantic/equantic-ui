using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The completion list a <see cref="CodeEditor"/> draws (#297), driven as a person drives it: a word
/// typed, the arrows, a press on a row. Where the list stands is read from the frame (the region of
/// what the surface offered) against where the engine says the word starts.
/// <para>
/// The hosts run <see cref="Density.Compact"/>, the desktop shells' density, where a press lands
/// where it is aimed. Under Comfortable a row's §08 margin reaches over the row above it (#630).
/// </para>
/// </summary>
public class CodeEditorCompletionTests
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

    /// <summary>Offers what it was made with, whatever was typed: the list is the view's to test.</summary>
    private sealed class ListProvider(params CodeCompletionItem[] items) : ICodeCompletionProvider
    {
        public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
            CodeCompletionContext context, CancellationToken cancellation) =>
            Task.FromResult(new CodeCompletionList(items));
    }

    /// <summary>Answers when the test says so, as a language server answers after the keystroke.</summary>
    private sealed class LateProvider(params CodeCompletionItem[] items) : ICodeCompletionProvider
    {
        private readonly TaskCompletionSource<CodeCompletionList> _answer = new();

        public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
            CodeCompletionContext context, CancellationToken cancellation) => _answer.Task;

        public void Answer() => _answer.TrySetResult(new CodeCompletionList(items));
    }

    /// <summary>Offers its entries at once and resolves the selected one when the test says so, with
    /// the detail a language service fills in on resolve.</summary>
    private sealed class ResolvingProvider(CodeCompletionItem item, string detail) : ICodeCompletionProvider
    {
        private readonly TaskCompletionSource<CodeCompletionItem> _resolved = new();

        public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
            CodeCompletionContext context, CancellationToken cancellation) =>
            Task.FromResult(new CodeCompletionList([item]));

        public Task<CodeCompletionItem> ResolveAsync(CodeCompletionItem asked, CancellationToken cancellation) =>
            _resolved.Task;

        public void Resolve() => _resolved.TrySetResult(item with { Detail = detail });
    }

    private static PhotonHost Host(VisualNode root, float width = 600, float height = 400) =>
        new(root, PhotonTheme.Instance, ThemeMode.Light, width, height)
        {
            TextRasterizer = new FixedWidthRasterizer(),
            Density = Density.Compact,
            // A wheel lands at once: smooth scrolling glides toward it over frames that move the clock.
            SmoothScroll = false,
        };

    private static RealizeResult Frame(PhotonHost host) => host.RenderFrame(new DisplayListBuilder());

    /// <summary>Two frames: the second knows how big the viewport turned out to be.</summary>
    private static RealizeResult Settle(PhotonHost host)
    {
        Frame(host);
        return Frame(host);
    }

    /// <summary>Puts the caret at <paramref name="line"/>, <paramref name="column"/> with a press, as
    /// a person does, and settles the frames after it.</summary>
    private static RealizeResult ClickAt(PhotonHost host, CodeEditor editor, int line, int column)
    {
        var region = Frame(host).CodeRegions.Single();
        var at = editor.Editor.CaretRect(new CodePosition(line, column));
        var x = region.Bounds.X + at.X + 1;
        var y = region.Bounds.Y + at.Y + at.Height / 2;
        host.PressDown(x, y);
        host.PressUp(x, y);
        return Settle(host);
    }

    private static RealizeResult Type(PhotonHost host, string text)
    {
        foreach (var c in text) host.TextInput(c.ToString());
        return Settle(host);
    }

    private static RealizeResult Press(PhotonHost host, string key, int times = 1)
    {
        for (var i = 0; i < times; i++) host.KeyDown(key);
        return Settle(host);
    }

    private static void PressAt(PhotonHost host, float x, float y)
    {
        host.PressDown(x, y);
        host.PressUp(x, y);
        Settle(host);
    }

    /// <summary>The rows the list shows, top to bottom, by their names.</summary>
    private static List<string> Shown(PhotonHost host) =>
        host.Semantics().Where(node => node.Role == SemanticRole.Option).Select(node => node.Label).ToList();

    /// <summary>Where the word being completed starts, on screen.</summary>
    private static Rect WordOnScreen(RealizeResult frame, CodeEditor editor)
    {
        var region = frame.CodeRegions.Single();
        var word = editor.Editor.CaretRect(editor.Editor.Completion.Start);
        return word with { X = region.Bounds.X + word.X, Y = region.Bounds.Y + word.Y };
    }

    /// <summary>Lines enough to fill the editor, the first holding words a list can offer for Co.</summary>
    private static string Lines(int count)
    {
        var lines = new List<string> { "var Column = 1; var ColorToken = 2;" };
        for (var i = 1; i < count; i++) lines.Add("");
        return string.Join("\n", lines);
    }

    private static CodeEditor Editor(string code, params CodeCompletionItem[] offered) =>
        new(code, "csharp")
        {
            ShowLineNumbers = false,
            Height = SizeValue.Fill,
            Completions = offered.Length == 0 ? null : [new ListProvider(offered)],
        };

    private static CodeCompletionItem[] Items(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new CodeCompletionItem($"Item{i:00}"))];

    /// <summary>
    /// From the list's left edge to its labels: the frame's hairline, the row's padding, the letter's
    /// cell a line wide and the gap after it. The web lays the labels there. Photon lays a bordered
    /// box's child over its border (#629), so until that is fixed its labels stand one border left of
    /// the word, and the placement, which is the component's, is what is pinned here.
    /// </summary>
    private static float LabelInset(CodeEditor editor) => 1 + Space.S2 + editor.Editor.Grid.Cell.Height + Space.S1;

    // ---- where it stands -------------------------------------------------------------------------

    [Fact]
    public void AWordTypedNearTheTop_ListsBelowItsLine_LabelsLinedUpWithTheWord()
    {
        var editor = Editor(Lines(20));
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);

        var frame = Type(host, "    Co");

        editor.Editor.Completion.IsOpen.Should().BeTrue("a word started by typing opens the list");
        var offered = frame.CodeRegions.Single().Offered;
        offered.Should().NotBeNull("the surface offers the list it draws");
        var word = WordOnScreen(frame, editor);
        offered!.Value.Y.Should().BeApproximately(word.Y + word.Height, 0.5f,
            "the list's top is the bottom of the line it completes");
        (offered.Value.X + LabelInset(editor)).Should().BeApproximately(word.X, 0.5f,
            "the list stands as far left of the word as its labels are inside it");
    }

    [Fact]
    public void AWordTypedOnTheLastVisibleLine_ListsAboveIt()
    {
        var editor = Editor(Lines(40));
        var host = Host(editor, height: 300);
        Settle(host);
        var grid = editor.Editor.Grid;
        // The last line wholly in the viewport.
        var last = (int)((300 - grid.Origin.Y) / grid.Cell.Height) - 1;
        ClickAt(host, editor, last, 0);

        var frame = Type(host, "Co");

        var offered = frame.CodeRegions.Single().Offered;
        offered.Should().NotBeNull();
        var word = WordOnScreen(frame, editor);
        var rows = Shown(host).Count;
        // The list's height on the web: its rows, its padding and its frame. Photon's frame comes out
        // two borders short until #629, so the top is what is pinned.
        var height = rows * grid.Cell.Height + 2 * (Space.S1 + 1);
        offered!.Value.Y.Should().BeApproximately(word.Y - height, 0.5f,
            "with no room below, the list ends at the top of the line it completes");
    }

    /// <summary>A word typed near the viewport's right edge keeps its list inside it.</summary>
    [Fact]
    public void AWordNearTheRightEdge_KeepsItsListInsideTheView()
    {
        var editor = Editor("var Column = 1;\n" + new string(' ', 88), new CodeCompletionItem("Column"),
            new CodeCompletionItem("ColorToken"));
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 88);

        var frame = Type(host, "Co");

        var region = frame.CodeRegions.Single();
        var visible = region.Visible ?? region.Bounds;
        var offered = region.Offered!.Value;
        (WordOnScreen(frame, editor).X - LabelInset(editor) + offered.Width).Should().BeGreaterThan(visible.Right,
            "where its word stands, the list would run past the view's right edge");
        offered.Right.Should().BeLessThanOrEqualTo(visible.Right + 0.5f, "the list's right edge stays in the view");
    }

    /// <summary>
    /// Code scrolled sideways takes the list's left edge with the view: the editor tracks how far it
    /// slid, and a list whose word has gone off the left stands at the view's left edge.
    /// </summary>
    [Fact]
    public void CodeScrolledSideways_KeepsTheListInsideTheView()
    {
        var editor = Editor("var line = \"" + new string('x', 300) + "\";\n", new CodeCompletionItem("Column"),
            new CodeCompletionItem("ColorToken"));
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);
        Type(host, "Co");

        host.ScrollBy(300, 30, 200).Should().BeTrue("the long first line scrolls the code sideways");
        var frame = Settle(host);

        editor.Editor.Completion.IsOpen.Should().BeTrue("a scroll moves no caret");
        var region = frame.CodeRegions.Single();
        var visible = region.Visible ?? region.Bounds;
        var offered = region.Offered!.Value;
        offered.Left.Should().BeGreaterThanOrEqualTo(visible.Left - 0.5f,
            "the list stands at the view's left edge, not with its word off the left of it");
        offered.Width.Should().BeGreaterThan(100, "and all of it is in the view");
    }

    [Fact]
    public void WhereNeitherSideHoldsAPage_TheRoomierOneShowsWhatFits()
    {
        var editor = new CodeEditor("var Column = 1;\n\n\n", "csharp")
        {
            ShowLineNumbers = false,
            Completions = [new ListProvider(Items(30))],
        };
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);

        Type(host, "I");

        var shown = Shown(host).Count;
        shown.Should().BeInRange(1, 11, "an editor that hugs four lines has room for a few rows, never a page");
        editor.Editor.Completion.PageSize.Should().Be(shown, "PageDown steps by the rows shown");
    }

    [Fact]
    public void AShortFileInATallPane_HasThePanesRoomForItsList()
    {
        var editor = Editor("var Column = 1;\n", Items(30));
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);

        var frame = Type(host, "I");

        Shown(host).Should().HaveCount(12, "the pane has room for a page under a two-line file");
        frame.HitRegions.Count(region => region.Node.Role == PressableRole.Option).Should().Be(12,
            "and every row of it can be pressed: a surface as short as its code clipped them away");
        var offered = frame.CodeRegions.Single().Offered!.Value;
        var word = WordOnScreen(frame, editor);
        offered.Y.Should().BeApproximately(word.Y + word.Height, 0.5f, "below the line, in the room under the code");
    }

    /// <summary>
    /// An answer that arrives after the keystroke asks for a frame of its own: nothing else would draw
    /// the list until the next key, since the editor learns of the answer outside any input the
    /// surface handled.
    /// </summary>
    [Fact]
    public void AnAnswerThatArrivesAfterTheKey_AsksForAFrame_AndTheListShows()
    {
        var late = new LateProvider(new CodeCompletionItem("Column"), new CodeCompletionItem("ColorToken"));
        var editor = new CodeEditor(Lines(20), "csharp")
        {
            ShowLineNumbers = false,
            Height = SizeValue.Fill,
            Completions = [late],
        };
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);
        Type(host, "Co");
        Shown(host).Should().BeEmpty("nothing has answered yet");
        host.NeedsRender.Should().BeFalse("the frames after the key were drawn");

        late.Answer();

        host.NeedsRender.Should().BeTrue("the answer asks for a frame");
        Settle(host);
        Shown(host).Should().Equal(["ColorToken", "Column"]);
    }

    // ---- the page --------------------------------------------------------------------------------

    [Fact]
    public void WalkingPastThePage_TheSelectedRowStaysShown()
    {
        var editor = Editor(Lines(20), Items(30));
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);
        Type(host, "I");
        Shown(host).Should().HaveCount(12, "a page of the thirty");

        Press(host, "ArrowDown", 14);

        editor.Editor.Completion.Selected.Should().Be(14);
        Shown(host).Should().Contain("Item14", "the fifteenth entry is selected and shown")
            .And.NotContain("Item00", "and the page moved past the first");
        host.Semantics().Single(node => node.Role == SemanticRole.Option && node.Selected == true)
            .Label.Should().Be("Item14", "the row the keyboard is on is the selected option");
        editor.Editor.Completion.PageSize.Should().Be(12, "PageDown steps by the rows shown");
    }

    // ---- what a row says -------------------------------------------------------------------------

    [Fact]
    public void ARowIsNamedByItsLabelAndItsDetail()
    {
        var editor = Editor(Lines(20), new CodeCompletionItem("Column", CodeCompletionKind.Class) { Detail = "class Column" });
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);

        Type(host, "Col");

        Shown(host).Should().Equal(["Column, class Column"]);
    }

    /// <summary>
    /// A row longer than the list cuts its detail first and its label after it, each with an
    /// ellipsis, by the code face's columns, so both targets cut it in the same place; its name keeps
    /// them whole. A label could not shrink, and on the web it ran past the frame with its detail
    /// pushed out of sight (found reviewing #297).
    /// </summary>
    [Fact]
    public void ARowLongerThanTheList_CutsItsDetailFirst_AndKeepsItsName()
    {
        var name = "Co" + new string('x', 70);
        var editor = Editor(Lines(20),
            new CodeCompletionItem(name) { Detail = "string" },
            new CodeCompletionItem("Column") { Detail = "(string text, int start, int count, bool ignoreCase) -> string" });
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);

        var frame = Type(host, "Co");

        var texts = Descendants(frame.Root).Select(node => node.Source).OfType<Text>().Select(text => text.Content).ToList();
        texts.Should().Contain(text => text.StartsWith("Coxx") && text.EndsWith("…"), "the long label is cut");
        texts.Should().NotContain(text => text == "string", "and its detail, which has no room left");
        texts.Should().Contain(text => text.StartsWith("(string text") && text.EndsWith("…"), "a long detail is cut");
        texts.Should().Contain("Column", "beside a label that fits whole");
        Shown(host).Should().Contain($"{name}, string", "the name keeps both whole");
    }

    /// <summary>
    /// A row is cut between two text elements, by the cells the code's grid gives them. Counted in
    /// UTF-16 units, the cut fell inside an emoji's surrogate pair and drew half of it, and a label of
    /// wide characters, twice as wide as its length, ran past the list uncut (found by Copilot
    /// reviewing #653).
    /// </summary>
    [Fact]
    public void ARowIsCutBetweenTextElements_ByTheCellsTheyTake()
    {
        var emoji = "Co" + new string('x', 56) + "😀😀😀";
        var wide = "Co" + new string('中', 40);
        var editor = Editor(Lines(20), new CodeCompletionItem(emoji), new CodeCompletionItem(wide));
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);

        var frame = Type(host, "Co");

        // The list's two labels, and not the code's own words that begin like them.
        var labels = Descendants(frame.Root).Select(node => node.Source).OfType<Text>()
            .Select(text => text.Content).Where(text => text.StartsWith("Cox") || text.StartsWith("Co中")).ToList();
        labels.Should().HaveCount(2);
        labels.Should().OnlyContain(label => Whole(label), "no cut falls inside a surrogate pair");
        // The widest a list grows is sixty columns of the code face.
        labels.Should().OnlyContain(label => CodeLineCells.WidthOf(label, 4) <= 60,
            "and no label takes more cells than the list has");
        labels.Should().OnlyContain(label => label.EndsWith("…"), "both are longer than the list, and cut");
    }

    /// <summary>
    /// The list measures its entries once for each list the completion holds, and the selected one on
    /// every build: a resolve fills the selected entry in where it stands, in the same list, and the
    /// detail it brings widens the list (Copilot's third round on #653 asked for the list's width to be
    /// kept across the rebuilds the arrows make).
    /// </summary>
    [Fact]
    public void ADetailTheResolveFillsIn_WidensTheList()
    {
        var provider = new ResolvingProvider(new CodeCompletionItem("Column"), new string('d', 40));
        var editor = new CodeEditor(Lines(20), "csharp")
        {
            ShowLineNumbers = false,
            Height = SizeValue.Fill,
            Completions = [provider],
        };
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);
        var before = Type(host, "Co").CodeRegions.Single().Offered!.Value.Width;

        provider.Resolve();
        var after = Settle(host).CodeRegions.Single().Offered!.Value.Width;

        after.Should().BeGreaterThan(before, "the detail the resolve brought takes columns the list did not have");
    }

    /// <summary>Whether every surrogate in <paramref name="text"/> is half of a pair.</summary>
    private static bool Whole(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
            else if (char.IsSurrogate(text[i])) return false;
        }
        return true;
    }

    private static IEnumerable<LayoutNode> Descendants(LayoutNode node)
    {
        yield return node;
        foreach (var child in node)
            foreach (var descendant in Descendants(child))
                yield return descendant;
    }

    [Fact]
    public void TheLabelMarksWhatTheWordMatched()
    {
        var editor = Editor(Lines(20), new CodeCompletionItem("Column"));
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);

        var frame = Type(host, "Col");

        var label = (Text)Find(frame.Root, node => node.Source is Text { Content: "Column", Spans: not null }).Source;
        label.Spans!.Select(run => run.Content).Should().Equal(["Col", "umn"]);
        var accent = PhotonTheme.Instance.Colors(Variant.Primary).Base;
        label.Spans![0].Color.Should().Be(accent, "the three characters the word matched are marked");
        label.Spans![1].Color.Should().NotBe(accent);
        label.Spans!.Should().OnlyContain(run => run.Mono, "in the code's face, as the word is");
    }

    [Fact]
    public void TheSelectedEntrysDocumentation_ShowsWithTheList()
    {
        var editor = Editor(Lines(20),
            new CodeCompletionItem("Column") { Documentation = "Lays its children out in a column." },
            new CodeCompletionItem("ColorToken"));
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);

        var frame = Type(host, "Co");

        // ColorToken sorts first and has none.
        FindOrNull(frame.Root, node => node.Source is Text { Content: "Lays its children out in a column." })
            .Should().BeNull("the selected entry has no documentation");
        frame = Press(host, "ArrowDown");
        FindOrNull(frame.Root, node => node.Source is Text { Content: "Lays its children out in a column." })
            .Should().NotBeNull("the selected entry's documentation shows with the list");
    }

    /// <summary>
    /// A language service's page of documentation shows its first lines and is laid out no further:
    /// four lines of the list hold a few hundred characters, and the whole page was measured and
    /// wrapped on every build while its entry was selected.
    /// </summary>
    [Fact]
    public void ALongDocumentation_IsLaidOutOnlyAsFarAsItShows()
    {
        var page = string.Join("\n", Enumerable.Range(0, 300).Select(i => $"Line {i} of a long page of documentation."));
        var editor = Editor(Lines(20), new CodeCompletionItem("Column") { Documentation = page });
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);

        var frame = Type(host, "Col");

        var shown = (Text)Find(frame.Root, node => node.Source is Text text && text.Content.StartsWith("Line 0 ")).Source;
        shown.MaxLines.Should().Be(4, "the box holds four lines");
        shown.Content.Length.Should().BeLessThan(page.Length / 10, "and only what they can show is laid out");
    }

    /// <summary>Documentation of four paragraphs, each a line of its own.</summary>
    private const string FourParagraphs = "Lays its children out.\nIn a column.\nTop to bottom.\nWith a gap.";

    /// <summary>
    /// The documentation takes the room the rows leave on its side and no more. A row that just fits
    /// under its line stays there, and documentation drawn whole under it ran out of the view (found by
    /// Copilot reviewing #653).
    /// </summary>
    [Fact]
    public void ARowThatJustFitsUnderItsLine_StaysThere_AndNoDocumentationRunsOutOfTheView()
    {
        var editor = Editor(Lines(40), new CodeCompletionItem("Column") { Documentation = FourParagraphs });
        var host = Host(editor, height: 300);
        Settle(host);
        var grid = editor.Editor.Grid;
        // The list's height on the web for one row: the row, its padding and its frame.
        var listHeight = grid.Cell.Height + 2 * (Space.S1 + 1);
        // The lowest line with room for that row under it, and so with none for documentation too.
        var line = (int)((300 - grid.Origin.Y - listHeight) / grid.Cell.Height) - 1;
        ClickAt(host, editor, line, 0);

        var frame = Type(host, "Col");

        var word = WordOnScreen(frame, editor);
        frame.CodeRegions.Single().Offered!.Value.Y.Should().BeApproximately(word.Y + word.Height, 0.5f,
            "the row fits under its line, and the documentation does not move it");
        Shown(host).Should().Equal(["Column"]);
        FindOrNull(frame.Root, node => node.Source is Text { Content: FourParagraphs })
            .Should().BeNull("no line of the documentation fits under the row, inside the view");
    }

    /// <summary>
    /// Above its line the list keeps its page, and its documentation shows the lines that fit between
    /// the rows and the view's top: drawn whole, it crossed the top (found by Copilot reviewing #653).
    /// </summary>
    [Fact]
    public void APageAboveItsLine_KeepsItsRows_AndItsDocumentationUnderTheViewsTop()
    {
        var documented = Enumerable.Range(0, 12)
            .Select(i => new CodeCompletionItem($"Item{i:00}") { Documentation = FourParagraphs }).ToArray();
        var editor = Editor(Lines(40), documented);
        var host = Host(editor, height: 300);
        Settle(host);
        var grid = editor.Editor.Grid;
        var last = (int)((300 - grid.Origin.Y) / grid.Cell.Height) - 1;
        ClickAt(host, editor, last, 0);

        var frame = Type(host, "I");

        Shown(host).Should().HaveCount(12, "the rows never yield to the documentation");
        var view = frame.CodeRegions.Single();
        var top = (view.Visible ?? view.Bounds).Top;
        var shown = Find(frame.Root, node => node.Source is Text { Content: FourParagraphs });
        shown.Bounds.Top.Should().BeGreaterThanOrEqualTo(top, "the documentation stays under the view's top");
        ((Text)shown.Source).MaxLines.Should().BeInRange(1, 3, "showing the lines that fit there, cut");
    }

    private static LayoutNode Find(LayoutNode node, Func<LayoutNode, bool> match) =>
        FindOrNull(node, match) ?? throw new InvalidOperationException("nothing in the frame matches");

    private static LayoutNode? FindOrNull(LayoutNode node, Func<LayoutNode, bool> match)
    {
        if (match(node)) return node;
        foreach (var child in node)
        {
            if (FindOrNull(child, match) is { } found) return found;
        }
        return null;
    }

    // ---- a press on a row ------------------------------------------------------------------------

    [Fact]
    public void PressingTheSecondRow_AcceptsIt_AndTheCodeKeepsTheKeyboard()
    {
        var editor = Editor(Lines(20), new CodeCompletionItem("Column"), new CodeCompletionItem("ColorToken"),
            new CodeCompletionItem("Count"));
        string? told = null;
        editor.OnChanged = text => told = text;
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);
        Type(host, "Co");
        var rows = host.Semantics().Where(node => node.Role == SemanticRole.Option).ToList();
        rows.Select(row => row.Label).Should().Equal(["ColorToken", "Column", "Count"]);

        PressAt(host, rows[1].Bounds.X + 4, rows[1].Bounds.Y + rows[1].Bounds.Height / 2);

        editor.Editor.Document.Line(1).Should().Be("Column");
        editor.Editor.Caret.Should().Be(new CodePosition(1, 6), "the caret is after what was accepted");
        editor.Editor.Completion.IsOpen.Should().BeFalse("accepting closes the list");
        host.CodeTarget.Should().NotBeNull("the code still has the keyboard");
        told.Should().Contain("Column", "the app hears of the edit as it does of a key's");
    }

    [Fact]
    public void APressOnTheListThatNoRowTakes_MovesNoCaret()
    {
        var editor = Editor(Lines(20), new CodeCompletionItem("Column"), new CodeCompletionItem("ColorToken"));
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);
        var frame = Type(host, "Co");
        var offered = frame.CodeRegions.Single().Offered!.Value;

        // The page mark's column, at the list's right edge and beside every row: no row reaches it,
        // even with the touch margin a pointer's target keeps.
        PressAt(host, offered.Right - 2, offered.Y + offered.Height / 2);

        editor.Editor.Caret.Should().Be(new CodePosition(1, 2), "the code under the list was not pressed");
        editor.Editor.Completion.IsOpen.Should().BeTrue();
        host.CodeTarget.Should().NotBeNull();
    }

    [Fact]
    public void TheRowsAreNoTabStops_TheKeyboardWalksThemFromTheCode()
    {
        var editor = Editor(Lines(20), new CodeCompletionItem("Column"), new CodeCompletionItem("ColorToken"));
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);

        var frame = Type(host, "Co");

        frame.HitRegions.Should().Contain(region => region.Node.Role == PressableRole.Option, "the rows take presses");
        frame.FocusStops.Should().NotContain(stop => stop.Pressable != null && stop.Pressable.Role == PressableRole.Option,
            "Tab never lands on a row: the arrows walk them while the code keeps the keyboard");
    }

    /// <summary>
    /// A list that left the view with its line takes no press outside the view: the area it would
    /// cover is clipped like the code it stands in, where unclipped it swallowed what was pressed
    /// below the editor (found reviewing #297).
    /// </summary>
    [Fact]
    public void AListThatLeftTheViewWithItsLine_IsClippedWithIt()
    {
        var editor = new CodeEditor(Lines(60), "csharp")
        {
            // The gutter is where a wheel reaches the vertical scroll alone: over the code, the
            // sideways scroll is the topmost one and takes it.
            ShowLineNumbers = true,
            Height = SizeValue.Fixed(200),
            Completions = [new ListProvider(new CodeCompletionItem("Column"), new CodeCompletionItem("ColorToken"))],
        };
        var page = new Column(gap: 0) { Width = SizeValue.Fill };
        page.Add(editor);
        page.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 200 }, new Text("below", TypeRole.BodyM)));
        var host = Host(page);
        Settle(host);
        ClickAt(host, editor, 1, 0);
        Type(host, "Co");

        host.ScrollBy(8, 100, 120).Should().BeTrue("the code scrolls");
        var frame = Settle(host);

        editor.Editor.Completion.IsOpen.Should().BeTrue("a scroll moves no caret, so the list stays open");
        var region = frame.CodeRegions.Single();
        var visible = region.Visible!.Value;
        WordOnScreen(frame, editor).Bottom.Should().BeLessThan(visible.Top, "the word's line left the view");
        visible.Bottom.Should().BeLessThanOrEqualTo(200.5f, "a press can land on the code only inside the editor");
        if (region.Offered is { } offered && offered.Width > 0 && offered.Height > 0)
            offered.Bottom.Should().BeLessThanOrEqualTo(visible.Bottom + 0.5f, "nor on the list, outside it");
    }

    // ---- what assistive technology is told ------------------------------------------------------

    [Fact]
    public void TheRowsAnnounceAsOptionsAfterTheCodeField_TheSelectedOneSelected()
    {
        var editor = Editor(Lines(20), new CodeCompletionItem("Column"), new CodeCompletionItem("ColorToken"),
            new CodeCompletionItem("Count"));
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);
        Type(host, "Co");

        Press(host, "ArrowDown");

        var tree = host.Semantics().ToList();
        var field = tree.FindIndex(node => node.Role == SemanticRole.CodeField);
        var options = tree.Select((node, index) => (node, index)).Where(pair => pair.node.Role == SemanticRole.Option).ToList();
        options.Should().HaveCount(3);
        options.Should().OnlyContain(pair => pair.index > field, "the options come after the field they belong to");
        options.Single(pair => pair.node.Selected == true).node.Label.Should().Be("Column",
            "the second entry is the one the keyboard is on");
        options.Count(pair => pair.node.Selected == false).Should().Be(2);
    }

    // ---- what an editor completes from ----------------------------------------------------------

    [Fact]
    public void ANewCSharpEditor_CompletesTheLanguagesWordsAndTheDocuments()
    {
        var editor = new CodeEditor("result = 1;", "csharp") { ShowLineNumbers = false, Height = SizeValue.Fill };
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 0, 11);
        host.KeyDown("Enter");
        Settle(host);

        Type(host, "re");

        Shown(host).Should().StartWith(["readonly", "record", "ref", "required", "result", "return"]);
    }

    [Fact]
    public void AnEmptyListOfProviders_CompletesNothing()
    {
        var editor = new CodeEditor("result = 1;", "csharp")
        {
            ShowLineNumbers = false,
            Height = SizeValue.Fill,
            Completions = [],
        };
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 0, 11);
        host.KeyDown("Enter");
        Settle(host);

        var frame = Type(host, "re");

        frame.CodeRegions.Single().Offered.Should().BeNull("no list shows");
        Shown(host).Should().BeEmpty();
    }

    [Fact]
    public void AReadOnlyEditor_CompletesNothing()
    {
        var editor = new CodeEditor("result = 1;\nre", "csharp")
        {
            ShowLineNumbers = false,
            Height = SizeValue.Fill,
            ReadOnly = true,
        };
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 2);

        editor.Editor.Completion.Invoke().Should().BeFalse("a read-only editor asks nothing");
        Settle(host).CodeRegions.Single().Offered.Should().BeNull();
    }

    /// <summary>
    /// An editor a parent makes read-only while its list shows closes the list: it stayed open until a
    /// key closed it, in an editor that completes nothing (found by Copilot reviewing #653).
    /// </summary>
    [Fact]
    public void AnEditorMadeReadOnlyWhileItsListShows_ClosesIt()
    {
        var editor = Editor(Lines(20), new CodeCompletionItem("Column"), new CodeCompletionItem("ColorToken"));
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);
        Type(host, "Co");
        Shown(host).Should().NotBeEmpty();

        editor.AdoptConfig(new CodeEditor(Lines(20), "csharp")
        {
            ShowLineNumbers = false,
            Height = SizeValue.Fill,
            ReadOnly = true,
            Completions = editor.Completions,
        });
        var frame = Settle(host);

        editor.Editor.Completion.IsOpen.Should().BeFalse("a read-only editor completes nothing");
        frame.CodeRegions.Single().Offered.Should().BeNull("and draws no list");
    }

    /// <summary>
    /// A provider an app added to the controller's completion itself, the way the controller's own
    /// documentation tells an IDE to add its language service, stays beside the built-ins: the editor
    /// cleared the list on its first build, and the service never answered (found reviewing #297).
    /// </summary>
    [Fact]
    public void AProviderAddedToTheController_StaysBesideTheBuiltIns()
    {
        var editor = new CodeEditor(Lines(20), "csharp") { ShowLineNumbers = false, Height = SizeValue.Fill };
        editor.Editor.Completion.Providers.Add(new ListProvider(new CodeCompletionItem("Cobalt")));
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);

        Type(host, "Co");

        Shown(host).Should().Contain("Cobalt", "the app's provider answers")
            .And.Contain("Column", "and so do the document's words");
    }

    [Fact]
    public void AParentRebuildingWithTheSameProviders_KeepsTheListOpen()
    {
        var provider = new ListProvider(new CodeCompletionItem("Column"), new CodeCompletionItem("ColorToken"));
        var editor = new CodeEditor(Lines(20), "csharp")
        {
            ShowLineNumbers = false,
            Height = SizeValue.Fill,
            Completions = [provider],
        };
        var host = Host(editor);
        Settle(host);
        ClickAt(host, editor, 1, 0);
        Type(host, "Co");
        var handed = editor.Editor.Completion.Providers.ToList();

        // What the reconciler does with a parent's next build: the retained editor adopts the fresh
        // one's configuration, a new list around the same provider.
        editor.AdoptConfig(new CodeEditor(Lines(20), "csharp") { Completions = [provider] });
        Settle(host);

        editor.Editor.Completion.IsOpen.Should().BeTrue();
        editor.Editor.Completion.Providers.Should().Equal(handed, "the same providers are not handed again");
    }

    /// <summary>
    /// The editor takes out the very providers it put in. Taken out by equality, a provider that
    /// equals one of them, a record with the same values, went in their place though the app had
    /// added it (found by Copilot reviewing #653).
    /// </summary>
    [Fact]
    public void AProviderThatEqualsTheEditors_StaysWhenTheEditorTakesItsOwnOut()
    {
        var apps = new EqualProvider("words");
        var editor = new CodeEditor(Lines(20), "csharp")
        {
            ShowLineNumbers = false,
            Height = SizeValue.Fill,
            Completions = [new EqualProvider("words")],
        };
        editor.Editor.Completion.Providers.Add(apps);
        var host = Host(editor);
        Settle(host);
        editor.Editor.Completion.Providers.Should().HaveCount(2, "the app's provider and the editor's");

        editor.AdoptConfig(new CodeEditor(Lines(20), "csharp") { Completions = [] });
        Settle(host);

        editor.Editor.Completion.Providers.Should().ContainSingle()
            .Which.Should().BeSameAs(apps, "the editor took out its own, and only its own");
    }

    /// <summary>A provider that equals any other of the same name, as a record does.</summary>
    private sealed record EqualProvider(string Name) : ICodeCompletionProvider
    {
        public Task<CodeCompletionList> CompleteAsync(CodeDocument document, CodePosition position,
            CodeCompletionContext context, CancellationToken cancellation) =>
            Task.FromResult(new CodeCompletionList(Array.Empty<CodeCompletionItem>()));
    }

    /// <summary>
    /// A parent that keeps one list of providers and adds to it hands what it added on its next build:
    /// the editor recorded the list itself, which compared with itself never held other providers
    /// (found by Copilot reviewing #653).
    /// </summary>
    [Fact]
    public void AListOfProvidersChangedInPlace_HandsWhatItGained()
    {
        var providers = new List<ICodeCompletionProvider> { new ListProvider(new CodeCompletionItem("Column")) };
        var editor = new CodeEditor(Lines(20), "csharp")
        {
            ShowLineNumbers = false,
            Height = SizeValue.Fill,
            Completions = providers,
        };
        var host = Host(editor);
        Settle(host);
        editor.Editor.Completion.Providers.Should().Equal(providers, "the first build hands the list");

        var gained = new ListProvider(new CodeCompletionItem("Cobalt"));
        providers.Add(gained);
        editor.AdoptConfig(new CodeEditor(Lines(20), "csharp") { Completions = providers });
        Settle(host);

        editor.Editor.Completion.Providers.Should().Equal(providers, "the list holds a provider it did not");
    }

    // ---- pixels ----------------------------------------------------------------------------------

    /// <summary>
    /// The list at the caret, light and dark, on the reference backend: its frame and its shadow, the
    /// selected entry's coat, the letters and the matched characters, the page mark and the selected
    /// entry's documentation. The state the picture is of is asserted beside it, so a golden cannot
    /// bless a list that shows the wrong thing.
    /// </summary>
    [Theory]
    [InlineData(ThemeMode.Light, "code-completion-light")]
    [InlineData(ThemeMode.Dark, "code-completion-dark")]
    public void TheListAtTheCaret_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        var editor = Editor(Lines(8),
            new CodeCompletionItem("ColorToken", CodeCompletionKind.Struct) { Detail = "struct" },
            new CodeCompletionItem("Column", CodeCompletionKind.Class)
            {
                Detail = "class",
                Documentation = "Lays its children out in a column.",
            },
            new CodeCompletionItem("Count", CodeCompletionKind.Property) { Detail = "int" });
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(360, 240);
        var host = new PhotonHost(editor, PhotonTheme.Instance, mode, 360, 240) { Density = Density.Compact };
        Settle(host);
        ClickAt(host, editor, 1, 0);
        Type(host, "Co");
        Press(host, "ArrowDown");
        host.RenderFrame(new DisplayListBuilder(), timeMs: 500);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs: 1000);
        backend.Render(builder.Build(), surface);

        Shown(host).Should().Equal(["ColorToken, struct", "Column, class", "Count, int"]);
        editor.Editor.Completion.Selected.Should().Be(1, "the second entry is selected, and its documentation shows");
        GoldenImage.Match(surface, golden);
    }
}
