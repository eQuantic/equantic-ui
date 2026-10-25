using eQuantic.UI.Code;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A grid that MEASURES places text in a proportional face (#983): a caret, a band, a press and the place a
/// vertical move aims at are read through the measured width of the line's text, where a monospaced grid
/// counts cells. The spreadsheet's cell editor, formula bar and name box draw on one; the code editor never
/// does, and its own tests are the proof that it did not move.
/// </summary>
public class CodeMeasuredGridTests
{
    /// <summary>A face in which `i` is 3 points wide, `W` 12 and everything else 7, summed: enough to tell a
    /// caret placed by measure from one placed by a count. Counts its calls.</summary>
    private sealed class StubFace
    {
        public int Calls { get; private set; }

        public float Measure(string text)
        {
            Calls++;
            var width = 0f;
            foreach (var c in text) width += c switch { 'i' => 3, 'W' => 12, _ => 7 };
            return width;
        }
    }

    private static CodeEditorController Editor(string text, StubFace face, float originX = 10, float originY = 4) =>
        new(text)
        {
            Grid = new CodeGrid(new Point(originX, originY), new Size(5, 20), Measure: face.Measure),
        };

    [Fact]
    public void ACaret_StandsWhereTheTextBeforeItEnds()
    {
        var editor = Editor("iiiWi", new StubFace());
        editor.Selection = new CodeRange(new CodePosition(0, 4));

        editor.Carets[0].X.Should().Be(10 + 21, "`iiiW` measures 21 from the line's start");
        editor.Carets[0].Y.Should().Be(4);
        editor.Carets[0].Height.Should().Be(20);
    }

    [Fact]
    public void APress_LandsOnTheNearerSideOfTheElementUnderIt()
    {
        var editor = Editor("iiiWi", new StubFace());

        editor.PositionAt(new Point(10 + 16, 10)).Should().Be(new CodePosition(0, 4),
            "16 is past the middle of `W` (9 to 21), so the caret goes after it");
        editor.PositionAt(new Point(10 + 14, 10)).Should().Be(new CodePosition(0, 3),
            "14 is before its middle, so the caret goes before it");
        editor.PositionAt(new Point(10 + 400, 10)).Should().Be(new CodePosition(0, 5), "past the end is the end");
        editor.PositionAt(new Point(0, 10)).Should().Be(new CodePosition(0, 0), "before the start is the start");
    }

    [Fact]
    public void APress_NeverLandsInsideATextElement()
    {
        // A thumbs-up with a skin tone is four UTF-16 units and one element.
        var editor = Editor("a\U0001F44D\U0001F3FDb", new StubFace());
        var face = new StubFace();
        var whole = face.Measure("a\U0001F44D\U0001F3FD");

        var landed = editor.PositionAt(new Point(10 + whole - 1, 10));

        landed.Column.Should().BeOneOf([1, 5], "a caret stands before the element or after it, never within");
    }

    [Fact]
    public void AVerticalMove_AimsAtTheSameDistance_NotTheSameCount()
    {
        var editor = Editor("WWWW\niiiiiiiiii", new StubFace());
        editor.Selection = new CodeRange(new CodePosition(0, 1));

        editor.Execute(CodeCommand.CursorDown);

        editor.Caret.Should().Be(new CodePosition(1, 4), "four `i` are as wide as one `W`");

        editor.Execute(CodeCommand.CursorUp);
        editor.Caret.Should().Be(new CodePosition(0, 1), "and back up aims at the same distance");
    }

    [Fact]
    public void ABand_RunsFromWhereItsFirstCharacterBeginsToWhereItsLastEnds()
    {
        var editor = Editor("iiWii\nW", new StubFace());
        editor.Selection = new CodeRange(new CodePosition(0, 2), new CodePosition(0, 3));

        var band = editor.SelectionBandsIn(0, 1).Should().ContainSingle().Subject;

        band.X.Should().Be(10 + 6);
        band.Width.Should().Be(12, "the band covers the `W` alone");
        band.Y.Should().Be(4);
    }

    [Fact]
    public void ABandOverALineBreak_TakesOneCellOfTheGridPastTheLinesEnd()
    {
        var editor = Editor("ii\nW", new StubFace());
        editor.Selection = new CodeRange(new CodePosition(0, 1), new CodePosition(1, 1));

        var bands = editor.SelectionBandsIn(0, 1);

        bands.Should().HaveCount(2);
        bands[0].X.Should().Be(10 + 3);
        bands[0].Width.Should().Be(3 + 5, "the second `i`, and the grid's cell for the break");
        bands[1].Width.Should().Be(12);
        bands[1].Y.Should().Be(24);
    }

    [Fact]
    public void WhatAPointerRestsOn_IsTheElementUnderIt()
    {
        var editor = Editor("iiWi", new StubFace());

        editor.CharacterAt(new Point(10 + 7, 10)).Should().Be(new CodePosition(0, 2), "7 is inside the `W`");
        editor.CharacterAt(new Point(10 + 40, 10)).Should().BeNull("past the line's end it rests on nothing");
    }

    [Fact]
    public void ALongLine_IsMeasuredOnlyWhereAsked()
    {
        var face = new StubFace();
        var editor = Editor(new string('i', 2000), face);
        editor.Selection = new CodeRange(new CodePosition(0, 1500));

        _ = editor.Carets;
        _ = editor.PositionAt(new Point(10 + 3000, 10));

        face.Calls.Should().BeLessThan(20, "a caret is one measurement and a press a binary search's, not one per character");
    }

    [Fact]
    public void AnotherMeasurer_MeasuresAgain()
    {
        var editor = Editor("WW", new StubFace());
        editor.Selection = new CodeRange(new CodePosition(0, 1));
        editor.Carets[0].X.Should().Be(10 + 12);

        editor.Grid = editor.Grid with { Measure = text => text.Length * 2f };

        editor.Carets[0].X.Should().Be(10 + 2, "the advances were taken with the measurer before");
    }

    [Fact]
    public void AGridWithNoMeasurer_PlacesByCells_AsItAlwaysDid()
    {
        var editor = new CodeEditorController("a\tb中c") { Grid = new CodeGrid(new Point(10, 0), new Size(8, 18)) };
        editor.Selection = new CodeRange(new CodePosition(0, 4));

        editor.Carets[0].X.Should().Be(10 + 7 * 8, "a, the tab to the stop at 4, b, and the wide character's two cells");
        editor.PositionAt(new Point(10 + 7 * 8 + 3, 5)).Should().Be(new CodePosition(0, 4));
    }
}
