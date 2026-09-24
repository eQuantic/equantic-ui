using eQuantic.UI.Code;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The rows a diff is drawn on: two sides that stay level at every change, unchanged runs folded
/// with context around each change, and an inline view that draws the removed lines where they were.
/// </summary>
public class CodeDiffLayoutTests
{
    private static string[] Lines(int count, Func<int, string> line) => Enumerable.Range(0, count).Select(line).ToArray();

    /// <summary>
    /// On random texts, the two sides take the same rows, and every line that is the same on both
    /// sides is drawn on the same row on both, which is what "level" means: a change between two
    /// level lines takes as many rows on each side, its own lines and the padding together.
    /// </summary>
    [Fact]
    public void SideBySide_EveryUnchangedLineIsLevelWithItsTwin_OnRandomTexts()
    {
        var random = new Random(2026_09_24);
        for (var round = 0; round < 300; round++)
        {
            var original = Lines(random.Next(0, 40), _ => ((char)('a' + random.Next(0, 3))).ToString());
            var modified = Lines(random.Next(0, 40), _ => ((char)('a' + random.Next(0, 3))).ToString());
            var changes = CodeDiffer.CompareLines(original, modified);

            var layout = CodeDiffLayout.SideBySide(changes, original.Length, modified.Length, context: 1);

            layout.Original.RowCount.Should().Be(layout.Modified.RowCount, $"round {round}");
            var (i, j) = (0, 0);
            foreach (var change in changes.Append(new CodeLineChange(original.Length, 0, modified.Length, 0, [])))
            {
                for (; i < change.OriginalStart && j < change.ModifiedStart; i++, j++)
                {
                    if (!layout.Original.IsVisible(i)) continue;
                    layout.Original.RowOf(i).Should().Be(layout.Modified.RowOf(j), $"round {round}, lines {i} and {j}");
                }
                i = change.OriginalStart + change.OriginalCount;
                j = change.ModifiedStart + change.ModifiedCount;
            }
        }
    }

    [Fact]
    public void ALongUnchangedRunIsFolded_WithContextAroundTheChange()
    {
        var original = Lines(100, i => $"line {i}");
        var modified = original.ToArray();
        modified[50] = "changed";
        var changes = CodeDiffer.CompareLines(original, modified);

        var layout = CodeDiffLayout.SideBySide(changes, 100, 100, context: 3);

        // Lines 0 to 46 fold into one row, 47 to 49 are the context, 50 changed, 51 to 53 are the
        // context again, and 54 to 99 fold into another.
        layout.Modified.RowCount.Should().Be(1 + 3 + 1 + 3 + 1);
        layout.Modified.RowAt(0).Should().Be(new CodeRow(CodeRowKind.Placeholder, 0, Count: 47));
        layout.Modified.RowOf(50).Should().Be(4);
        layout.Modified.RowAt(8).Should().Be(new CodeRow(CodeRowKind.Placeholder, 54, Count: 46));
        layout.Original.RowCount.Should().Be(layout.Modified.RowCount);
    }

    [Fact]
    public void AFoldedRowSaysWhatTheViewSays_ForTheCountItHides()
    {
        var original = Lines(100, i => $"line {i}");
        var modified = original.ToArray();
        modified[50] = "changed";
        var changes = CodeDiffer.CompareLines(original, modified);

        var layout = CodeDiffLayout.Inline(changes, 100, 100, context: 3, foldLabel: count => $"{count} hidden");

        layout.Modified.RowAt(0).Label.Should().Be("47 hidden");
        layout.Original.RowAt(0).Label.Should().Be("47 hidden", "both sides fold the same run");
        CodeDiffLayout.SideBySide(changes, 100, 100).Modified.RowAt(0).Label.Should().BeNull(
            "a view that gives no label gets none: the engine writes no text of the interface");
    }

    [Fact]
    public void ARunTheViewOpened_StaysOpen()
    {
        var original = Lines(100, i => $"line {i}");
        var modified = original.ToArray();
        modified[50] = "changed";
        var changes = CodeDiffer.CompareLines(original, modified);

        var layout = CodeDiffLayout.SideBySide(changes, 100, 100, context: 3, expanded: [0]);

        layout.Modified.IsVisible(10).Should().BeTrue("the run from line 0 was opened");
        layout.Modified.IsVisible(80).Should().BeFalse("the run after the change was not");
    }

    [Fact]
    public void AShortRunIsNotFolded()
    {
        var original = Lines(8, i => $"line {i}");
        var modified = original.ToArray();
        modified[4] = "changed";

        var layout = CodeDiffLayout.SideBySide(CodeDiffer.CompareLines(original, modified), 8, 8, context: 3);

        layout.Modified.RowCount.Should().Be(8, "folding two lines into a row saves a row and costs a press");
    }

    [Fact]
    public void Inline_DrawsTheRemovedLinesBeforeTheLinesThatReplacedThem()
    {
        string[] original = ["keep", "old one", "old two", "keep too"];
        string[] modified = ["keep", "new", "keep too"];
        var changes = CodeDiffer.CompareLines(original, modified);

        var layout = CodeDiffLayout.Inline(changes, original.Length, modified.Length);

        layout.Modified.RowCount.Should().Be(5);
        layout.Modified.RowAt(1).Should().Be(new CodeRow(CodeRowKind.Filler, 1, SourceLine: 1));
        layout.Modified.RowAt(2).Should().Be(new CodeRow(CodeRowKind.Filler, 1, SourceLine: 2));
        layout.Modified.RowAt(3).Should().Be(new CodeRow(CodeRowKind.Line, 1), "then the line that replaced them");
    }

    /// <summary>
    /// A patch's view draws each gap as one row on both sides, saying what the patch says in its
    /// place, and keeps the lines after it level.
    /// </summary>
    [Fact]
    public void APatchsGapsAreOneRowOnEachSide_AndTheLinesAfterThemStayLevel()
    {
        var source = CodeDiffSource.FromPatch(CodePatch.Parse("""
            --- a/f.cs
            +++ b/f.cs
            @@ -1,2 +1,3 @@
             keep
            +added
             keep too
            @@ -20,2 +21,2 @@
             far away
            -old
            +new
            """)[0]);

        var layout = CodeDiffLayout.SideBySide(source.Changes, source.OriginalLineCount, source.ModifiedLineCount,
            gaps: source.Gaps);

        layout.Original.RowAt(3).Should().Be(new CodeRow(CodeRowKind.Filler, 2, Label: "@@ -20,2 +21,2 @@"),
            "after the original's padding for the added line, the gap, saying what the patch says there");
        layout.Modified.RowAt(3).Should().Be(new CodeRow(CodeRowKind.Filler, 3, Label: "@@ -20,2 +21,2 @@"));
        layout.Original.RowOf(2).Should().Be(layout.Modified.RowOf(3), "far away is level on both sides");
        layout.Original.RowCount.Should().Be(layout.Modified.RowCount);
    }
}
