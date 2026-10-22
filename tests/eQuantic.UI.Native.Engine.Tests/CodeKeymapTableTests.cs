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

    /// <summary>
    /// An app's chord of two keys starts on a key the defaults bind on their own: its bindings come before
    /// every default, of either kind. The defaults' ⌘A selected all, and the app's ⌘A ⌘D never started
    /// (Copilot's first round on #1027).
    /// </summary>
    [Fact]
    public void AnAppsChordOfTwoKeys_StartsOnAKeyTheDefaultsBindAlone()
    {
        var editor = Editor();
        var before = editor.Selection;
        editor.KeyBindings.Add(new CodeKeyBinding(KeyChord.Command("d"), CodeCommand.CursorDocumentEnd) { First = KeyChord.Command("a") });

        Press(editor, "a", KeyModifiers.Command).Should().BeTrue("the app's first half waits");
        editor.Selection.Should().Be(before, "and the defaults' select all did not run");
        Press(editor, "d", KeyModifiers.Command).Should().BeTrue();

        editor.Caret.Should().Be(editor.Document.End);
    }

    /// <summary>A command that does nothing answers false, so the next command its chord names runs: the
    /// card with nothing to explain claimed the key, and the select all after it never ran.</summary>
    [Fact]
    public void ACardWithNothingToExplain_LetsTheChordsNextCommandRun()
    {
        var editor = Editor();
        editor.KeyBindings.Add(new CodeKeyBinding(KeyChord.Command("j"), CodeCommand.HoverShow));
        editor.KeyBindings.Add(new CodeKeyBinding(KeyChord.Command("j"), CodeCommand.SelectAll));

        editor.CanExecute(CodeCommand.HoverShow).Should().BeFalse("no provider, and no problem at the caret");
        Press(editor, "j", KeyModifiers.Command).Should().BeTrue();

        editor.Hover.IsOpen.Should().BeFalse();
        editor.Selection.Should().Be(Everything(editor));
    }

    /// <summary>A list of one entry has nowhere to walk, so its navigation cannot run: a menu greys it out,
    /// and a chord of two keys ending in it does not wait (Copilot's first round on #1027).</summary>
    [Fact]
    public void AListOfOneEntry_CannotBeWalked()
    {
        var editor = Editor("x\n");
        editor.Selection = new CodeRange(new CodePosition(0, 1));
        editor.Completion.Providers.Add(new ListCompletionProvider(new CodeCompletionItem("xylophone")));
        editor.Completion.Invoke().Should().BeTrue();
        editor.Completion.Items.Should().ContainSingle();
        editor.KeyBindings.Add(new CodeKeyBinding(KeyChord.Command("n"), CodeCommand.CompletionNext) { First = KeyChord.Command("g") });

        foreach (var walk in new[] { CodeCommand.CompletionNext, CodeCommand.CompletionPrevious, CodeCommand.CompletionNextPage, CodeCommand.CompletionPreviousPage })
            editor.CanExecute(walk).Should().BeFalse($"{walk} has nowhere to go");
        editor.CanExecute(CodeCommand.CompletionAccept).Should().BeTrue("the one entry can still be accepted");
        editor.CanExecute(CodeCommand.CompletionHide).Should().BeTrue();

        Press(editor, "g", KeyModifiers.Command).Should().BeFalse("the chord's command could not run, so ⌘G is the host's");
    }

    /// <summary>⌘K waits only while a card could show at the caret: a problem elsewhere explains nothing
    /// here, and ⌘K is the host's (a browser's Ctrl+K), as with no problem at all.</summary>
    [Fact]
    public void CommandK_WaitsOnlyWhileACardCouldShowAtTheCaret()
    {
        var editor = Editor();
        editor.Diagnostics.Report([new CodeDiagnostic(new CodeRange(new CodePosition(2, 0), new CodePosition(2, 7)), "misspelt")]);

        Press(editor, "k", KeyModifiers.Command).Should().BeFalse("the problem is on another line");

        editor.Selection = new CodeRange(new CodePosition(2, 3));
        Press(editor, "k", KeyModifiers.Command).Should().BeTrue("the caret is on the problem");
        Press(editor, "i", KeyModifiers.Command).Should().BeTrue();
        editor.Hover.IsOpen.Should().BeTrue("⌘K ⌘I showed the problem's card");
    }

    /// <summary>The key that completes a chord of two keys is the chord's even when its command found
    /// nothing to do, as VS Code has it: it is never handed on as itself.</summary>
    [Fact]
    public void TheSecondHalf_IsTheChordsEvenWhenItsCommandDidNothing()
    {
        var editor = Editor();
        editor.Diagnostics.Report([new CodeDiagnostic(new CodeRange(new CodePosition(1, 0), new CodePosition(1, 5)), "unused")]);
        Press(editor, "k", KeyModifiers.Command).Should().BeTrue("the caret is on the problem");
        // The problem goes before the second half: a build's report, an answer landing.
        editor.Diagnostics.Report([]);

        Press(editor, "i", KeyModifiers.Command).Should().BeTrue("⌘I completes ⌘K ⌘I");

        editor.Hover.IsOpen.Should().BeFalse("there was nothing left to explain");
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
