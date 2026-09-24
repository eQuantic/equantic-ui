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

    [Fact]
    public void AFoldOpensOnAPress()
    {
        var original = Lines(100, i => $"line {i}");
        var modified = original.Replace("line 90", "changed 90");
        var host = Host(new CodeDiff(original, modified, "plaintext"));
        var frame = Settle(host);

        Texts(frame, "line 10").Should().BeEmpty("the unchanged run before the change is folded");
        var fold = frame.HitRegions.First(region => region.Node.Label == SdkStrings.UnchangedLines(87));
        fold.Node.OnPressed!();
        frame = Settle(host);

        Texts(frame, "line 10").Should().HaveCount(2, "the run is open on both sides");
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
        frame.HitRegions.Single(region => region.Node.Label == SdkStrings.NextChange).Node.OnPressed!();
        frame = Settle(host);
        diff.Editor.Caret.Line.Should().Be(80);
        Texts(frame, "second").Should().NotBeEmpty("the change the step reached is drawn");

        frame.HitRegions.Single(region => region.Node.Label == SdkStrings.NextChange).Node.OnPressed!();
        Settle(host);
        diff.Editor.Caret.Line.Should().Be(10, "past the last change the step wraps to the first");
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
