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
/// The panel a <see cref="CodeEditor"/> draws over the call being written (#298), driven as a person drives
/// it: a call typed, its arguments, a word that opens the completion list beside it, a card asked for over
/// it. What is drawn is read from the frame, where the surface's notes landed, against where the engine says
/// the caret stands.
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeEditorSignatureTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeEditorSignatureTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    private sealed class FixedWidthRasterizer : ITextRasterizer
    {
        public TextRaster? Rasterize(string content, TypeStyle style, float typeScale,
            float maxWidth, int maxLines, float scale, TextAlignment align)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var width = Math.Max(1, (int)Math.Min(content.Length * 8 * scale, maxWidth * scale));
            var height = Math.Max(1, (int)(style.LineHeight * scale));
            return new TextRaster(width, height, new byte[width * height]);
        }
    }

    private sealed class Explainer : ICodeHoverProvider
    {
        public Task<CodeHover?> HoverAsync(CodeDocument document, CodePosition position, CancellationToken cancellation) =>
            Task.FromResult<CodeHover?>(new CodeHover(document.WordAt(position), "A card's answer."));
    }

    private sealed class Parent(CodeEditor first) : StatefulComponent
    {
        private CodeEditor _current = first;

        public void Hand(CodeEditor next) => SetState(() => _current = next);

        public override VisualNode Build(ComponentContext context) => _current;
    }

    private static PhotonHost Host(VisualNode root, float width = 600, float height = 400) =>
        new(root, PhotonTheme.Instance, ThemeMode.Light, width, height)
        {
            TextRasterizer = new FixedWidthRasterizer(),
            Density = Density.Compact,
            SmoothScroll = false,
        };

    private static RealizeResult Frame(PhotonHost host) => host.RenderFrame(new DisplayListBuilder());

    private static RealizeResult Settle(PhotonHost host)
    {
        Frame(host);
        return Frame(host);
    }

    /// <summary>Twenty lines, line L being <c>var nameL = L;</c>.</summary>
    private static string Lines(int count = 20) =>
        string.Join("\n", Enumerable.Range(0, count).Select(line => $"var name{line} = {line};"));

    private static CodeEditor Editor(string code, ICodeSignatureProvider? provider = null) =>
        new(code, "csharp")
        {
            Height = SizeValue.Fill,
            Completions = [],
            SignatureProviders = provider is null ? [] : [provider],
        };

    /// <summary>The editor's controller, its waits taken out, so the panel and a card show at once.</summary>
    private static CodeEditorController Controller(CodeEditor editor)
    {
        var controller = editor.Editor;
        controller.SignatureHelp.Delay = TimeSpan.Zero;
        controller.Hover.Delay = TimeSpan.Zero;
        return controller;
    }

    private static Rect OnScreen(RealizeResult frame, CodeEditor editor, CodePosition position)
    {
        var region = frame.CodeRegions.Single();
        var at = editor.Editor.CaretRect(position);
        return at with { X = region.Bounds.X + at.X, Y = region.Bounds.Y + at.Y };
    }

    /// <summary>Puts the caret at the end of <paramref name="line"/> with a press, as a person does.</summary>
    private static void ClickAtEndOf(PhotonHost host, CodeEditor editor, int line)
    {
        var end = new CodePosition(line, editor.Editor.Document.Line(line).Length);
        var at = OnScreen(Frame(host), editor, end);
        host.PressDown(at.X + 1, at.Y + at.Height / 2);
        host.PressUp(at.X + 1, at.Y + at.Height / 2);
        Settle(host);
    }

    private static RealizeResult Type(PhotonHost host, string text)
    {
        foreach (var c in text) host.TextInput(c.ToString());
        return Settle(host);
    }

    private static IEnumerable<LayoutNode> Descendants(LayoutNode node)
    {
        yield return node;
        foreach (var child in node)
            foreach (var descendant in Descendants(child))
                yield return descendant;
    }

    private static List<Text> Texts(RealizeResult frame) =>
        [.. Descendants(frame.Root).Select(node => node.Source).OfType<Text>()];

    // ---- where it stands --------------------------------------------------------------------------

    [Fact]
    public void TypingACall_ShowsThePanelOverItsLine_AtTheCaret()
    {
        var editor = Editor(Lines(), CallSignatures.ColumnOnly());
        Controller(editor);
        var host = Host(editor);
        Settle(host);
        ClickAtEndOf(host, editor, 10);

        var frame = Type(host, " Column(");

        editor.Editor.SignatureHelp.IsOpen.Should().BeTrue();
        var note = frame.CodeRegions.Single().Notes.Should().ContainSingle("the panel is the surface's note").Subject;
        var caret = OnScreen(frame, editor, editor.Editor.Caret);
        note.Bottom.Should().BeApproximately(caret.Y, 0.5f, "the panel ends at the top of the caret's line");
        note.X.Should().BeApproximately(caret.X, 0.5f, "and starts at the caret");
    }

    [Fact]
    public void OnTheFirstLine_ThePanelStandsUnderIt()
    {
        var editor = Editor(Lines(), CallSignatures.ColumnOnly());
        Controller(editor);
        var host = Host(editor);
        Settle(host);
        ClickAtEndOf(host, editor, 0);

        var frame = Type(host, " Column(");

        var note = frame.CodeRegions.Single().Notes!.Single();
        var caret = OnScreen(frame, editor, editor.Editor.Caret);
        note.Y.Should().BeApproximately(caret.Y + caret.Height, 0.5f, "with no room above, under the line");
    }

    // ---- what it says -----------------------------------------------------------------------------

    [Fact]
    public void ThePanel_MarksTheArgumentTheCaretIsOn_AndSaysWhatItIsFor()
    {
        var editor = Editor(Lines(), CallSignatures.ColumnOnly());
        Controller(editor);
        var host = Host(editor);
        Settle(host);
        ClickAtEndOf(host, editor, 10);
        Type(host, " Column(");

        var frame = Type(host, "1, ");

        var label = Texts(frame).Single(text => text.Content == CallSignatures.Column.Label);
        var accent = PhotonTheme.Instance.Colors(Variant.Primary).Base;
        label.Spans!.Single(run => run.Color == accent).Content.Should().Be("MainAlign main",
            "the second argument is the one the caret is on");
        Texts(frame).Select(text => text.Content).Should().Contain(["MainAlign main", "Lays its children out in a column."]);
        host.Semantics().Should().Contain(node => node.Live == LiveRegionUrgency.Polite,
            "the panel is read out where the person is, the parameter's label in its text");
    }

    // ---- beside the list and the card -------------------------------------------------------------

    [Fact]
    public void TheListUnderTheLine_PutsThePanelOverIt_AndBothShow()
    {
        var editor = Editor(Lines(), CallSignatures.ColumnOnly());
        editor.Completions = [new ListCompletionProvider(new CodeCompletionItem("name10"), new CodeCompletionItem("named"))];
        Controller(editor);
        var host = Host(editor);
        Settle(host);
        ClickAtEndOf(host, editor, 10);
        Type(host, " Column(");

        var frame = Type(host, "na");

        editor.Editor.Completion.IsOpen.Should().BeTrue("a word typed opens the list");
        var region = frame.CodeRegions.Single();
        var caret = OnScreen(frame, editor, editor.Editor.Caret);
        region.Offered!.Value.Y.Should().BeGreaterThanOrEqualTo(caret.Y + caret.Height - 0.5f, "the list stands under the line");
        region.Notes.Should().ContainSingle().Which.Bottom.Should().BeApproximately(caret.Y, 0.5f,
            "and the panel over it");
    }

    [Fact]
    public void ACardShowing_HidesThePanel()
    {
        var editor = Editor(Lines(), CallSignatures.ColumnOnly());
        editor.HoverProviders = [new Explainer()];
        var controller = Controller(editor);
        var host = Host(editor);
        Settle(host);
        ClickAtEndOf(host, editor, 10);
        Type(host, " Column(");

        controller.Hover.ShowAt(new CodePosition(10, 4)).Should().BeTrue();
        var frame = Settle(host);

        var shown = Texts(frame).Select(text => text.Content).ToList();
        shown.Should().Contain("A card's answer.").And.NotContain(CallSignatures.Column.Label,
            "the card is what the person asked for last");
        controller.SignatureHelp.IsOpen.Should().BeTrue("the panel is hidden, not closed");
    }

    [Fact]
    public void SignatureProvidersHandedByAParent_AreAdopted()
    {
        var first = Editor(Lines());
        var parent = new Parent(first);
        var host = Host(parent);
        Settle(host);

        parent.Hand(Editor(Lines(), CallSignatures.ColumnOnly()));
        Settle(host);
        first.Editor.SignatureHelp.Delay = TimeSpan.Zero;
        ClickAtEndOf(host, first, 10);
        Type(host, " Column(");

        first.Editor.SignatureHelp.IsOpen.Should().BeTrue("the provider of the parent's next build reached the editor it keeps");
    }

    // ---- pixels -----------------------------------------------------------------------------------

    /// <summary>
    /// The panel over a call, light and dark, on the reference backend: its frame and shadow, the label with
    /// the argument the caret is on in the accent colour, the overload count, the parameter's documentation.
    /// The state the picture is of is asserted beside it.
    /// </summary>
    [Theory]
    [InlineData(ThemeMode.Light, "code-signature-light")]
    [InlineData(ThemeMode.Dark, "code-signature-dark")]
    public void ThePanelOverACall_RendersThePhotonPixels(ThemeMode mode, string golden)
    {
        var editor = new CodeEditor(Lines(8), "csharp")
        {
            Height = SizeValue.Fill,
            Completions = [],
            SignatureProviders = [CallSignatures.Overloaded()],
        };
        Controller(editor);
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(360, 240);
        var host = new PhotonHost(editor, PhotonTheme.Instance, mode, 360, 240) { Density = Density.Compact };
        Settle(host);
        ClickAtEndOf(host, editor, 4);
        foreach (var c in " Add(1, ") host.TextInput(c.ToString());
        Settle(host);
        host.KeyDown("ArrowDown");
        Settle(host);
        host.RenderFrame(new DisplayListBuilder(), timeMs: 500);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs: 1000);
        backend.Render(builder.Build(), surface);

        editor.Editor.SignatureHelp.Signature!.Label.Should().Be("Add(int value, int times)", "↓ chose the second overload");
        editor.Editor.SignatureHelp.ActiveParameter.Should().Be(1, "the caret is on the second argument");
        var texts = Descendants(host.LastFrame!.Root).Select(node => node.Source).OfType<Text>().ToList();
        texts.Single(text => text.Content == "Add(int value, int times)").Spans!
            .Single(run => run.Color == PhotonTheme.Instance.Colors(Variant.Primary).Base).Content.Should().Be("int times");
        texts.Select(text => text.Content).Should().Contain(["2/3", "int times: How many times to add it.",
            "Adds a value as many times as it is told."]);
        GoldenImage.Match(surface, golden);
    }
}
