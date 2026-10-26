using System.Text.Json;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Excel's chords answer the same on both sides (#984): the C# keymap and engine, and their twins in the
/// bundle the Server serves, run by the SDK's embedded Bun, in both keyboard traditions. A page moves the
/// selection through the twin and a window through .NET, so every chord must leave the same ranges and the
/// same active cell.
/// </summary>
public class SheetChordTwinTests
{
    private static string Runtime() =>
        ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");

    private static void OnBun() =>
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");

    private static List<string> Lines(string output) =>
        output.Split('\n').Where(line => line.StartsWith('=')).Select(line => line[1..]).ToList();

    /// <summary>A key with its modifiers (Shift 1, Alt 2, Command 4, Control 8), or text.</summary>
    private sealed record Step(string Kind, string Value, int Modifiers = 0);

    private static Step[] Session(bool apple)
    {
        var control = apple ? 8 : 4;
        return
        [
            new("key", " ", 1), new("key", "ArrowDown"), new("key", " ", control), new("key", "a", 4),
            new("key", "a", 4), new("key", "ArrowDown"), new("key", "End", 4), new("key", "End", 5),
            new("key", "Home", 4), new("key", "PageDown", 2), new("key", "PageUp", 2), new("key", "Backspace", control),
            new("key", "ArrowRight", 1), new("key", "ArrowDown", 1), new("key", ".", control), new("key", ".", control),
            new("key", "ArrowLeft"), new("text", "1"), new("key", "Tab"), new("text", "2"), new("key", "Tab"),
            new("key", "Enter"), new("key", "z", 4), new("key", "y", 4), new("key", "F8", 1), new("key", "Escape"),
        ];
    }

    private static string StateOf(SheetController sheet, bool claimed) =>
        string.Join("|", claimed,
            string.Join(";", sheet.Ranges.Select(range =>
                $"{range.Anchor.Row}:{range.Anchor.Col}-{range.Focus.Row}:{range.Focus.Col}")),
            $"{sheet.ActiveCell.Row}:{sheet.ActiveCell.Col}", sheet.RevealVersion, sheet.AddingRanges);

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryChord_LeavesTheSameSelection(bool apple)
    {
        OnBun();
        var session = Session(apple);
        var convention = apple ? KeyboardConvention.Apple : KeyboardConvention.Standard;

        var sheet = new SheetController(200, 30);
        var scroll = new eQuantic.UI.Components.TableScroll();
        scroll.Arrange(new Size(900, 600), 44 + sheet.Document.ColumnSpans.Total, 26 + sheet.Document.RowSpans.Total,
            44, 26, reduceMotion: true);
        sheet.Grid = new SheetGrid(scroll, 44, 26);
        foreach (var (row, col) in new[] { (1, 1), (1, 2), (2, 1), (2, 2), (6, 4) })
            sheet.Document.SetCell(new CellRef(row, col), "x");
        sheet.Selection = new SheetRange(new CellRef(1, 1));
        ISheetSurfaceModel model = sheet;
        var dotnet = new List<string>();
        foreach (var step in session)
        {
            var claimed = step.Kind == "text"
                ? model.HandleText(step.Value)
                : model.HandleKey(step.Value, (KeyModifiers)step.Modifiers, convention, null);
            dotnet.Add(StateOf(sheet, claimed));
        }

        var steps = session.Select(step => new object[] { step.Kind, step.Value, step.Modifiers });
        var program = $$"""
            import { CellRef, SheetController, SheetGrid, SheetRange, Size, TableScroll } from '{{Runtime()}}';
            const steps = {{JsonSerializer.Serialize(steps)}};
            const sheet = new SheetController(200, 30);
            const scroll = new TableScroll();
            scroll.arrange(new Size(900, 600), 44 + sheet.document.columnSpans.total, 26 + sheet.document.rowSpans.total, 44, 26, true);
            sheet.grid = new SheetGrid(scroll, 44, 26);
            for (const [row, col] of [[1, 1], [1, 2], [2, 1], [2, 2], [6, 4]]) sheet.document.setCell(new CellRef(row, col), 'x');
            sheet.selection = new SheetRange(new CellRef(1, 1));
            const name = (b) => (b ? 'True' : 'False');
            for (const [kind, value, modifiers] of steps) {
              const claimed = kind === 'text' ? sheet.handleText(value)
                : sheet.handleKey(value, modifiers, '{{(apple ? "apple" : "standard")}}', null);
              const ranges = sheet.ranges.map((r) => `${r.anchor.row}:${r.anchor.col}-${r.focus.row}:${r.focus.col}`).join(';');
              console.log('=' + [name(claimed), ranges, `${sheet.activeCell.row}:${sheet.activeCell.col}`,
                sheet.revealVersion, name(sheet.addingRanges)].join('|'));
            }
            """;

        var web = Lines(JsExecutor.Run(program, timeoutMs: 120_000));
        web.Should().HaveCount(dotnet.Count, "every step is answered on the web");
        for (var i = 0; i < dotnet.Count; i++)
            web[i].Should().Be(dotnet[i], $"step {i} ({session[i]}) answers alike");
    }
}
