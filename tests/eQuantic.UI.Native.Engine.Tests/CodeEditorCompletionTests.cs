using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
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

    private static PhotonHost Host(VisualNode root, float width = 600, float height = 400) =>
        new(root, PhotonTheme.Instance, ThemeMode.Light, width, height)
        {
            TextRasterizer = new FixedWidthRasterizer(),
            Density = Density.Compact,
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

        // The frame's padding, above the first row.
        PressAt(host, offered.X + offered.Width / 2, offered.Y + 2);

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
}
