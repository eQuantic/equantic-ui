using System.Globalization;
using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The editor as a COMPONENT (slice 1c of docs/CODE-EDITOR-PLAN.md, defects 8 to 11): the find bar
/// over the code and where the keyboard goes as it opens and closes, what the app hears, the
/// caption, and an editor that fills the place it is given.
/// </summary>
public class CodeEditorComponentTests
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

    private static PhotonHost Host(VisualNode root, float width = 500, float height = 400) =>
        new(root, PhotonTheme.Instance, ThemeMode.Light, width, height)
        {
            TextRasterizer = new FixedWidthRasterizer(),
        };

    /// <summary>Two frames: the second knows how big the viewport turned out to be.</summary>
    private static RealizeResult Settle(PhotonHost host)
    {
        host.RenderFrame(new DisplayListBuilder());
        return host.RenderFrame(new DisplayListBuilder());
    }

    private static RealizeResult Frame(PhotonHost host) => host.RenderFrame(new DisplayListBuilder());

    private static RealizeResult Press(PhotonHost host, string key, KeyModifiers modifiers = KeyModifiers.None)
    {
        host.KeyDown(key, modifiers);
        return Frame(host);
    }

    private static RealizeResult Type(PhotonHost host, string text)
    {
        host.TextInput(text);
        return Frame(host);
    }

    /// <summary>Puts the keyboard in the code at its first column, the way a click does.</summary>
    private static void ClickInto(PhotonHost host, RealizeResult frame)
    {
        var region = frame.CodeRegions.Single();
        var grid = region.Surface.Grid();
        var x = region.Bounds.X + grid.Origin.X + 1;
        var y = region.Bounds.Y + grid.Origin.Y + 2;
        host.PressDown(x, y);
        host.PressUp(x, y);
        Frame(host);
    }

    /// <summary>⌘F. The chord is the editor's own, so the keyboard has to be in the editor for it to
    /// answer: every test puts it there first, as a person does.</summary>
    private static RealizeResult OpenFind(PhotonHost host) => Press(host, "f", KeyModifiers.Command);

    // ---- the find bar --------------------------------------------------------------------------

    /// <summary>
    /// Opening find wrapped the code in a new layer, so the surface moved in the tree, and a surface
    /// that moves is a new one to every host: its scroll went back to the top, a reveal on it was
    /// "the first time it was seen", and Photon's keyboard pointed at a path nothing had any more.
    /// </summary>
    [Fact]
    public void OpeningFindLeavesTheCodeWhereItWas()
    {
        var host = Host(new CodeEditor("searchable text", "csharp") { ShowLineNumbers = false });
        var settled = Settle(host);
        var before = settled.CodeRegions.Single().Path;
        ClickInto(host, settled);

        var after = OpenFind(host);

        after.TextRegions.Should().ContainSingle("the bar is open");
        after.CodeRegions.Single().Path.Should().Be(before);
    }

    [Fact]
    public void OpeningFindHandsTheFieldTheKeyboard_EvenFromTheCode()
    {
        var editor = new CodeEditor("one needle two", "csharp") { ShowLineNumbers = false };
        var host = Host(editor);
        ClickInto(host, Settle(host));
        host.CodeTarget.Should().NotBeNull("the code has the keyboard before find opens");

        OpenFind(host);
        Type(host, "needle");

        (host.TextTarget?.Placeholder).Should().Be(SdkStrings.Find);
        editor.Editor.Document.Text.Should().Be("one needle two", "what was typed went to the bar");
    }

    [Fact]
    public void EscapeClosesFind_AndTheCodeHasTheKeyboardAgain()
    {
        var editor = new CodeEditor("one needle two", "csharp") { ShowLineNumbers = false };
        var host = Host(editor);
        ClickInto(host, Settle(host));
        OpenFind(host);

        Press(host, "Escape").TextRegions.Should().BeEmpty("Escape closes the bar");
        Frame(host);

        host.CodeTarget.Should().NotBeNull("the keyboard goes back to the code it came from");
        Type(host, "x");
        editor.Editor.Document.Text.Should().Be("xone needle two");
    }

    [Fact]
    public void EscapeInTheCodeClosesTheBarToo()
    {
        var editor = new CodeEditor("one needle two", "csharp") { ShowLineNumbers = false };
        var host = Host(editor);
        ClickInto(host, Settle(host));
        OpenFind(host);
        ClickInto(host, Frame(host));

        Press(host, "Escape").TextRegions.Should().BeEmpty("the bar belongs to the editor, wherever its keyboard is");
        host.CodeTarget.Should().NotBeNull("closing the bar is all that Escape did");
    }

    [Fact]
    public void FindOpenedAgainHasTheKeyboardAgain()
    {
        var host = Host(new CodeEditor("one needle two", "csharp") { ShowLineNumbers = false });
        ClickInto(host, Settle(host));
        OpenFind(host);
        Press(host, "Escape");
        Frame(host);

        OpenFind(host);

        (host.TextTarget?.Placeholder).Should().Be(SdkStrings.Find,
            "a field that asked for the keyboard asks again every time it appears");
    }

    /// <summary>
    /// Enter walks the matches: the next one is selected, brought into view and announced to the
    /// app, and the field keeps the keyboard, so the one after it is one more Enter away.
    /// </summary>
    [Fact]
    public void EnterWalksTheMatches_RevealingEach_AndTellingTheApp()
    {
        var code = string.Join("\n", Enumerable.Range(0, 81).Select(i => i is 60 or 80 ? "needle" : $"line {i}"));
        var heard = new List<CodeRange>();
        var editor = new CodeEditor(code, "csharp")
        {
            ShowLineNumbers = false,
            MaxHeight = 120,
            OnSelectionChanged = heard.Add,
        };
        var host = Host(editor);
        ClickInto(host, Settle(host));
        OpenFind(host);
        Type(host, "needle");
        heard.Clear();

        Press(host, "Enter");
        Frame(host);
        var frame = Frame(host);

        var match = new CodeRange(new CodePosition(60, 0), new CodePosition(60, 6));
        editor.Editor.Selection.Should().Be(match);
        heard.Should().Equal([match], "a selection the find bar moved is a selection that moved");
        var region = frame.CodeRegions.Single();
        var viewport = frame.ScrollRegions.First(r => r.Axis == ScrollAxis.Vertical
            && region.Path.StartsWith(r.Path, StringComparison.Ordinal));
        var caretY = region.Bounds.Y + region.Surface.Model.Carets[0].Y;
        caretY.Should().BeInRange(viewport.Bounds.Y, viewport.Bounds.Y + viewport.Bounds.Height,
            "the match is brought into view");

        (host.TextTarget?.Placeholder).Should().Be(SdkStrings.Find, "the field keeps the keyboard");
        Press(host, "Enter");
        editor.Editor.Selection.Start.Line.Should().Be(80, "the next Enter is the next match");
    }

    /// <summary>
    /// The bar counts what ITS field looks for. The app's own <c>Search</c> still marks the code, and
    /// with the field empty the bar showed that search's count beside it, a count for nothing typed.
    /// </summary>
    [Fact]
    public void TheBarCountsWhatItsFieldLooksFor_NotTheAppsSearch()
    {
        var editor = new CodeEditor("one two one", "csharp") { ShowLineNumbers = false, Search = "one" };
        var host = Host(editor);
        ClickInto(host, Settle(host));

        var opened = OpenFind(host);
        Count(opened.Root, node => node.Source is Text { Content: var text } && text.Contains('/')).Should().Be(0,
            "nothing is typed in the field yet");

        var typed = Type(host, "two");
        Count(typed.Root, node => node.Source is Text { Content: "0/1" }).Should().Be(1);
    }

    [Fact]
    public void TheBarsCloseButtonSpeaksTheInterfacesLanguage()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
            var host = Host(new CodeEditor("x", "csharp"));
            ClickInto(host, Settle(host));

            var frame = OpenFind(host);

            frame.HitRegions.Select(r => r.Node.Label).Should().Contain("Fechar barra de localização");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void ABlocksCopyButtonSpeaksTheInterfacesLanguage()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
            var host = Host(new CodeBlock("var x = 1;", "csharp") { OnCopy = () => { } });

            var frame = Settle(host);

            frame.HitRegions.Select(r => r.Node.Label).Should().Contain("Copiar código");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    // ---- the keyboard ----------------------------------------------------------------------------

    [Fact]
    public void AnEditorThatAsksForTheKeyboardGetsIt()
    {
        var host = Host(new CodeEditor("x", "csharp") { Autofocus = true });

        Frame(host);

        host.CodeTarget.Should().NotBeNull("the web focuses it on mount, and so does a window");
    }

    /// <summary>
    /// An IDE puts the keyboard back in the code after a panel over it closes, or when a file opens.
    /// The request is the model's, so it is honoured by whichever host draws the surface, even when
    /// it was made before the first frame.
    /// </summary>
    [Fact]
    public void TheAppCanAskForTheKeyboard_BeforeTheFirstFrameOrAfter()
    {
        var early = new CodeEditor("x", "csharp");
        early.Editor.RequestFocus();
        var first = Host(early);
        Frame(first);
        first.CodeTarget.Should().NotBeNull("asked before it was drawn");

        var late = new CodeEditor("x", "csharp");
        var second = Host(late);
        Settle(second);
        second.CodeTarget.Should().BeNull("nothing asked yet");
        late.Editor.RequestFocus();
        Frame(second);
        second.CodeTarget.Should().NotBeNull("asked after");
    }

    [Fact]
    public void ASurfaceDrawnAgainDoesNotTakeTheKeyboardAgain()
    {
        var editor = new CodeEditor("x", "csharp");
        editor.Editor.RequestFocus();
        var host = Host(editor);
        Frame(host);
        host.KeyDown("Escape");
        Settle(host);

        host.CodeTarget.Should().BeNull("a request is honoured once");
    }

    // ---- what the app hears ----------------------------------------------------------------------

    [Fact]
    public void ACaretMoveIsNotAnEdit()
    {
        var edits = 0;
        var moves = 0;
        var editor = new CodeEditor("abc", "csharp")
        {
            ShowLineNumbers = false,
            OnChanged = _ => edits++,
            OnSelectionChanged = _ => moves++,
        };
        var host = Host(editor);
        ClickInto(host, Settle(host));
        edits = 0;
        moves = 0;

        Press(host, "ArrowRight");
        edits.Should().Be(0, "the document did not change");
        moves.Should().Be(1);

        Type(host, "x");
        edits.Should().Be(1);
    }

    // ---- the caption -----------------------------------------------------------------------------

    [Fact]
    public void TheCaptionIsDrawn()
    {
        var host = Host(new CodeEditor("x", "csharp") { Caption = "Program.cs" });

        var frame = Settle(host);

        Count(frame.Root, node => node.Source is Text { Content: "Program.cs" }).Should().Be(1);
    }

    // ---- an editor that fills its place ----------------------------------------------------------

    /// <summary>
    /// How an IDE uses the editor: as tall as its pane, with no cap to state. Without a cap there was
    /// no vertical viewport, so there was no window either, and every keystroke built every line:
    /// 100 to 213 ms per key on a 3000-line file.
    /// </summary>
    [Fact]
    public void AnEditorFillingItsPlace_BuildsOnlyTheLinesYouCanSee()
    {
        var text = string.Join("\n", Enumerable.Range(0, 4000).Select(i => $"var line{i} = {i};"));
        var host = Host(new CodeEditor(text, "csharp") { Height = SizeValue.Fill, ShowLineNumbers = false });

        var frame = Settle(host);

        var rows = Count(frame.Root, node => node.Source is Row);
        rows.Should().BeLessThan(200, "a 4000-line file must not build 4000 rows");
        rows.Should().BeGreaterThan(5, "…and it must build the ones on screen");
    }

    [Fact]
    public void AnEditorFillingItsPlace_IsAsTallAsThePlace()
    {
        var host = Host(new CodeEditor("one\ntwo", "csharp") { Height = SizeValue.Fill }, 500, 400);

        var frame = Settle(host);

        var viewport = frame.ScrollRegions.First(r => r.Axis == ScrollAxis.Vertical);
        viewport.Bounds.Height.Should().BeApproximately(400, 0.5f, "a short file in a tall pane still fills it");
    }

    /// <summary>
    /// A bounded editor with a cap is capped whole, slab and all. The cap was the inner viewport's
    /// alone, so a Fill editor in a pane taller than its MaxHeight scrolled its code in the top of a
    /// slab that went on, empty, to the bottom of the pane.
    /// </summary>
    [Fact]
    public void AFillEditorWithACap_IsCappedWhole()
    {
        var text = string.Join("\n", Enumerable.Range(0, 200).Select(i => $"var line{i} = {i};"));
        var host = Host(new CodeEditor(text, "csharp") { Height = SizeValue.Fill, MaxHeight = 300 }, 500, 600);

        var frame = Settle(host);

        var viewport = frame.ScrollRegions.First(r => r.Axis == ScrollAxis.Vertical);
        viewport.Bounds.Height.Should().BeApproximately(300, 0.5f, "the code scrolls in the capped height");
        var layers = First(frame.Root, node => node.Source is Stack)!;
        layers.Bounds.Height.Should().BeApproximately(300, 0.5f, "and the editor is no taller than the code's viewport");
    }

    private static Framework.LayoutNode? First(Framework.LayoutNode node, Func<Framework.LayoutNode, bool> predicate)
    {
        if (predicate(node)) return node;
        foreach (var child in node.Children)
            if (First(child, predicate) is { } found) return found;
        return null;
    }

    /// <summary>
    /// A step through the matches the bar holds lands where a search of the whole file would have:
    /// forward from the selection's end, back from its start, wrapping at either end.
    /// </summary>
    [Fact]
    public void AStepThroughTheHeldMatches_LandsWhereASearchWould()
    {
        var random = new Random(2026_09_24);
        for (var round = 0; round < 300; round++)
        {
            var text = string.Join("\n", Enumerable.Range(0, random.Next(1, 8))
                .Select(_ => string.Concat(Enumerable.Range(0, random.Next(0, 12)).Select(_ => "ab "[random.Next(3)]))));
            var controller = new CodeEditorController(text, CodeLanguages.For("csharp"));
            var needle = new[] { "a", "b", "ab", "ba" }[random.Next(4)];
            var matches = controller.FindAll(needle);
            var end = controller.Document.End;
            var line = random.Next(0, end.Line + 1);
            var start = new CodePosition(line, random.Next(0, controller.Document.Line(line).Length + 1));
            controller.Selection = new CodeRange(start, controller.Document.Clamp(start with { Column = start.Column + random.Next(0, 3) }));

            foreach (var backward in new[] { false, true })
                controller.NextOf(matches, backward).Should().Be(Linear(matches, controller.Selection, backward),
                    $"round {round}, {(backward ? "back" : "forward")}");
        }

        static CodeRange? Linear(IReadOnlyList<CodeRange> matches, CodeRange selection, bool backward)
        {
            if (matches.Count == 0) return null;
            if (backward)
            {
                for (var i = matches.Count - 1; i >= 0; i--)
                    if (matches[i].End <= selection.Start) return matches[i];
                return matches[^1];
            }
            foreach (var match in matches)
                if (match.Start >= selection.End) return match;
            return matches[0];
        }
    }

    /// <summary>
    /// Enter steps through the matches of what the field holds when it is pressed. A key typed and
    /// Enter pressed before the next build stepped through the matches of the text before the key.
    /// </summary>
    [Fact]
    public void EnterStepsThroughWhatTheFieldHoldsNow_NotWhatTheLastBuildSaw()
    {
        var editor = new CodeEditor("fo x foo", "csharp") { ShowLineNumbers = false };
        var host = Host(editor);
        ClickInto(host, Settle(host));
        OpenFind(host);
        Type(host, "fo");

        host.TextInput("o");
        host.KeyDown("Enter");
        Frame(host);

        editor.Editor.Selection.Should().Be(new CodeRange(new CodePosition(0, 5), new CodePosition(0, 8)),
            "the only foo, where the fo before it would have stepped to the first fo");
    }

    /// <summary>
    /// A cap set on a bounded editor, or taken away, leaves the code where it was in the tree: the box
    /// that caps it is always there. It was there only while it capped, so the surface moved, the
    /// scroll went back to the top and, on Photon, the keyboard pointed at a path nothing had.
    /// </summary>
    [Fact]
    public void ACapSetOrTakenAway_LeavesTheCodeWhereItWas()
    {
        var text = string.Join("\n", Enumerable.Range(0, 200).Select(i => $"var line{i} = {i};"));
        var pane = new CappedPane(text);
        var host = Host(pane, 500, 600);
        var before = Settle(host).CodeRegions.Single().Path;

        pane.Cap(300);
        var capped = Settle(host).CodeRegions.Single().Path;
        pane.Cap(0);
        var after = Settle(host).CodeRegions.Single().Path;

        capped.Should().Be(before);
        after.Should().Be(before);
    }

    private sealed class CappedPane(string text) : Primitives.StatefulComponent
    {
        private float _cap;

        public void Cap(float cap) => SetState(() => _cap = cap);

        public override VisualNode Build(ComponentContext context) =>
            new CodeEditor(text, "csharp") { Height = SizeValue.Fill, MaxHeight = _cap };
    }

    /// <summary>A parent that decides how tall its editor is, and builds it anew as a parent does.</summary>
    private sealed class Pane(string text) : Primitives.StatefulComponent
    {
        public SizeValue EditorHeight = SizeValue.Fill;

        public void Resize(SizeValue height) => SetState(() => EditorHeight = height);

        public override VisualNode Build(ComponentContext context) =>
            new CodeEditor(text, "csharp") { Height = EditorHeight };
    }

    /// <summary>
    /// An editor that stops being bounded shows every line, and builds them. The window it had while
    /// it had a viewport went on limiting the build, and with no viewport left to report anything it
    /// was never let go: switched from Fill to Hug well down a file, it built one screen of lines
    /// and every other line was blank. (On the web the offset stayed stale the other way too: a
    /// viewport mounted again started at the top while the window stayed where it had been, and
    /// the web reports an offset only when something scrolls.)
    /// </summary>
    [Fact]
    public void AnEditorThatStopsBeingBounded_BuildsEveryLineAgain()
    {
        var text = string.Join("\n", Enumerable.Range(0, 400).Select(i => $"var line{i} = {i};"));
        var pane = new Pane(text);
        var host = Host(pane, 500, 400);
        var frame = Settle(host);
        var viewport = frame.ScrollRegions.First(r => r.Axis == ScrollAxis.Vertical);
        // Over the gutter, which the vertical viewport holds and the sideways one does not.
        host.ScrollBy(viewport.Bounds.X + 4, viewport.Bounds.Y + 20, 4000).Should().BeTrue();
        Settle(host);

        pane.Resize(SizeValue.Hug);
        frame = Settle(host);

        // The last line's number (only the gutter says 400) and the first line's name (only its code
        // says line0): the two ends of the file, one screen of lines apart from where it was.
        Count(frame.Root, node => node.Source is Text { Content: "400" }).Should().Be(1,
            "an editor with no viewport shows every one of its 400 lines, the last");
        Count(frame.Root, node => node.Source is Text { Content: "line0" }).Should().Be(1, "…and the first");
    }

    /// <summary>
    /// The MARKS are windowed with the lines. Every match and every selected line built a mark, and
    /// a map of its line's cells, on every build, in view or not: a select-all over 4000 lines with
    /// a search on built 8000 boxes a frame.
    /// </summary>
    [Fact]
    public void AnEditorBuildsTheMarksOfTheLinesInViewOnly()
    {
        var text = string.Join("\n", Enumerable.Range(0, 4000).Select(i => $"var needle{i} = {i};"));
        var editor = new CodeEditor(text, "csharp")
        {
            MaxHeight = 300,
            ShowLineNumbers = false,
            Search = "needle",
            MatchBrackets = false,
        };
        editor.Editor.SelectAll();
        var host = Host(editor);

        var frame = Settle(host);

        var marks = Count(frame.Root, node => node.Source is Positioned);
        marks.Should().BeLessThan(200, "4000 matches and 4000 selected lines must not build 8000 marks");
        marks.Should().BeGreaterThan(20, "…and the ones in view are there");
    }

    private static int Count(Framework.LayoutNode node, Func<Framework.LayoutNode, bool> predicate)
    {
        var total = predicate(node) ? 1 : 0;
        foreach (var child in node.Children) total += Count(child, predicate);
        return total;
    }
}
