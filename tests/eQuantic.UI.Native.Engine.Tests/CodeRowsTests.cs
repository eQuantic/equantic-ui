using eQuantic.UI.Code;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// How a document's lines become a view's rows (docs/CODE-EDITOR-PLAN.md, the shape, §9): padding
/// that keeps two sides of a diff level, rows that show another document's lines, runs of lines
/// behind one placeholder row, and runs behind none.
/// </summary>
public class CodeRowsTests
{
    [Fact]
    public void WithNothingSaid_ARowIsALine()
    {
        var rows = new CodeRows(5, [], []);

        rows.RowCount.Should().Be(5);
        for (var line = 0; line < 5; line++)
        {
            rows.RowOf(line).Should().Be(line);
            rows.RowAt(line).Should().Be(new CodeRow(CodeRowKind.Line, line));
        }
    }

    [Fact]
    public void AFillerPushesTheLinesAfterItDown()
    {
        var rows = new CodeRows(5, [new CodeFiller(2, 3)], []);

        rows.RowCount.Should().Be(8);
        rows.RowOf(1).Should().Be(1);
        rows.RowOf(2).Should().Be(5, "three rows of filler stand before line 2");
        rows.RowAt(3).Should().Be(new CodeRow(CodeRowKind.Filler, 2));
        rows.LineAtRow(3).Should().Be(2, "a press on a filler lands on the line it stands before");
    }

    [Fact]
    public void AFillerAfterTheLastLine_LandsAPressOnTheLastLine()
    {
        var rows = new CodeRows(5, [new CodeFiller(5, 2)], []);

        rows.RowCount.Should().Be(7);
        rows.RowAt(6).Should().Be(new CodeRow(CodeRowKind.Filler, 5));
        rows.LineAtRow(6).Should().Be(4);
    }

    [Fact]
    public void AFillerCanShowAnotherDocumentsLines()
    {
        var rows = new CodeRows(3, [new CodeFiller(1, 2, SourceLine: 10)], []);

        rows.RowAt(1).SourceLine.Should().Be(10);
        rows.RowAt(2).SourceLine.Should().Be(11);
    }

    [Fact]
    public void ACollapsedRunIsOnePlaceholderRow()
    {
        var rows = new CodeRows(10, [], [new CodeCollapse(2, 6)]);

        rows.RowCount.Should().Be(6);
        rows.RowAt(2).Should().Be(new CodeRow(CodeRowKind.Placeholder, 2, Count: 5));
        rows.RowOf(4).Should().Be(2, "a hidden line is drawn at its placeholder");
        rows.IsVisible(4).Should().BeFalse();
        rows.RowOf(7).Should().Be(3);
        rows.LineAtRow(2).Should().Be(2, "a press on the placeholder lands on the first line it hides");
    }

    [Fact]
    public void AFoldHidesItsLinesBehindTheHeaderAboveThem()
    {
        var rows = new CodeRows(10, [], [new CodeCollapse(3, 5, Placeholder: false)]);

        rows.RowCount.Should().Be(7);
        rows.RowOf(4).Should().Be(2, "under the header, on line 2");
        rows.RowOf(6).Should().Be(3);
        rows.RowAt(3).Should().Be(new CodeRow(CodeRowKind.Line, 6));
    }

    [Fact]
    public void AFillerAtACollapsedLine_ComesBeforeIt()
    {
        var rows = new CodeRows(6, [new CodeFiller(2, 1)], [new CodeCollapse(2, 3)]);

        rows.RowAt(2).Kind.Should().Be(CodeRowKind.Filler);
        rows.RowAt(3).Kind.Should().Be(CodeRowKind.Placeholder);
        rows.RowOf(4).Should().Be(4);
    }

    /// <summary>A list of lines can be empty, where a document never is: a diff against nothing is
    /// its fillers alone, and must not gain a line nobody wrote.</summary>
    [Fact]
    public void AViewOfNoLines_IsItsFillersAlone()
    {
        var rows = new CodeRows(0, [new CodeFiller(0, 3)], []);

        rows.RowCount.Should().Be(3);
        rows.RowAt(1).Should().Be(new CodeRow(CodeRowKind.Filler, 0));
        rows.LineAtRow(1).Should().Be(0);
        new CodeRows(0, [], []).RowCount.Should().Be(0);
    }

    [Fact]
    public void AViewThatCannotBeDrawn_SaysSo()
    {
        var overlapping = () => new CodeRows(10, [], [new CodeCollapse(2, 6), new CodeCollapse(5, 8)]);
        var insideACollapse = () => new CodeRows(10, [new CodeFiller(4, 1)], [new CodeCollapse(2, 6)]);

        overlapping.Should().Throw<ArgumentException>();
        insideACollapse.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Random maps over random documents: every row maps back to itself through its line, every
    /// visible line through its row, and the rows are exactly the visible lines, the fillers and
    /// the placeholders.
    /// </summary>
    [Fact]
    public void EveryRowAndEveryLineMapBack_OnRandomViews()
    {
        var random = new Random(2026_09_24);
        for (var round = 0; round < 300; round++)
        {
            var lineCount = random.Next(1, 60);
            var fillers = new List<CodeFiller>();
            var collapses = new List<CodeCollapse>();
            var line = 0;
            while (line <= lineCount)
            {
                var roll = random.Next(6);
                if (roll == 0) fillers.Add(new CodeFiller(line, random.Next(1, 4)));
                if (roll == 1 && line < lineCount)
                {
                    var last = Math.Min(lineCount - 1, line + random.Next(0, 5));
                    collapses.Add(new CodeCollapse(line, last, random.Next(2) == 0));
                    line = last + 1;
                    continue;
                }
                line++;
            }

            var rows = new CodeRows(lineCount, fillers, collapses);

            var hidden = collapses.Sum(c => c.LastLine - c.FirstLine + 1);
            rows.RowCount.Should().Be(lineCount - hidden + fillers.Sum(f => f.Rows) + collapses.Count(c => c.Placeholder),
                $"round {round}");
            for (var row = 0; row < rows.RowCount; row++)
            {
                var shown = rows.RowAt(row);
                if (shown.Kind == CodeRowKind.Line) rows.RowOf(shown.Line).Should().Be(row, $"round {round}, row {row}");
            }
            for (var at = 0; at < lineCount; at++)
            {
                if (!rows.IsVisible(at)) continue;
                rows.RowAt(rows.RowOf(at)).Should().Be(new CodeRow(CodeRowKind.Line, at), $"round {round}, line {at}");
            }
        }
    }
}
