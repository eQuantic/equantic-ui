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
/// The diff view as a COMPONENT (slice 2b of docs/CODE-EDITOR-PLAN.md): two sides kept level, the
/// inline view's removed lines, a fold that opens, the steps through the changes, a modified side that
/// edits and is compared again, and a patch's view that reads only. On Photon's layout, the same tree
/// the web realizes.
/// </summary>
public class CodeDiffComponentTests
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

    private static PhotonHost Host(VisualNode root, float width = 900, float height = 700) =>
        new(root, PhotonTheme.Instance, ThemeMode.Light, width, height) { TextRasterizer = new FixedWidthRasterizer() };

    private static RealizeResult Settle(PhotonHost host)
    {
        host.RenderFrame(new DisplayListBuilder());
        return host.RenderFrame(new DisplayListBuilder());
    }

    /// <summary>A press the way a pointer makes one: down and up at the middle of the region, through
    /// the host's own dispatch, which a code surface under a pressable must not take from it.</summary>
    private static void Press(PhotonHost host, HitRegion region)
    {
        var x = region.Bounds.X + region.Bounds.Width / 2;
        var y = region.Bounds.Y + region.Bounds.Height / 2;
        host.PressDown(x, y);
        host.PressUp(x, y);
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

    private static List<Framework.LayoutNode> Texts(RealizeResult frame, string content) =>
        All(frame.Root, node => node.Source is Text { Content: var text } && text == content);

    private static string Lines(int count, Func<int, string> line) =>
        string.Join("\n", Enumerable.Range(0, count).Select(line));

    [Fact]
    public void SideBySide_TheTwoSidesStayLevel()
    {
        var frame = Settle(Host(new CodeDiff("a\nb\nc", "a\nX\nY\nc", "plaintext")));

        var both = Texts(frame, "c");
        both.Should().HaveCount(2, "each side draws its own c");
        both[0].Bounds.Y.Should().BeApproximately(both[1].Bounds.Y, 0.5f,
            "the original is padded after b, so the c after the change is level on both sides");
        both[0].Bounds.X.Should().BeLessThan(both[1].Bounds.X, "the original is on the left");
    }

    [Fact]
    public void Inline_DrawsTheRemovedLinesBetweenTheLinesThatReplacedThem()
    {
        var frame = Settle(Host(new CodeDiff("a\nb\nc", "a\nX\nc", "plaintext") { Inline = true }));

        var a = Texts(frame, "a").Should().ContainSingle("inline shows one column").Subject;
        var b = Texts(frame, "b").Should().ContainSingle().Subject;
        var x = Texts(frame, "X").Should().ContainSingle().Subject;
        a.Bounds.Y.Should().BeLessThan(b.Bounds.Y);
        b.Bounds.Y.Should().BeLessThan(x.Bounds.Y, "the removed line comes before the line that replaced it");
        b.Bounds.X.Should().BeApproximately(x.Bounds.X, 0.5f, "in the same column");
    }

    /// <summary>
    /// A press on a folded run opens it on both sides and leaves the caret where it was: the press
    /// was the row's, not the code's. The keyboard is then in the side pressed, so F7 still steps: the
    /// row that held it is gone once the run opens, and on the web the focus fell to the page, where
    /// the diff's own keys do not answer.
    /// </summary>
    [Fact]
    public void AFoldOpensOnAPress_AndLeavesTheKeyboardInTheDiff()
    {
        var original = Lines(100, i => $"line {i}");
        var modified = original.Replace("line 90", "changed 90");
        var diff = new CodeDiff(original, modified, "plaintext");
        var host = Host(diff);
        var frame = Settle(host);

        Texts(frame, "line 10").Should().BeEmpty("the unchanged run before the change is folded");
        var fold = frame.HitRegions.Last(region => region.Node.Label == SdkStrings.UnchangedLines(87));
        var caret = diff.Editor.Caret;
        Press(host, fold);
        frame = Settle(host);

        Texts(frame, "line 10").Should().HaveCount(2, "the run is open on both sides");
        diff.Editor.Caret.Should().Be(caret, "the press was the row's, not the code's");
        host.KeyDown("F7").Should().BeTrue("the diff's own keys still answer");
        host.CodeTarget.Should().NotBeNull("the keyboard is in the side whose fold was pressed");
        host.CodeTarget!.Model.Should().BeSameAs(diff.Editor);
    }

    [Fact]
    public void NextChange_MovesTheCaretToTheChangeAfterIt_AndWraps()
    {
        var original = Lines(100, i => $"line {i}");
        var modified = original.Replace("line 10", "first").Replace("line 80", "second");
        var diff = new CodeDiff(original, modified, "plaintext");
        var host = Host(diff);
        var frame = Settle(host);

        diff.Editor.Caret.Line.Should().Be(10, "a diff opens at its first change");
        Press(host, frame.HitRegions.Single(region => region.Node.Label == SdkStrings.NextChange));
        frame = Settle(host);
        diff.Editor.Caret.Line.Should().Be(80);
        Texts(frame, "second").Should().NotBeEmpty("the change the step reached is drawn");

        Press(host, frame.HitRegions.Single(region => region.Node.Label == SdkStrings.NextChange));
        Settle(host);
        diff.Editor.Caret.Line.Should().Be(10, "past the last change the step wraps to the first");
    }

    /// <summary>
    /// A change that removed the end of the text starts past its last line, so the step puts the
    /// caret on the last line: the next step still wraps to the first change. It found that change
    /// after the caret it had left there, every time, and stayed on it.
    /// </summary>
    [Fact]
    public void NextChange_WrapsPastAChangeThatRemovedTheEnd()
    {
        var original = Lines(40, i => $"line {i}");
        var modified = Lines(30, i => i == 5 ? "first" : $"line {i}");
        var diff = new CodeDiff(original, modified, "plaintext");
        var host = Host(diff);
        var frame = Settle(host);

        diff.Editor.Caret.Line.Should().Be(5, "a diff opens at its first change");
        Press(host, frame.HitRegions.Single(region => region.Node.Label == SdkStrings.NextChange));
        frame = Settle(host);
        diff.Editor.Caret.Line.Should().Be(29, "lines 30 to 39 are gone, after the last line left");

        Press(host, frame.HitRegions.Single(region => region.Node.Label == SdkStrings.NextChange));
        Settle(host);
        diff.Editor.Caret.Line.Should().Be(5, "past the last change the step wraps to the first");
    }

    /// <summary>
    /// F7 is the diff's own: of two on one screen, the one the keyboard is in steps, and with the
    /// keyboard in neither the key is not taken. Page-wide, F7 typed in the first diff stepped the
    /// last one mounted.
    /// </summary>
    [Fact]
    public void F7_StepsTheDiffTheKeyboardIsIn()
    {
        var original = Lines(30, i => $"line {i}");
        var modified = original.Replace("line 5", "first").Replace("line 20", "second");
        var top = new CodeDiff(original, modified, "plaintext") { MaxHeight = 150 };
        var bottom = new CodeDiff(original, modified, "plaintext") { MaxHeight = 150 };
        var page = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        page.Add(top);
        page.Add(bottom);
        var host = Host(page);
        var frame = Settle(host);

        host.KeyDown("F7").Should().BeFalse("the keyboard is in neither diff");

        // Into the top diff's code, on its caret's own row: the first change, line 5.
        var region = frame.CodeRegions.Single(r => ReferenceEquals(r.Surface.Model, top.Editor));
        var caret = top.Editor.Carets[0];
        host.PressDown(region.Bounds.X + caret.X + 1, region.Bounds.Y + caret.Y + caret.Height / 2);
        host.PressUp(region.Bounds.X + caret.X + 1, region.Bounds.Y + caret.Y + caret.Height / 2);
        Settle(host);
        top.Editor.Caret.Line.Should().Be(5);

        host.KeyDown("F7").Should().BeTrue();
        Settle(host);

        top.Editor.Caret.Line.Should().Be(20, "the diff the keyboard is in stepped to its next change");
        bottom.Editor.Caret.Line.Should().Be(5, "the other stayed where it opened");
    }

    [Fact]
    public void EditingTheModifiedSide_ComparesAgain_AndTellsTheApp()
    {
        string? told = null;
        var diff = new CodeDiff("a\nb", "a\nb", "plaintext") { OnChanged = text => told = text };
        var host = Host(diff);
        var frame = Settle(host);
        Texts(frame, "+0").Should().ContainSingle("nothing differs yet");

        var region = frame.CodeRegions.Single(r => ReferenceEquals(r.Surface.Model, diff.Editor));
        var grid = region.Surface.Grid();
        host.PressDown(region.Bounds.X + grid.Origin.X + 1, region.Bounds.Y + grid.Origin.Y + 2);
        host.PressUp(region.Bounds.X + grid.Origin.X + 1, region.Bounds.Y + grid.Origin.Y + 2);
        host.RenderFrame(new DisplayListBuilder());
        host.TextInput("x");
        frame = Settle(host);

        told.Should().Be("xa\nb");
        Texts(frame, "+1").Should().ContainSingle("the edited line is a change now");
        Texts(frame, "−1").Should().ContainSingle();
    }

    [Fact]
    public void APatchsView_ReadsOnly_AndSaysWhatThePatchLeftOut()
    {
        var file = CodePatch.Parse("""
            --- a/f.cs
            +++ b/f.cs
            @@ -1,2 +1,2 @@
             keep
            -old
            +new
            @@ -20,1 +20,1 @@ class Far
            -gone
            +here
            """)[0];
        var diff = CodeDiff.OfPatch(file, "csharp");
        var host = Host(diff);
        var frame = Settle(host);

        Texts(frame, "@@ -20,1 +20,1 @@ class Far").Should().HaveCount(2, "each side says what the patch says in its place");
        diff.Editor.ReadOnly.Should().BeTrue("a patch's lines are not the file, and editing them edits nothing");
        Texts(frame, "20").Should().NotBeEmpty("a line is numbered as its file numbers it");
    }

    /// <summary>
    /// A patch's sides are the lines it quotes, and the editors hold THOSE lines: a line of a file with
    /// mixed endings can hold a bare carriage return, which a patch keeps inside the line and a text
    /// read again splits. Read again, the editor had a line more than the rows, the numbers and the
    /// marks were counted on, and everything after it was drawn a line off.
    /// </summary>
    [Fact]
    public void APatchsEditors_HoldTheLinesThePatchQuotes()
    {
        var file = CodePatch.Parse("--- a/f.txt\n+++ b/f.txt\n@@ -1,2 +1,2 @@\n keep\n-old\n+new\rpart\n")[0];
        var diff = CodeDiff.OfPatch(file);
        Settle(Host(diff));

        diff.Editor.Document.LineCount.Should().Be(2, "the patch quotes two lines of the modified file");
        diff.Editor.Document.Line(1).Should().Be("new\rpart");
    }

    [Fact]
    public void TheToolbarSpeaksTheInterfacesLanguage()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
            var frame = Settle(Host(new CodeDiff("a", "b", "plaintext")));

            frame.HitRegions.Select(region => region.Node.Label).Should().Contain(["Próxima alteração", "Alteração anterior", "Mostrar em linha"]);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
