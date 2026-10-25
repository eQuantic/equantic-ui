using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The go-to field the editor draws (#1083): what it says about the line it reads, the lines it washes, its
/// symbols as the field's own listbox, and on Photon the whole of it driven by keys, the text and Enter, the
/// code taking the keyboard back and its view centred on the line gone to.
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeGoToViewTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeGoToViewTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    private static readonly string Text = string.Join("\n", Enumerable.Range(1, 200).Select(i => $"line {i}"));

    private sealed class Answering(IReadOnlyList<CodeSymbol> symbols) : ICodeSymbolProvider
    {
        public Task<IReadOnlyList<CodeSymbol>> SymbolsAsync(CodeDocument document, CancellationToken cancellation) =>
            Task.FromResult(symbols);
    }

    private static CodeRange Lines(int from, int to) => new(new CodePosition(from, 0), new CodePosition(to, 0));

    private static readonly Answering Symbols = new(
    [
        new CodeSymbol("Ledger", CodeSymbolKind.Class, Lines(0, 150), Lines(0, 0))
        {
            Children =
            [
                new CodeSymbol("AddEntry", CodeSymbolKind.Method, Lines(20, 30), Lines(20, 20)),
                new CodeSymbol("Balance", CodeSymbolKind.Method, Lines(120, 130), Lines(120, 120)),
            ],
        },
    ]);

    private static IEnumerable<VisualNode> Children(VisualNode node) => node switch
    {
        FlexNode flex => flex.Children,
        Stack stack => stack.Children,
        Box box => box.Child is { } child ? [child] : [],
        Anchored anchored => [anchored.Anchor, anchored.Panel],
        CodeSurface surface => [surface.Child],
        SingleChildNode single => [single.Child],
        _ => [],
    };

    private static IEnumerable<T> All<T>(VisualNode node) where T : VisualNode
    {
        if (node is T match) yield return match;
        foreach (var child in Children(node))
            foreach (var found in All<T>(child))
                yield return found;
    }

    // ---- what the field draws ---------------------------------------------------------------------------

    [Fact]
    public void TheField_SaysWhatTheLineReads_AndTheLineIsWashed()
    {
        var context = new ComponentContext(PhotonTheme.Instance);
        var editor = new CodeEditor(Text, "csharp");
        All<TextEntry>(editor.Build(context)).Should().BeEmpty("the field shows only while going to");

        editor.Editor.Execute(CodeCommand.GoToLine);
        var drawn = editor.Build(context);
        var field = All<TextEntry>(drawn).Single();
        field.Label.Should().Be(SdkStrings.GoTo);
        field.Value.Should().Be(":");
        All<Text>(drawn).Select(text => text.Content).Should().Contain(SdkStrings.GoToLinePrompt(200));
        All<LiveRegion>(drawn).Should().ContainSingle("what it says is read as it changes");

        editor.Editor.GoTo.Query = ":40:3";
        drawn = editor.Build(context);
        All<Text>(drawn).Select(text => text.Content).Should().Contain(SdkStrings.GoToPosition(40, 3));
        All<CodeBlock>(drawn).Single().Decorations
            .Should().Contain(mark => mark.Kind == CodeDecorationKind.Line && mark.Range.Start.Line == 39);

        editor.Editor.GoTo.Close();
        All<TextEntry>(editor.Build(context)).Should().BeEmpty();
    }

    [Fact]
    public void TheSymbols_AreTheFieldsListbox()
    {
        var context = new ComponentContext(PhotonTheme.Instance);
        var editor = new CodeEditor(Text, "csharp") { SymbolProviders = [Symbols] };
        editor.Build(context);

        editor.Editor.GoTo.Open(CodeGoToMode.Symbol);
        var drawn = editor.Build(context);

        var anchored = All<Anchored>(drawn).Single();
        anchored.PanelRole.Should().Be(AnchorPanelRole.Listbox);
        anchored.ActiveIndex.Should().Be(0);
        var options = All<Pressable>(anchored.Panel).Where(press => press.Role == PressableRole.Option).ToList();
        options.Select(option => option.Label).Should().Equal("Ledger", "AddEntry, Ledger", "Balance, Ledger");
        options.Select(option => option.Selected).Should().Equal(true, false, false);
        All<Text>(anchored.Panel).Select(text => text.Content).Should().Contain(SdkStrings.SymbolsCount(3));
    }

    /// <summary>The field closes as it loses the keyboard, where the view stands, as VS Code's quick input
    /// hides; its list hangs with no scrim to swallow a press elsewhere.</summary>
    [Fact]
    public void TheField_ClosesAsItLosesTheKeyboard_AndHangsNoScrim()
    {
        var context = new ComponentContext(PhotonTheme.Instance);
        var editor = new CodeEditor(Text, "csharp");
        editor.Editor.GoTo.Open(CodeGoToMode.Line);
        var drawn = editor.Build(context);
        All<Anchored>(drawn).Single().OnDismiss.Should().BeNull();

        All<TextEntry>(drawn).Single().OnFocusChanged!(false);

        editor.Editor.GoTo.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void AnEditorWithNoSymbolProvider_SaysSo()
    {
        var context = new ComponentContext(PhotonTheme.Instance);
        var editor = new CodeEditor(Text, "csharp");
        editor.Editor.GoTo.Open(CodeGoToMode.Symbol);

        All<Text>(editor.Build(context)).Select(text => text.Content).Should().Contain(SdkStrings.NoSymbolProvider);
    }

    // ---- on Photon, by keys -----------------------------------------------------------------------------

    [Fact]
    public void OnPhoton_TheKeysTheTextAndEnter_GoToASymbol_AndTheCodeTakesTheKeyboardBack()
    {
        var editor = new CodeEditor(Text, "csharp")
        {
            Height = SizeValue.Fill,
            Autofocus = true,
            SymbolProviders = [Symbols],
        };
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, 640, 300) { SmoothScroll = false };
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder());

        host.KeyDown("o", KeyModifiers.Shift | KeyModifiers.Command).Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder());
        editor.Editor.GoTo.IsOpen.Should().BeTrue();

        host.TextInput("bal").Should().BeTrue("the field has the keyboard");
        host.RenderFrame(new DisplayListBuilder());
        editor.Editor.GoTo.Query.Should().Be("@bal");
        editor.Editor.GoTo.Entries.Select(entry => entry.Name).Should().Equal("Balance");

        host.KeyDown("Enter").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder());

        editor.Editor.GoTo.IsOpen.Should().BeFalse();
        editor.Editor.Selection.Should().Be(new CodeRange(new CodePosition(120, 0)));
        var scrolled = editor.Editor.VerticalScroll!.Offset;
        scrolled.Should().BeGreaterThan(0, "line 121 is centred in a view over the top of the document");
        host.TextInput("x").Should().BeTrue("the code has the keyboard back");
        editor.Editor.Document.Line(120).Should().Be("xline 121");
    }

    [Fact]
    public void OnPhoton_TheArrowsWalkTheList_AndEscapeGivesTheViewBack()
    {
        var editor = new CodeEditor(Text, "csharp")
        {
            Height = SizeValue.Fill,
            Autofocus = true,
            SymbolProviders = [Symbols],
        };
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, 640, 300) { SmoothScroll = false };
        host.RenderFrame(new DisplayListBuilder());
        host.RenderFrame(new DisplayListBuilder());
        var view = editor.Editor.View;
        view.Height.Should().BeGreaterThan(0, "the editor reports the view it draws its code in");

        host.KeyDown("o", KeyModifiers.Shift | KeyModifiers.Command);
        host.RenderFrame(new DisplayListBuilder());
        host.KeyDown("ArrowDown").Should().BeTrue();
        host.KeyDown("ArrowDown").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder());
        editor.Editor.GoTo.Selected.Should().Be(2);
        editor.Editor.GoTo.Preview.Should().Be(Lines(120, 130));

        host.KeyDown("Escape").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder());

        editor.Editor.GoTo.IsOpen.Should().BeFalse();
        editor.Editor.VerticalScroll!.Offset.Should().Be(view.Y, "the view stands where it stood when the field opened");
        editor.Editor.Selection.Should().Be(new CodeRange(new CodePosition(0, 0)));
    }
}
