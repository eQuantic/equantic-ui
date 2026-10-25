using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What an edit tells the things that follow the text (a problem's squiggle, a language server): the
/// edits every change raises, undo and redo included, and where a range stands after one
/// (<see cref="CodeEdit.Carry"/>). Undo and redo raised no edit, which a change keeps for a document
/// replaced whole, so nothing could carry a range through them (#299).
/// </summary>
public class CodeEditEventTests
{
    private static CodePosition P(int line, int column) => new(line, column);

    private static CodeRange R(int line, int from, int to) => new(P(line, from), P(line, to));

    [Fact]
    public void Undo_RaisesTheEditItMade()
    {
        var editor = new CodeEditorController("");
        editor.HandleText("a");
        editor.HandleText("b");
        var raised = new List<CodeChange>();
        editor.Changed += raised.Add;

        editor.Undo().Should().BeTrue();

        var edit = raised.Should().ContainSingle().Which.Edits.Should().ContainSingle().Which;
        edit.Range.Should().Be(R(0, 0, 2));
        edit.RemovedText.Should().Be("ab");
        edit.InsertedText.Should().BeEmpty();
    }

    [Fact]
    public void Redo_RaisesTheEditItMade()
    {
        var editor = new CodeEditorController("");
        editor.HandleText("a");
        editor.HandleText("b");
        editor.Undo();
        var raised = new List<CodeChange>();
        editor.Changed += raised.Add;

        editor.Redo().Should().BeTrue();

        var edit = raised.Should().ContainSingle().Which.Edits.Should().ContainSingle().Which;
        edit.Range.Should().Be(new CodeRange(P(0, 0)));
        edit.RemovedText.Should().BeEmpty();
        edit.InsertedText.Should().Be("ab");
        edit.InsertedRange.Should().Be(R(0, 0, 2));
    }

    [Fact]
    public void ADocumentReplacedWhole_IsAChangeOfNoEdits()
    {
        var editor = new CodeEditorController("one");
        var raised = new List<CodeChange>();
        editor.Changed += raised.Add;

        editor.Document = CodeDocument.FromText("two");

        var change = raised.Should().ContainSingle().Which;
        change.Replaced.Should().BeTrue();
        change.Edits.Should().BeEmpty();
    }

    // ---- carrying a range -------------------------------------------------------------------------

    private static CodeEdit Edit(CodeRange range, string removed, string inserted) =>
        new(range, removed, inserted, new CodeRange(range.Start), new CodeRange(range.Start), false);

    [Fact]
    public void ARangeAfterAnEditOnALaterLine_KeepsItsColumns()
    {
        var edit = Edit(new CodeRange(P(0, 0)), "", "one\ntwo\n");

        edit.Carry(R(3, 4, 6)).Should().Be(R(5, 4, 6));
    }

    [Fact]
    public void ARangeAfterAnEditOnItsLine_MovesWithTheEditsEnd()
    {
        var edit = Edit(R(0, 2, 5), "abc", "x\nyz");

        edit.Carry(R(0, 7, 9)).Should().Be(R(1, 4, 6));
    }

    [Fact]
    public void ARangeBeforeTheEdit_Stays()
    {
        var edit = Edit(R(2, 0, 3), "abc", "");

        edit.Carry(R(1, 1, 4)).Should().Be(R(1, 1, 4));
    }

    [Fact]
    public void AnEditOverTheRangesStart_StartsItWhereTheReplacementStarts()
    {
        var edit = Edit(R(0, 5, 10), "fives", "");

        edit.Carry(R(0, 8, 12)).Should().Be(R(0, 5, 7));
    }

    [Fact]
    public void AnEmptyRangeWhereTextIsInserted_StaysWithTheTextAfterIt()
    {
        var edit = Edit(new CodeRange(P(0, 3)), "", "abc");

        edit.Carry(new CodeRange(P(0, 3))).Should().Be(new CodeRange(P(0, 6)));
    }
}
