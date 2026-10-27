using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What the editor draws of its folding (#1119): a chevron beside every header line, a button named by its lines
/// and saying whether they show, the <c>⋯</c> after a folded header, and the rows the hidden lines no longer take.
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeFoldingViewTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeFoldingViewTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    /// <summary>A region from the class's <c>{</c> on line 1 to line 5, and one from the method's on line 3 to line 4.</summary>
    private const string Source = "class A\n{\n    void B()\n    {\n        x();\n    }\n}";

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

    private static Pressable Button(VisualNode drawn, string label) =>
        All<Pressable>(drawn).Single(press => press.Label == label);

    [Fact]
    public void EveryHeader_HasAChevron_NamedByItsLines_AndOpen()
    {
        var context = new ComponentContext(PhotonTheme.Instance);
        var editor = new CodeEditor(Source, "csharp");

        var drawn = editor.Build(context);

        Button(drawn, SdkStrings.FoldLines(2, 6)).Expanded.Should().BeTrue();
        Button(drawn, SdkStrings.FoldLines(4, 5)).Expanded.Should().BeTrue();
        Button(drawn, SdkStrings.FoldLines(2, 6)).CanRequestFocus.Should().BeFalse("a press leaves the keyboard with the code");
        All<Icon>(drawn).Count(icon => icon.Glyph == (IconGlyph)Icons.ChevronDown).Should().Be(2);
        All<CodeBlock>(drawn).Single().Rows.Should().BeNull("nothing is folded");
    }

    [Fact]
    public void APressOnAChevron_FoldsItsRegion_AndTheHeaderShowsTheMarker()
    {
        var context = new ComponentContext(PhotonTheme.Instance);
        var editor = new CodeEditor(Source, "csharp");

        Button(editor.Build(context), SdkStrings.FoldLines(2, 6)).OnPressed!();
        var drawn = editor.Build(context);

        var chevron = Button(drawn, SdkStrings.UnfoldLines(2, 6));
        chevron.Expanded.Should().BeFalse();
        All<Icon>(chevron).Single().Glyph.Should().Be((IconGlyph)Icons.ChevronRight);
        var block = All<CodeBlock>(drawn).Single();
        block.Rows!.RowCount.Should().Be(3, "lines 2 to 5 take no row");
        All<Text>(block.Build(context)).Select(text => text.Content).Should().Contain("⋯");
        All<Pressable>(drawn).Should().NotContain(press => press.Label == SdkStrings.FoldLines(4, 5),
            "a region inside a folded one shows no chevron");
    }

    [Fact]
    public void APressOnTheMarker_UnfoldsTheRegion()
    {
        var context = new ComponentContext(PhotonTheme.Instance);
        var editor = new CodeEditor(Source, "csharp");
        editor.Editor.Folding.Toggle(3);
        var lines = All<CodeBlock>(editor.Build(context)).Single().Build(context);

        var marker = All<Pressable>(lines).Single(press => press.Label == SdkStrings.UnfoldLines(4, 5));
        All<Text>(marker).Select(text => text.Content).Should().Equal("⋯");
        marker.Expanded.Should().BeFalse();
        marker.CanRequestFocus.Should().BeFalse("a press leaves the keyboard with the code");
        marker.OnPressed!();

        editor.Editor.Folding.IsFolded(3).Should().BeFalse();
        All<Text>(All<CodeBlock>(editor.Build(context)).Single().Build(context))
            .Should().NotContain(text => text.Content == "⋯");
    }

    [Fact]
    public void ACodeWithNoRegion_DrawsNoChevron()
    {
        var context = new ComponentContext(PhotonTheme.Instance);
        var flat = new CodeEditor("a\nb\nc", "csharp");

        var drawn = flat.Build(context);

        All<Pressable>(drawn).Should().NotContain(press => press.Expanded != null);
        All<Icon>(drawn).Should().BeEmpty();
    }
}
