using System.Diagnostics;
using System.Globalization;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// What the status bar sums (#984): the cells in use the ranges cover, counted once, and the plain numbers
/// among them read in the page's culture until values are typed, with their sum and their average.
/// </summary>
public class SheetSummaryTests
{
    private static CellRef Cell(string a1)
    {
        SheetReference.TryParse(a1, 1_048_576, CellRef.MaxCols, out var range).Should().BeTrue();
        return range.TopLeft;
    }

    private static T InCulture<T>(string culture, Func<T> read)
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            return read();
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void Portuguese_ReadsItsDecimalComma()
    {
        var sheet = new SheetController(100, 10);
        sheet.Document.SetCell(Cell("B2"), "7,01");
        sheet.Document.SetCell(Cell("B3"), "14,02");
        sheet.Document.SetCell(Cell("B4"), "texto");
        sheet.Selection = new SheetRange(Cell("B2"), Cell("B4"));

        var summary = InCulture("pt-BR", () => sheet.Summary);

        summary.Should().Be(new SheetSummary(3, 2, 21.03m, 10.515m));
    }

    [Fact]
    public void English_ReadsAPlainNumber_AndNoThousandsSeparator()
    {
        var sheet = new SheetController(100, 10);
        sheet.Document.SetCell(Cell("B2"), "7,01");
        sheet.Document.SetCell(Cell("B3"), "2.5");
        sheet.Document.SetCell(Cell("B4"), "-1e2");
        sheet.Selection = new SheetRange(Cell("B2"), Cell("B4"));

        var summary = InCulture("en-US", () => sheet.Summary);

        summary.Count.Should().Be(3);
        summary.Numbers.Should().Be(2, "`7,01` is text where the comma is a thousands separator");
        summary.Sum.Should().Be(-97.5m);
    }

    [Fact]
    public void OverlappingRanges_CountACellOnce()
    {
        var sheet = new SheetController(100, 10);
        sheet.Document.SetCell(Cell("B2"), "1");
        sheet.Document.SetCell(Cell("B3"), "2");
        sheet.Selection = new SheetRange(Cell("B2"), Cell("B3"));
        ISheetSurfaceModel model = sheet;
        // ⌘ on B3, dragged to B4: a second range over B3.
        model.HandlePointer(PointerPhase.Down, new Point(96 + 48, 2 * 28 + 14), KeyModifiers.Command, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Move, new Point(96 + 48, 3 * 28 + 14), KeyModifiers.Command, 1, PointerKind.Mouse);
        model.HandlePointer(PointerPhase.Up, new Point(96 + 48, 3 * 28 + 14), KeyModifiers.Command, 1, PointerKind.Mouse);
        sheet.Ranges.Should().HaveCount(2);

        InCulture("en-US", () => sheet.Summary).Should().Be(new SheetSummary(2, 2, 3m, 1.5m));
    }

    [Fact]
    public void NoNumber_HasNoAverage()
    {
        var sheet = new SheetController(100, 10);
        sheet.Document.SetCell(Cell("B2"), "a");
        sheet.Document.SetCell(Cell("B3"), "b");
        sheet.Selection = new SheetRange(Cell("B2"), Cell("B3"));

        sheet.Summary.Should().Be(new SheetSummary(2, 0, 0m, null));
    }

    [Fact]
    public void TheSummary_FollowsTheValuesAndTheRanges()
    {
        var sheet = new SheetController(100, 10);
        sheet.Document.SetCell(Cell("B2"), "1");
        sheet.Document.SetCell(Cell("B3"), "2");
        sheet.Selection = new SheetRange(Cell("B2"), Cell("B3"));
        sheet.Summary.Sum.Should().Be(3);

        sheet.SetCell(Cell("B3"), "5");
        sheet.Summary.Sum.Should().Be(6, "a value changed");

        sheet.Selection = new SheetRange(Cell("B2"));
        sheet.Summary.Count.Should().Be(1, "the ranges changed");
    }

    /// <summary>
    /// The bench (#984): a sheet of 100 000 rows by 100 columns, a million cells in use (every row's
    /// first ten), and the whole sheet selected. The summary reads the stored cells, never the sheet's ten
    /// million, and a key that moves nothing reads it from the cache. The ceilings are an order of
    /// magnitude above what was measured, as the other sheet benches keep theirs, because CI machines are
    /// shared: what they catch is a summary that walks the sheet, or reads again on every key.
    /// </summary>
    [Fact]
    public void AWholeSheetSelected_CostsWhatItHolds_AndAKeyThatMovesNothingReadsNothing()
    {
        var sheet = new SheetController(100_000, 100);
        for (var row = 0; row < 100_000; row++)
            for (var col = 0; col < 10; col++)
                sheet.Document.SetCell(new CellRef(row, col), "1,5");
        sheet.SelectAll();

        var clock = Stopwatch.StartNew();
        var summary = InCulture("pt-BR", () => sheet.Summary);
        clock.Stop();
        summary.Should().Be(new SheetSummary(1_000_000, 1_000_000, 1_500_000m, 1.5m));
        // Measured at 99 ms on an M-series Mac, and 262 ms for the twin (spreadsheet-perf.spec.ts).
        clock.Elapsed.TotalMilliseconds.Should().BeLessThan(1000);

        ((ISheetSurfaceModel)sheet).HandleKey("Backspace", KeyModifiers.Command, KeyboardConvention.Standard, null);
        var again = Stopwatch.StartNew();
        var cached = InCulture("pt-BR", () => sheet.Summary);
        again.Stop();
        cached.Should().Be(summary);
        again.Elapsed.TotalMilliseconds.Should().BeLessThan(5, "nothing moved, so nothing is read again");
    }
}
