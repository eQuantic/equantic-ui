using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The keymap as data (#1010): a command run with no key, an app's own bindings over the defaults, a
/// chord given back to the host, a chord of two keys, and one chord trying its commands in order. Driven
/// as a host drives the editor, through <see cref="CodeEditorController.HandleKey"/>, with no screen.
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeKeymapTableTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeKeymapTableTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    private static CodeEditorController Editor(string text = "alpha beta\ngamma delta\nepsilon")
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.SignatureHelp.Delay = TimeSpan.Zero;
        editor.Selection = new CodeRange(new CodePosition(1, 2));
        return editor;
    }

    private static bool Press(CodeEditorController editor, string key, KeyModifiers modifiers = KeyModifiers.None,
        KeyboardConvention convention = KeyboardConvention.Apple) =>
        editor.HandleKey(key, modifiers, convention, null);

    private static CodeRange Everything(CodeEditorController editor) =>
        new(CodePosition.Start, editor.Document.End);

    // ---- a command with no key ----------------------------------------------------------------------

    [Fact]
    public void ACommandRunFromCode_RunsAsItsKeyWould()
    {
        var editor = Editor();

        editor.Execute(CodeCommand.SelectAll).Should().BeTrue();

        editor.Selection.Should().Be(Everything(editor));
    }

    [Fact]
    public void ACommandWithNothingToDo_AnswersFalse_AndChangesNothing()
    {
        var editor = Editor();
        var before = editor.Selection;

        editor.CanExecute(CodeCommand.CompletionNext).Should().BeFalse("no list shows");
        editor.Execute(CodeCommand.CompletionNext).Should().BeFalse();

        editor.Selection.Should().Be(before);
    }

    // ---- the defaults ---------------------------------------------------------------------------------

    [Fact]
    public void AWordLeft_IsOptionOnAMac_AndControlElsewhere()
    {
        var mac = Editor("alpha beta");
        mac.Selection = new CodeRange(new CodePosition(0, 8));
        var pc = Editor("alpha beta");
        pc.Selection = new CodeRange(new CodePosition(0, 8));

        Press(mac, "ArrowLeft", KeyModifiers.Alt).Should().BeTrue();
        Press(pc, "ArrowLeft", KeyModifiers.Command, KeyboardConvention.Standard).Should().BeTrue();

        mac.Caret.Should().Be(new CodePosition(0, 6));
        pc.Caret.Should().Be(new CodePosition(0, 6));
    }

    /// <summary>A combination no convention gives the arrows is the host's: the hand-written keymap read
    /// the modifiers loosely and took ⌃⌥⇧← on a Mac as a step left.</summary>
    [Fact]
    public void AChordNoConventionNames_IsTheHosts()
    {
        var editor = Editor();
        var before = editor.Selection;

        Press(editor, "ArrowLeft", KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift).Should().BeFalse();

        editor.Selection.Should().Be(before);
    }

    [Fact]
    public void TheDefaults_NameNoChordTwiceForOneCommand()
    {
        foreach (var convention in new[] { KeyboardConvention.Apple, KeyboardConvention.Standard })
        {
            var defaults = CodeKeymap.Defaults(convention);
            defaults.Should().NotBeEmpty();
            defaults.Select(binding => $"{binding.First?.Key}|{binding.First?.Modifiers}|{binding.Chord.Key}|{binding.Chord.Modifiers}|{binding.Command}")
                .Should().OnlyHaveUniqueItems($"the {convention} table names each chord's command once");
        }
    }

    // ---- one chord, several commands ------------------------------------------------------------------

    [Fact]
    public void TheDownArrow_TriesTheList_ThenThePanel_ThenTheCaret()
    {
        var editor = Editor("x.\n\n");
        editor.Selection = new CodeRange(new CodePosition(0, 2));
        editor.SignatureHelp.Providers.Add(CallSignatures.Overloaded());
        editor.Completion.Providers.Add(new ListCompletionProvider(new CodeCompletionItem("one"), new CodeCompletionItem("other")));
        foreach (var c in "Add(") editor.HandleText(c.ToString());
        editor.HandleText("o");
        editor.Completion.IsOpen.Should().BeTrue();
        var selected = editor.Completion.Selected;

        Press(editor, "ArrowDown").Should().BeTrue();
        editor.Completion.Selected.Should().NotBe(selected, "the list took the arrow first");
        editor.SignatureHelp.ActiveSignature.Should().Be(0);

        editor.Completion.Dismiss();
        Press(editor, "ArrowDown").Should().BeTrue();
        editor.SignatureHelp.ActiveSignature.Should().Be(1, "with no list, the panel's next overload");

        editor.SignatureHelp.Dismiss();
        Press(editor, "ArrowDown").Should().BeTrue();
        editor.Caret.Line.Should().Be(1, "with neither, the caret");
    }

    // ---- an app's own bindings ------------------------------------------------------------------------

    [Fact]
    public void AKeyOfTheAppsOwn_RunsItsCommand()
    {
        var editor = Editor();
        editor.KeyBindings.Add(new CodeKeyBinding(KeyChord.Command("d"), CodeCommand.SelectAll));

        Press(editor, "d", KeyModifiers.Command).Should().BeTrue();

        editor.Selection.Should().Be(Everything(editor));
    }

    [Fact]
    public void AChordBoundToNothing_IsGivenBackToTheHost()
    {
        var editor = Editor();
        editor.KeyBindings.Add(new CodeKeyBinding(KeyChord.Command("a"), CodeCommand.None));
        var before = editor.Selection;

        Press(editor, "a", KeyModifiers.Command).Should().BeFalse();

        editor.Selection.Should().Be(before);
    }

    /// <summary>An app's binding replaces the defaults for its chord, and only for its chord.</summary>
    [Fact]
    public void ARebinding_ReplacesTheDefaultForThatChordAlone()
    {
        var editor = Editor();
        editor.KeyBindings.Add(new CodeKeyBinding(KeyChord.Command("a"), CodeCommand.CursorDocumentEnd));
        editor.HandleText("!");

        Press(editor, "a", KeyModifiers.Command).Should().BeTrue();
        editor.Caret.Should().Be(editor.Document.End, "⌘A runs the app's command");
        editor.Selection.IsEmpty.Should().BeTrue("and not the default's select all");

        Press(editor, "z", KeyModifiers.Command).Should().BeTrue("⌘Z is still the default's undo");
        editor.Document.Text.Should().NotContain("!");
    }

    [Fact]
    public void AChordOfTwoKeysOfTheAppsOwn_WaitsForItsSecondHalf()
    {
        var editor = Editor();
        editor.KeyBindings.Add(new CodeKeyBinding(KeyChord.Command("u"), CodeCommand.SelectAll) { First = KeyChord.Command("k") });

        Press(editor, "k", KeyModifiers.Command).Should().BeTrue("the first half waits, its command could run");
        Press(editor, "u", KeyModifiers.Command).Should().BeTrue();

        editor.Selection.Should().Be(Everything(editor));
    }

    /// <summary>The first half of a chord of two keys waits only while its second half's command could
    /// run: with no card to show, ⌘K is the host's.</summary>
    [Fact]
    public void TheFirstHalf_WaitsOnlyWhileItsCommandCouldRun()
    {
        var editor = Editor();

        editor.CanExecute(CodeCommand.HoverShow).Should().BeFalse("no provider and no problem");
        Press(editor, "k", KeyModifiers.Command).Should().BeFalse();
    }
}
