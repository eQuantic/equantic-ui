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
        var before = Settle(host).CodeRegions.Single().Path;

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
        Settle(host);
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

    [Fact]
    public void TheBarsCloseButtonSpeaksTheInterfacesLanguage()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
            var host = Host(new CodeEditor("x", "csharp"));
            Settle(host);

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
