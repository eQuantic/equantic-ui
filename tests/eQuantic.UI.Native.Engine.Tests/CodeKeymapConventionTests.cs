using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The keyboard's two traditions, through the ONE keymap both hosts call. The same chord means
/// different things in them (⌘← is a line's start on a Mac, Ctrl+← one word back everywhere else),
/// and a keymap that knew only Apple's had Windows and Linux users jumping to the start of the line
/// every time they meant to step over a word. The host says which tradition its users live in; the
/// model never guesses.
/// </summary>
public class CodeKeymapConventionTests
{
    private static CodeEditorController At(string text, int line, int column)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.Selection = new CodeRange(new CodePosition(line, column));
        return editor;
    }

    private static bool Key(CodeEditorController editor, string key, KeyModifiers modifiers,
        KeyboardConvention convention) => editor.HandleKey(key, modifiers, convention, null);

    [Fact]
    public void CommandArrowIsTheLinesStartOnApple_AndOneWordBackEverywhereElse()
    {
        var apple = At("one two three", 0, 13);
        Key(apple, "ArrowLeft", KeyModifiers.Command, KeyboardConvention.Apple);
        apple.Caret.Column.Should().Be(0);

        var standard = At("one two three", 0, 13);
        Key(standard, "ArrowLeft", KeyModifiers.Command, KeyboardConvention.Standard);
        standard.Caret.Column.Should().Be(8, "Ctrl+← steps back over one word");
    }

    [Fact]
    public void AltArrowStepsAWordOnApple()
    {
        var editor = At("one two three", 0, 13);
        Key(editor, "ArrowLeft", KeyModifiers.Alt, KeyboardConvention.Apple);

        editor.Caret.Column.Should().Be(8);
    }

    [Fact]
    public void HomeAndEndReachTheLinesEnds_AndWithCtrlTheDocuments()
    {
        var editor = At("one\ntwo", 1, 2);

        Key(editor, "Home", KeyModifiers.None, KeyboardConvention.Standard);
        editor.Caret.Should().Be(new CodePosition(1, 0));
        Key(editor, "Home", KeyModifiers.Command, KeyboardConvention.Standard);
        editor.Caret.Should().Be(new CodePosition(0, 0));
        Key(editor, "End", KeyModifiers.Command, KeyboardConvention.Standard);
        editor.Caret.Should().Be(new CodePosition(1, 3));
    }

    [Fact]
    public void CtrlArrowUpIsLeftToTheHost_WhereItScrollsTheView()
    {
        var editor = At("one\ntwo", 1, 1);

        Key(editor, "ArrowUp", KeyModifiers.Command, KeyboardConvention.Standard).Should().BeFalse();
        editor.Caret.Should().Be(new CodePosition(1, 1));
    }

    [Fact]
    public void CommandBackspaceTakesEverythingLeftOfTheCaretOnItsLine_OnApple()
    {
        var editor = At("one two", 0, 5);
        Key(editor, "Backspace", KeyModifiers.Command, KeyboardConvention.Apple);

        editor.Document.Text.Should().Be("wo");
    }

    [Fact]
    public void EscapeReleasesTab_AndAnyOtherKeyTakesItBack()
    {
        var editor = At("one", 0, 0);

        Key(editor, "Escape", KeyModifiers.None, KeyboardConvention.Apple).Should().BeFalse(
            "Escape still means what it means around the editor");
        Key(editor, "Tab", KeyModifiers.None, KeyboardConvention.Apple).Should().BeFalse(
            "the next Tab leaves");
        editor.Document.Text.Should().Be("one");

        Key(editor, "Escape", KeyModifiers.None, KeyboardConvention.Apple);
        Key(editor, "Shift", KeyModifiers.Shift, KeyboardConvention.Apple);
        Key(editor, "Tab", KeyModifiers.Shift, KeyboardConvention.Apple).Should().BeFalse(
            "a modifier on its own is not another key, or Shift+Tab could never leave backwards");

        Key(editor, "ArrowRight", KeyModifiers.None, KeyboardConvention.Apple);
        Key(editor, "Tab", KeyModifiers.None, KeyboardConvention.Apple).Should().BeTrue(
            "any other key sets the trap again");
    }

    [Fact]
    public void TheCopyKeysAreLeftToAHostWhoseClipboardArrivesAsEvents()
    {
        var editor = At("one", 0, 0);

        // With no clipboard handed over (a browser's copy, cut and paste carry the text), claiming
        // the key would cancel the very event that brings it.
        Key(editor, "c", KeyModifiers.Command, KeyboardConvention.Apple).Should().BeFalse();
        Key(editor, "v", KeyModifiers.Command, KeyboardConvention.Apple).Should().BeFalse();
        editor.Document.Text.Should().Be("one");
    }
}
