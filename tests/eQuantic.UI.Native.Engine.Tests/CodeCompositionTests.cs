using eQuantic.UI.Code;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// An INPUT METHOD building text, in the model both hosts drive. The composition lives in the
/// document while it grows (the line reflows around it, the highlighter colours it, the caret sits
/// after it), none of its steps reaches the undo history, a commit is ONE edit and a cancellation
/// leaves no trace. Before this the web took text one key at a time and Photon tracked a
/// composition it never showed.
/// </summary>
public class CodeCompositionTests
{
    private static CodeEditorController At(string text, int column)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.Selection = new CodeRange(new CodePosition(0, column));
        return editor;
    }

    [Fact]
    public void TheCompositionIsInTheDocumentWhileItGrows()
    {
        var editor = At("ab", 1);

        editor.SetComposition("k");
        editor.SetComposition("ka");

        editor.Document.Text.Should().Be("akab");
        editor.Composition.Should().Be(new CodeRange(new CodePosition(0, 1), new CodePosition(0, 3)));
        editor.Caret.Should().Be(new CodePosition(0, 3));
    }

    [Fact]
    public void ACommitIsOneEdit_WhichOneUndoTakesBack()
    {
        var editor = At("ab", 1);

        editor.SetComposition("k");
        editor.SetComposition("ka");
        editor.HandleText("か");

        editor.Document.Text.Should().Be("aかb");
        editor.Composition.Should().BeNull();
        editor.Undo();
        editor.Document.Text.Should().Be("ab");
    }

    /// <summary>
    /// ONE edit means one step of its own, not the tail of the run it began at: typing `a` and then
    /// committing `か` right after it had the commit coalesce with the `a`, so one undo took both,
    /// and the typing after a commit joined it the same way. Found in review.
    /// </summary>
    [Fact]
    public void ACommitIsAnUndoStepOfItsOwn_JoinedToNeitherTheTypingBeforeItNorAfter()
    {
        var editor = At("", 0);

        editor.HandleText("a");
        editor.SetComposition("k");
        editor.HandleText("か");
        editor.HandleText("b");

        editor.Undo();
        editor.Document.Text.Should().Be("aか", "the typing after a commit is a step of its own");
        editor.Undo();
        editor.Document.Text.Should().Be("a", "and the commit is ONE step, not the end of the run before it");
    }

    [Fact]
    public void ACancellationLeavesNoTrace()
    {
        var editor = At("ab", 1);

        editor.SetComposition("x");
        editor.SetComposition("");

        editor.Document.Text.Should().Be("ab");
        editor.Composition.Should().BeNull();
        editor.Undo().Should().BeFalse("nothing a composition does is recorded");
    }

    [Fact]
    public void ComposingOverASelectionReplacesIt_AndACancellationPutsItBack()
    {
        var editor = At("one two", 0);
        editor.Selection = new CodeRange(new CodePosition(0, 4), new CodePosition(0, 7));

        editor.SetComposition("x");
        editor.Document.Text.Should().Be("one x");

        editor.SetComposition("");
        editor.Document.Text.Should().Be("one two");
        editor.Document.TextIn(editor.Selection).Should().Be("two");
    }

    [Fact]
    public void LosingTheKeyboardCancelsTheComposition()
    {
        var editor = At("ab", 1);
        editor.SetComposition("x");

        // The platform has already dropped it; underlined text claiming an input method nobody is
        // using would be a lie in the document.
        editor.FocusChanged(false);

        editor.Document.Text.Should().Be("ab");
        editor.Composition.Should().BeNull();
    }

    [Fact]
    public void AReadOnlyEditorComposesNothing()
    {
        var editor = At("ab", 1);
        editor.ReadOnly = true;

        editor.SetComposition("x").Should().BeFalse();
        editor.Document.Text.Should().Be("ab");
    }
}
