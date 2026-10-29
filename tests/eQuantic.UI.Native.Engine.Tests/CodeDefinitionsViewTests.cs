using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What the editor draws for definitions and references (#1084): the list of places as the go-to field's
/// listbox, the card that says nothing was found, the link ⌘ underlines, and on Photon the whole of it driven as
/// a person does, the hand over a link with ⌘ pressed and the pointer at rest, a click to the definition, ⇧F12
/// and F4 through the list shown and closed.
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeDefinitionsViewTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeDefinitionsViewTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    /// <summary>Forty lines: <c>total</c> declared on line 2 (counted from 0) and used on lines 5, 8 and 11.</summary>
    private static readonly string Source = BuildSource();

    private static string BuildSource()
    {
        var lines = Enumerable.Range(0, 40).Select(i => $"    // line {i}").ToArray();
        lines[0] = "class Ledger";
        lines[2] = "    int total;";
        lines[5] = "        total += amount;";
        lines[8] = "        total += amount;";
        lines[11] = "        return total;";
        return string.Join("\n", lines);
    }

    private static CodePosition At(int line, int column) => new(line, column);

    private static string WordAt(CodeDocument document, CodePosition at)
    {
        var line = document.Line(at.Line);
        var column = at.Column < line.Length && CodeDocument.IsWordChar(line[at.Column]) ? at.Column
            : at.Column > 0 && CodeDocument.IsWordChar(line[at.Column - 1]) ? at.Column - 1 : -1;
        return column < 0 ? "" : document.TextIn(document.WordAt(At(at.Line, column)));
    }

    private sealed class Defining : ICodeDefinitionProvider
    {
        public Task<IReadOnlyList<CodeLocation>> DefinitionsAsync(CodeDocument document, CodePosition position,
            CodeDefinitionKind kind, CancellationToken cancellation) =>
            Task.FromResult<IReadOnlyList<CodeLocation>>(WordAt(document, position) == "total"
                ? [new CodeLocation(new CodeRange(At(2, 8), At(2, 13)))]
                : []);
    }

    private sealed class Referring : ICodeReferenceProvider
    {
        public Task<IReadOnlyList<CodeLocation>> ReferencesAsync(CodeDocument document, CodePosition position,
            CancellationToken cancellation)
        {
            var name = WordAt(document, position);
            var found = new List<CodeLocation>();
            for (var line = 0; line < document.LineCount && name.Length > 0; line++)
            {
                var at = document.Line(line).IndexOf(name, StringComparison.Ordinal);
                if (at >= 0) found.Add(new CodeLocation(new CodeRange(At(line, at), At(line, at + name.Length))));
            }
            return Task.FromResult<IReadOnlyList<CodeLocation>>(found);
        }
    }

    private static CodeEditor Editor() => new(Source, "csharp")
    {
        Height = SizeValue.Fill,
        DefinitionProviders = [new Defining()],
        ReferenceProviders = [new Referring()],
    };

    private static IEnumerable<VisualNode> Children(VisualNode node) => node switch
    {
        FlexNode flex => flex.Children,
        Stack stack => stack.Children,
        Box box => box.Child is { } child ? [child] : [],
        Anchored anchored => [anchored.Anchor, anchored.Panel],
        // What a surface draws over its code, a card among it, beside the code it draws.
        CodeSurface surface => [surface.Child, .. surface.Notes.Select(note => note.Child)],
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

    // ---- what the editor draws ----------------------------------------------------------------------------

    [Fact]
    public void TheReferences_AreTheFieldsListbox_ARowItsLineAndWhereItStands()
    {
        var context = new ComponentContext(PhotonTheme.Instance);
        var editor = Editor();
        editor.Build(context);
        editor.Editor.Selection = new CodeRange(At(8, 9));

        editor.Editor.Execute(CodeCommand.FindReferences).Should().BeTrue();
        var drawn = editor.Build(context);

        var field = All<TextEntry>(drawn).Single();
        field.Label.Should().Be(SdkStrings.ReferencesCount(4), "a list of places is named by what it lists");
        field.Placeholder.Should().Be(SdkStrings.FilterPlaces);
        field.Value.Should().BeEmpty("its text only filters it, with no prefix");
        var anchored = All<Anchored>(drawn).Single();
        anchored.PanelRole.Should().Be(AnchorPanelRole.Listbox);
        anchored.ActiveIndex.Should().Be(2, "the use the caret stands on is selected");
        var options = All<Pressable>(anchored.Panel).Where(press => press.Role == PressableRole.Option).ToList();
        options.Select(option => option.Label).Should().Equal(
            "int total;, 3:9", "total += amount;, 6:9", "total += amount;, 9:9", "return total;, 12:16");
        All<Text>(options[0]).Should().HaveCount(2, "a place has no kind, and no letter before its line");
        All<Text>(anchored.Panel).Select(text => text.Content).Should().Contain(SdkStrings.ReferencesCount(4));
        All<CodeBlock>(drawn).Single().Decorations
            .Should().Contain(mark => mark.Kind == CodeDecorationKind.Line && mark.Range.Start.Line == 8,
                "the selected place's line is washed");
    }

    [Fact]
    public void NothingFound_IsSaidInACardAtTheCaret_ThatIsAnnounced()
    {
        var context = new ComponentContext(PhotonTheme.Instance);
        var editor = Editor();
        editor.Build(context);
        editor.Editor.Selection = new CodeRange(At(5, 20));

        editor.Editor.Execute(CodeCommand.GoToDefinition).Should().BeTrue();
        var drawn = editor.Build(context);

        var said = All<LiveRegion>(drawn).Single();
        All<Text>(said).Select(text => text.Content).Should().Equal(SdkStrings.NoDefinitionFound("amount"));
        // The note's frame, round the text itself, as wide as its line to a whole pixel (CodeNoteBuilder): a box as
        // wide as a fractional measure loses a browser's rounding, and the line ends cut by an ellipsis.
        var frame = said.Child.Should().BeOfType<Box>().Subject;
        frame.Child.Should().BeOfType<Text>("a note that says one thing frames the text as it is");
        frame.Style.Width.Kind.Should().Be(SizeKind.Fixed);
        frame.Style.Width.Value.Should().Be(MathF.Ceiling(frame.Style.Width.Value)).And.BeGreaterThan(0);
        editor.Editor.Selection = new CodeRange(At(6, 0));
        All<LiveRegion>(editor.Build(context)).Should().BeEmpty("a caret that moves takes it away");
    }

    [Fact]
    public void TheLink_IsUnderlinedInTheAccentColour()
    {
        // Laid out by a host, so the grid knows its cells and the pointer lands on a character.
        var editor = Editor();
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, 640, 400) { SmoothScroll = false };
        var name = OnScreen(Settle(host), editor, At(5, 10));

        host.PointerMove(name.X, name.Y, modifiers: KeyModifiers.Command);
        editor.Editor.Definitions.Link.Should().Be(new CodeRange(At(5, 8), At(5, 13)));
        var drawn = editor.Build(new ComponentContext(PhotonTheme.Instance));

        All<CodeBlock>(drawn).Single().Decorations.Should().Contain(mark =>
            mark.Kind == CodeDecorationKind.Underline && mark.Range == new CodeRange(At(5, 8), At(5, 13))
            && mark.Color == PhotonTheme.Instance.Colors(Variant.Primary).Base);
    }

    private sealed class Waiting : ICodeDefinitionProvider
    {
        public readonly TaskCompletionSource<IReadOnlyList<CodeLocation>> Answer = new();

        public Task<IReadOnlyList<CodeLocation>> DefinitionsAsync(CodeDocument document, CodePosition position,
            CodeDefinitionKind kind, CancellationToken cancellation) => Answer.Task;
    }

    /// <summary>On the web an answer arrives after the key that asked, and the caret it moves was not the app's
    /// to hear about: the status line of the dashboard's code screen kept the line F12 was pressed on.</summary>
    [Fact]
    public void ADefinitionThatArrivesLater_TellsTheAppWhereTheCaretWent()
    {
        var context = new ComponentContext(PhotonTheme.Instance);
        var waiting = new Waiting();
        var told = new List<CodeRange>();
        var editor = new CodeEditor(Source, "csharp") { DefinitionProviders = [waiting], OnSelectionChanged = told.Add };
        editor.Build(context);
        editor.Editor.Selection = new CodeRange(At(5, 9));
        editor.Editor.Execute(CodeCommand.GoToDefinition);

        waiting.Answer.SetResult([new CodeLocation(new CodeRange(At(2, 8), At(2, 13)))]);

        editor.Editor.Selection.Should().Be(new CodeRange(At(2, 8)));
        told.Should().Equal(new CodeRange(At(2, 8)));
    }

    // ---- on Photon ----------------------------------------------------------------------------------------

    private static RealizeResult Frame(PhotonHost host) => host.RenderFrame(new DisplayListBuilder());

    private static RealizeResult Settle(PhotonHost host)
    {
        Frame(host);
        return Frame(host);
    }

    /// <summary>A point on the left of the character at <paramref name="position"/>, on screen.</summary>
    private static Point OnScreen(RealizeResult frame, CodeEditor editor, CodePosition position)
    {
        var region = frame.CodeRegions.Single();
        var at = editor.Editor.CaretRect(position);
        return new Point(region.Bounds.X + at.X + 2, region.Bounds.Y + at.Y + at.Height / 2);
    }

    [Fact]
    public void OnPhoton_CommandPressedAtRest_IsAHandOverTheName_AndAClickGoesToItsDefinition()
    {
        var editor = Editor();
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, 640, 400) { SmoothScroll = false };
        var frame = Settle(host);
        var name = OnScreen(frame, editor, At(5, 10));

        host.PointerMove(name.X, name.Y);
        Settle(host);
        host.CursorAt(name.X, name.Y).Should().Be(CursorShape.Text);

        host.ModifiersChanged(KeyModifiers.Command);
        Settle(host);
        editor.Editor.Definitions.Link.Should().Be(new CodeRange(At(5, 8), At(5, 13)), "⌘ pressed with the pointer at rest");
        host.CursorAt(name.X, name.Y).Should().Be(CursorShape.Pointer);

        host.PressDown(name.X, name.Y, 1, KeyModifiers.Command);
        host.PressUp(name.X, name.Y);
        Settle(host);
        editor.Editor.Selection.Should().Be(new CodeRange(At(2, 8)), "the click went to the declaration");

        host.PointerMove(name.X, name.Y, modifiers: KeyModifiers.Command);
        host.ModifiersChanged(KeyModifiers.None);
        editor.Editor.Definitions.Link.Should().BeNull("⌘ released takes the link away");
    }

    /// <summary>A drag hovers nothing, and still moves the pointer: ⌘ pressed after the release, with no move, is
    /// heard where the drag ended, and links the name there rather than the one the drag began on.</summary>
    [Fact]
    public void OnPhoton_CommandAfterADrag_LinksTheNameWhereTheDragEnded()
    {
        var editor = Editor();
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, 640, 400) { SmoothScroll = false };
        var frame = Settle(host);
        var from = OnScreen(frame, editor, At(5, 10));
        var to = OnScreen(frame, editor, At(8, 18));

        host.PointerMove(from.X, from.Y);
        host.PressDown(from.X, from.Y);
        host.PointerMove(to.X, to.Y);
        host.PressUp(to.X, to.Y);
        Settle(host);
        host.ModifiersChanged(KeyModifiers.Command);

        editor.Editor.Definitions.Link.Should().BeNull("`amount`, where the drag ended, is defined nowhere: no link, and none on `total`");
        host.ModifiersChanged(KeyModifiers.None);
        var back = OnScreen(Settle(host), editor, At(8, 10));
        host.PointerMove(back.X, back.Y);
        host.ModifiersChanged(KeyModifiers.Command);
        editor.Editor.Definitions.Link.Should().Be(new CodeRange(At(8, 8), At(8, 13)));
    }

    [Fact]
    public void OnPhoton_ShiftF12ListsTheReferences_AndF4WalksThem_ShownAndClosed()
    {
        var editor = Editor();
        editor.Autofocus = true;
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, 640, 400) { SmoothScroll = false };
        Settle(host);
        editor.Editor.Selection = new CodeRange(At(8, 9));

        host.KeyDown("F12", KeyModifiers.Shift).Should().BeTrue();
        Settle(host);
        editor.Editor.GoTo.Mode.Should().Be(CodeGoToMode.References);
        editor.Editor.GoTo.Selected.Should().Be(2);

        host.KeyDown("F4").Should().BeTrue("the field has the keyboard, and F4 walks its rows");
        Frame(host);
        editor.Editor.GoTo.Selected.Should().Be(3);
        host.KeyDown("F4", KeyModifiers.Shift).Should().BeTrue();
        Frame(host);
        editor.Editor.GoTo.Selected.Should().Be(2);

        host.KeyDown("Enter").Should().BeTrue();
        Settle(host);
        editor.Editor.GoTo.IsOpen.Should().BeFalse();
        editor.Editor.Selection.Should().Be(new CodeRange(At(8, 8)));

        host.KeyDown("F4").Should().BeTrue("the code has the keyboard back, and F4 walks the places held");
        Settle(host);
        editor.Editor.Selection.Should().Be(new CodeRange(At(11, 15)));
    }
}
