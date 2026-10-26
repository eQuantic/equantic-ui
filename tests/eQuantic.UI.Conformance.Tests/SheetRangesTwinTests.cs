using System.Text.Json;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Several ranges answer the same on both sides (#984): the C# engine, and its twin in the bundle the Server
/// serves, run by the SDK's embedded Bun. A page selects through the twin and a window through .NET, so a
/// press with ⌘, a drag that adds a range, add mode, Delete and Ctrl+Enter over several ranges, and the
/// plain move that leaves one must leave the same ranges and the same cells.
/// </summary>
public class SheetRangesTwinTests
{
    private static string Runtime() =>
        ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");

    private static void OnBun() =>
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");

    private static List<string> Lines(string output) =>
        output.Split('\n').Where(line => line.StartsWith('=')).Select(line => line[1..]).ToList();

    /// <summary>One step: a pointer phase over a cell (row, column) with its modifiers, a key, or text.</summary>
    private sealed record Step(string Kind, string Value, int Row = 0, int Col = 0, int Modifiers = 0);

    private static readonly Step[] Session =
    [
        new("down", "", 1, 1), new("up", "", 1, 1),
        new("down", "", 4, 4, 4), new("up", "", 4, 4, 4),
        new("down", "", 3, 3, 4), new("move", "", 5, 4, 4), new("up", "", 5, 4, 4),
        new("key", "F8", Modifiers: 1), new("down", "", 7, 6), new("up", "", 7, 6), new("key", "Escape"),
        new("key", "ArrowDown", Modifiers: 1), new("text", "x"), new("key", "Enter", Modifiers: 4),
        new("key", "z", Modifiers: 4), new("key", "Delete"), new("key", "z", Modifiers: 4),
        new("key", "ArrowDown"),
    ];

    private static string StateOf(SheetController sheet, bool claimed) =>
        string.Join("|", claimed,
            string.Join(";", sheet.Ranges.Select(range =>
                $"{range.Anchor.Row}:{range.Anchor.Col}-{range.Focus.Row}:{range.Focus.Col}")),
            $"{sheet.ActiveCell.Row}:{sheet.ActiveCell.Col}", sheet.AddingRanges,
            sheet.Document.GetCell(new CellRef(1, 1)), sheet.Document.GetCell(new CellRef(4, 4)),
            sheet.Document.GetCell(new CellRef(6, 4)));

    [SkippableFact]
    public void ASessionOfPressesAndKeys_LeavesTheSameRangesAndCells()
    {
        OnBun();
        var sheet = new SheetController(200, 30);
        ISheetSurfaceModel model = sheet;
        var dotnet = new List<string>();
        foreach (var step in Session)
        {
            var at = new Point(step.Col * 96f + 48f, step.Row * 28f + 14f);
            var modifiers = (KeyModifiers)step.Modifiers;
            var claimed = step.Kind switch
            {
                "down" => model.HandlePointer(PointerPhase.Down, at, modifiers, 1, PointerKind.Mouse),
                "move" => model.HandlePointer(PointerPhase.Move, at, modifiers, 1, PointerKind.Mouse),
                "up" => model.HandlePointer(PointerPhase.Up, at, modifiers, 1, PointerKind.Mouse),
                "text" => model.HandleText(step.Value),
                _ => model.HandleKey(step.Value, modifiers, KeyboardConvention.Standard, null),
            };
            dotnet.Add(StateOf(sheet, claimed));
        }

        var steps = Session.Select(step => new object[] { step.Kind, step.Value, step.Row, step.Col, step.Modifiers });
        var program = $$"""
            import { CellRef, Point, SheetController } from '{{Runtime()}}';
            const steps = {{JsonSerializer.Serialize(steps)}};
            const sheet = new SheetController(200, 30);
            const name = (b) => (b ? 'True' : 'False');
            for (const [kind, value, row, col, modifiers] of steps) {
              const at = new Point(col * 96 + 48, row * 28 + 14);
              const claimed = kind === 'down' ? sheet.handlePointer('down', at, modifiers, 1, 'mouse')
                : kind === 'move' ? sheet.handlePointer('move', at, modifiers, 1, 'mouse')
                : kind === 'up' ? sheet.handlePointer('up', at, modifiers, 1, 'mouse')
                : kind === 'text' ? sheet.handleText(value)
                : sheet.handleKey(value, modifiers, 'standard', null);
              const ranges = sheet.ranges.map((r) => `${r.anchor.row}:${r.anchor.col}-${r.focus.row}:${r.focus.col}`).join(';');
              console.log('=' + [name(claimed), ranges, `${sheet.activeCell.row}:${sheet.activeCell.col}`,
                name(sheet.addingRanges), sheet.document.getCell(new CellRef(1, 1)),
                sheet.document.getCell(new CellRef(4, 4)), sheet.document.getCell(new CellRef(6, 4))].join('|'));
            }
            """;

        var web = Lines(JsExecutor.Run(program, timeoutMs: 120_000));
        web.Should().HaveCount(dotnet.Count, "every step is answered on the web");
        for (var i = 0; i < dotnet.Count; i++)
            web[i].Should().Be(dotnet[i], $"step {i} ({Session[i]}) answers alike");
    }
}
