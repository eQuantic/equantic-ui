using System.Globalization;
using System.Text.Json;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The status bar's summary answers the same on both sides (#984): which texts read as numbers in the page's
/// culture, how many cells the ranges cover once, and the sum and the average as the culture writes them,
/// from the C# engine and from its twin in the bundle the Server serves, run by the SDK's embedded Bun. The
/// twin writes them as the status bar's twin does, through the formatter a <c>ToString</c> in the current
/// culture lowers to.
/// </summary>
public class SheetSummaryTwinTests
{
    private static string Runtime() =>
        ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");

    private static void OnBun() =>
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");

    private static List<string> Lines(string output) =>
        output.Split('\n').Where(line => line.StartsWith('=')).Select(line => line[1..]).ToList();

    /// <summary>Column B from row 2 down: a decimal comma, a decimal point, an exponent, a group separator, a
    /// sign, white space, words, and what no culture reads.</summary>
    private static readonly string[] Values =
    [
        "7,01", "14,02", "texto", "2.5", "-1e2", "1.000", " 3 ", "+4", "(5)", "0x10", "1,000.5", "1e30", "1", "2", "2",
    ];

    /// <summary>Each selection: the ranges, as rows of column B (0-based, inclusive), the active one last.
    /// The first is set, and each other is a ⌘ drag down column B, as a person adds one.</summary>
    private static readonly int[][] Selections =
    [
        [1, 3],
        [1, 1],
        [1, 2, 2, 3],
        [1, 13],
        [12, 13, 13, 13, 1, 1],
        [13, 15],
    ];

    /// <summary>Over row <paramref name="row"/> of column B, the grid unplaced.</summary>
    private static Point At(int row) => new(96 + 48, row * 28 + 14);

    private static string Write(SheetSummary summary) =>
        string.Join("|", summary.Count, summary.Numbers, summary.Sum.ToString(CultureInfo.CurrentCulture),
            summary.Average?.ToString(CultureInfo.CurrentCulture) ?? "none",
            summary.Sum.ToString("G10", CultureInfo.CurrentCulture),
            summary.Average?.ToString("G10", CultureInfo.CurrentCulture) ?? "none");

    [SkippableTheory]
    [InlineData("en-US")]
    [InlineData("pt-BR")]
    public void EverySelection_SumsAlike(string culture)
    {
        OnBun();
        var before = CultureInfo.CurrentCulture;
        var dotnet = new List<string>();
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var sheet = new SheetController(100, 10);
            ISheetSurfaceModel model = sheet;
            for (var i = 0; i < Values.Length; i++) sheet.Document.SetCell(new CellRef(i + 1, 1), Values[i]);
            foreach (var rows in Selections)
            {
                sheet.Selection = new SheetRange(new CellRef(rows[0], 1), new CellRef(rows[1], 1));
                for (var r = 2; r < rows.Length; r += 2)
                {
                    model.HandlePointer(PointerPhase.Down, At(rows[r]), KeyModifiers.Command, 1, PointerKind.Mouse);
                    model.HandlePointer(PointerPhase.Move, At(rows[r + 1]), KeyModifiers.Command, 1, PointerKind.Mouse);
                    model.HandlePointer(PointerPhase.Up, At(rows[r + 1]), KeyModifiers.Command, 1, PointerKind.Mouse);
                }
                dotnet.Add(Write(sheet.Summary));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }

        var program = $$"""
            {{ConformanceRunner.CultureInstall(culture)}}
            import { $eq, CellRef, Point, SheetController, SheetRange } from '{{Runtime()}}';
            const values = {{JsonSerializer.Serialize(Values)}};
            const selections = {{JsonSerializer.Serialize(Selections)}};
            const sheet = new SheetController(100, 10);
            values.forEach((value, i) => sheet.document.setCell(new CellRef(i + 1, 1), value));
            for (const rows of selections) {
              sheet.selection = new SheetRange(new CellRef(rows[0], 1), new CellRef(rows[1], 1));
              const at = (row) => new Point(96 + 48, row * 28 + 14);
              for (let r = 2; r < rows.length; r += 2) {
                sheet.handlePointer('down', at(rows[r]), 4, 1, 'mouse');
                sheet.handlePointer('move', at(rows[r + 1]), 4, 1, 'mouse');
                sheet.handlePointer('up', at(rows[r + 1]), 4, 1, 'mouse');
              }
              const s = sheet.summary;
              const g10 = (v) => (v == null ? 'none' : $eq.text.format(v, 'G10'));
              console.log('=' + [s.count, s.numbers, $eq.text.format(s.sum, null), s.average == null ? 'none' : $eq.text.format(s.average, null),
                g10(s.sum), g10(s.average)].join('|'));
            }
            """;

        Lines(JsExecutor.Run(program, timeoutMs: 120_000)).Should().Equal(dotnet);
    }
}
