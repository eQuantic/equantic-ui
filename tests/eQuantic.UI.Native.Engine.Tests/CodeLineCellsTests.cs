using eQuantic.UI.Code;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A column is not a cell (defect 12 of docs/CODE-EDITOR-PLAN.md): the model counted one column per
/// UTF-16 unit and placed every caret one cell per column, while the browser drew a tab to the next
/// eight-column stop and a wide character across two cells, so the caret stood beside the wrong
/// glyph; and it stepped one unit at a time, so a Backspace on an emoji left half of it.
/// </summary>
public class CodeLineCellsTests
{
    [Fact]
    public void PlainTextIsOneCellPerColumn()
    {
        var line = new CodeLineCells("var x", 4);

        line.Width.Should().Be(5);
        line.CellOf(3).Should().Be(3);
        line.ColumnAt(3).Should().Be(3);
    }

    [Fact]
    public void ATabRunsToTheNextStop()
    {
        var line = new CodeLineCells("a\tb\t\tc", 4);

        line.CellOf(1).Should().Be(1, "the tab begins where the a ends");
        line.CellOf(2).Should().Be(4, "and runs to the stop at 4");
        line.CellOf(4).Should().Be(8, "a tab that begins ON a stop runs a whole width");
        line.CellOf(5).Should().Be(12);
        line.Width.Should().Be(13);
    }

    [Fact]
    public void AWideCharacterTakesTwoCells_AndAnEmojiToo()
    {
        var line = new CodeLineCells("a\u4E2Db\U0001F600c", 4);

        line.CellOf(1).Should().Be(1);
        line.CellOf(2).Should().Be(3, "the ideograph is two cells");
        line.CellOf(3).Should().Be(4);
        line.CellOf(5).Should().Be(6, "the emoji is two cells and two UTF-16 units");
        line.Width.Should().Be(7);
    }

    [Fact]
    public void AnElementIsNeverSplit_NotByAMarkNotByASurrogatePairNotByAJoiner()
    {
        var line = new CodeLineCells("e\u0301x\U0001F468\u200D\U0001F469\u200D\U0001F467y", 4);

        line.Next(0).Should().Be(2, "the accent belongs to its e");
        line.Previous(2).Should().Be(0);
        line.Next(3).Should().Be(11, "a family emoji is one step");
        line.Previous(11).Should().Be(3);
        line.CellOf(1).Should().Be(0, "a column inside an element counts as its start");
        line.Count.Should().Be(4);
    }

    [Fact]
    public void AFlagIsOneElement_TwoCellsWide()
    {
        var line = new CodeLineCells("\U0001F1E7\U0001F1F7!", 4);

        line.Next(0).Should().Be(4);
        line.CellOf(4).Should().Be(2);
    }

    [Fact]
    public void AnEmojiPresentationSelectorMakesAnElementWide()
    {
        var line = new CodeLineCells("\u2764\uFE0F!", 4);

        line.CellOf(2).Should().Be(2);
    }

    [Fact]
    public void AClickLandsOnTheNearerSideOfWhatItHit()
    {
        var line = new CodeLineCells("a\tb\u4E2D", 4);

        line.ColumnAt(1.4f).Should().Be(1, "the left half of the tab is before it");
        line.ColumnAt(2.6f).Should().Be(2, "the right half is after it");
        line.ColumnAt(5.2f).Should().Be(3, "the left half of the ideograph");
        line.ColumnAt(6.1f).Should().Be(4, "its right half");
        line.ColumnAt(40).Should().Be(4, "past the end is the end");
    }

    [Fact]
    public void CellOfAndColumnAtAreInverses_OnEveryBoundary()
    {
        var line = new CodeLineCells("\tif (\u4E2D\U0001F600 && e\u0301) {", 4);

        for (var i = 0; i < line.Count; i++)
        {
            var element = line.ElementAt(i);
            line.ColumnAt(line.CellOf(element.Start)).Should().Be(element.Start);
        }
        line.ColumnAt(line.Width).Should().Be(line.Text.Length);
    }

    [Fact]
    public void AnEmptyLineHasNoCellsAndNowhereToGo()
    {
        var line = new CodeLineCells("", 4);

        line.Width.Should().Be(0);
        line.Next(0).Should().Be(0);
        line.Previous(0).Should().Be(0);
        line.ColumnAt(3).Should().Be(0);
    }

    /// <summary>
    /// An element's width is its CLUSTER's: a skin tone or an emoji presentation selector makes it an
    /// emoji, drawn two cells wide, whatever its first character is alone. It was read from the first
    /// character, so ✌🏻 took one cell and drew two, and the rest of the line slid off the grid.
    /// </summary>
    [Theory]
    [InlineData("\u270C\U0001F3FB!", 3)]   // ✌🏻 then !
    [InlineData("\u270C!", 2)]              // ✌ alone is text, one cell
    [InlineData("\u2764\uFE0F!", 3)]        // ❤️ then !
    [InlineData("\U0001F7F0", 2)]           // 🟰, emoji 14
    [InlineData("\u2329x", 3)]              // 〈, East Asian wide
    [InlineData("\U0001B000", 2)]           // 𛀀, kana supplement
    [InlineData("\U0001F321", 1)]           // 🌡 is text by default
    [InlineData("ab\u200Bcd", 4)]           // a zero-width space takes no cell
    [InlineData("\uFEFFx", 1)]              // nor does a byte-order mark
    public void AnElementIsAsWideAsItsCluster(string text, int width)
    {
        new CodeLineCells(text, 4).Width.Should().Be(width);
    }

    /// <summary>.NET makes one element of a lone high surrogate and the mark after it, and reading a
    /// code point from the two threw. Malformed text reaches an editor through a paste.</summary>
    [Fact]
    public void ALoneSurrogateBeforeAMarkIsOneCell_NotAnException()
    {
        new CodeLineCells("\uD83D\u0301x", 4).Width.Should().Be(2);
    }

    /// <summary>
    /// A text element that BEGINS with a mark (at the start of a line, or after a tab) has no advance
    /// of its own when the mark is nonspacing or enclosing: it combines with whatever is drawn before
    /// it. A spacing mark has one, by definition. The voiced sound mark is East Asian Wide and took
    /// two cells.
    /// </summary>
    [Theory]
    [InlineData("\u3099x", 1)]      // a voiced sound mark alone, then x
    [InlineData("\t\u0301x", 5)]   // a tab to its stop, an acute alone, then x
    [InlineData("\u20DDx", 1)]      // an enclosing circle alone
    [InlineData("\u0903x", 2)]      // a spacing mark has its cell
    public void AMarkThatBeginsAnElementTakesTheCellsItsKindSays(string text, int width)
    {
        new CodeLineCells(text, 4).Width.Should().Be(width);
    }
}
