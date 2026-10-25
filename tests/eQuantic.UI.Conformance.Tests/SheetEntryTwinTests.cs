using System.Globalization;
using System.Text.Json;
using eQuantic.UI.Code;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using eQuantic.UI.Sheet;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A cell's entry answers the same on both sides (#983): the C# engines, and their twins in the bundle the
/// Server serves, run by the SDK's embedded Bun. A page edits a cell through the twin and a window through
/// .NET, so a caret placed by measure, a key of the entry, a composition, the date and time chords in a
/// page's culture and a reference the name box goes to must read alike, or the sheet is two sheets.
/// </summary>
public class SheetEntryTwinTests
{
    private static string Runtime() =>
        ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");

    private static void OnBun() =>
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");

    private static List<string> Lines(string output) =>
        output.Split('\n').Where(line => line.StartsWith('=')).Select(line => line[1..]).ToList();

    // ---- the code engine measures --------------------------------------------------------------------------

    [SkippableFact]
    public void AMeasuredGrid_PlacesCaretsBandsAndPressesAsDotNetDoes()
    {
        OnBun();
        // Ten points a character but `i` 3 and `W` 12, summed: a measure no count of cells reproduces.
        static float Measure(string text) => text.Sum(c => c switch { 'i' => 3f, 'W' => 12f, _ => 10f });
        string DotNet()
        {
            var editor = new CodeEditorController("iiWii\nWWWW\niiiiiiiiii")
            {
                Grid = new CodeGrid(new Point(10, 4), new Size(5, 20), Measure: Measure),
            };
            var answers = new List<string>();
            editor.Selection = new CodeRange(new CodePosition(0, 3));
            answers.Add($"{editor.Carets[0].X}");
            answers.Add($"{editor.PositionAt(new Point(10 + 13, 10)).Column}|{editor.PositionAt(new Point(10 + 16, 10)).Column}");
            editor.Selection = new CodeRange(new CodePosition(1, 1));
            editor.Execute(CodeCommand.CursorDown);
            answers.Add($"{editor.Caret.Line}:{editor.Caret.Column}");
            editor.Selection = new CodeRange(new CodePosition(0, 2), new CodePosition(1, 1));
            answers.Add(string.Join(";", editor.SelectionBandsIn(0, 2).Select(band => $"{band.X},{band.Y},{band.Width}")));
            answers.Add($"{editor.CharacterAt(new Point(10 + 7, 10))?.Column}");
            return string.Join("/", answers);
        }

        var program = $$"""
            import { CodeEditorController, CodeGrid, CodePosition, CodeRange, Point, Size } from '{{Runtime()}}';
            const measure = (text) => [...text].reduce((sum, c) => sum + (c === 'i' ? 3 : c === 'W' ? 12 : 10), 0);
            const editor = new CodeEditorController('iiWii\nWWWW\niiiiiiiiii');
            editor.grid = new CodeGrid(new Point(10, 4), new Size(5, 20), null, measure);
            const answers = [];
            editor.selection = new CodeRange(new CodePosition(0, 3));
            answers.push(`${editor.carets[0].x}`);
            answers.push(`${editor.positionAt(new Point(10 + 13, 10)).column}|${editor.positionAt(new Point(10 + 16, 10)).column}`);
            editor.selection = new CodeRange(new CodePosition(1, 1));
            editor.execute('cursorDown');
            answers.push(`${editor.caret.line}:${editor.caret.column}`);
            editor.selection = new CodeRange(new CodePosition(0, 2), new CodePosition(1, 1));
            answers.push(editor.selectionBandsIn(0, 2).map((band) => `${band.x},${band.y},${band.width}`).join(';'));
            answers.push(`${editor.characterAt(new Point(10 + 7, 10))?.column}`);
            console.log('=' + answers.join('/'));
            """;

        Lines(JsExecutor.Run(program, timeoutMs: 120_000)).Should().Equal(DotNet());
    }

    // ---- a session of keys, text and compositions -------------------------------------------------------

    /// <summary>One step of a session: a key with its modifiers, text, or a composition.</summary>
    private sealed record Step(string Kind, string Value, int Modifiers = 0);

    private static readonly Step[] Session =
    [
        new("text", "a"), new("text", "b"), new("comp", "に"), new("comp", "にほ"), new("text", "日本"),
        new("key", "F2"), new("key", "ArrowLeft"), new("key", "Home"), new("key", "ArrowRight", 1 | 4),
        new("text", "X"), new("key", "Enter", 2), new("text", "two"), new("key", "z", 4), new("key", "z", 4),
        new("key", "Enter"), new("key", "ArrowUp"), new("key", "Backspace"), new("text", "new"),
        new("key", "Escape"), new("key", "F2"), new("key", "End"), new("text", "!"), new("key", "Tab"),
        new("key", "ArrowLeft"), new("key", "Delete"), new("key", "z", 4), new("key", "Escape"), new("key", "Tab"),
        new("key", "ArrowRight"), new("key", "Tab"), new("comp", "x"), new("comp", ""), new("text", "\U0001F44D\U0001F3FD"),
        new("key", "Backspace"), new("text", "(\""), new("key", "Enter", 4),
    ];

    /// <summary>A text on one line of the output, its breaks marked, as the twin's side marks them.</summary>
    private static string Line(string text) => text.Replace("\n", "⏎");

    private static string StateOf(SheetController sheet, bool claimed)
    {
        var mode = sheet.EditMode.ToString();
        return string.Join("|", claimed, sheet.Editing, char.ToLowerInvariant(mode[0]) + mode[1..],
            Line(sheet.Entry.Document.Text), $"{sheet.Entry.Caret.Line}:{sheet.Entry.Caret.Column}",
            $"{sheet.ActiveCell.Row}:{sheet.ActiveCell.Col}", Line(sheet.Document.GetCell(new CellRef(1, 1))),
            Line(sheet.Document.GetCell(new CellRef(1, 2))));
    }

    [SkippableFact]
    public void ASessionOfKeysTextAndCompositions_LeavesTheSameEntryAndCells()
    {
        OnBun();
        var sheet = new SheetController(200, 30)
        {
            Grid = new SheetGrid(null, 0, 0) { Measure = text => text.Length * 10f, LineHeight = 20, CellPadding = EdgeInsets.Symmetric(6, 4) },
            Selection = new SheetRange(new CellRef(1, 1)),
        };
        ISheetSurfaceModel model = sheet;
        var dotnet = new List<string>();
        foreach (var step in Session)
        {
            var claimed = step.Kind switch
            {
                "text" => model.HandleText(step.Value),
                "comp" => model.SetComposition(step.Value),
                _ => model.HandleKey(step.Value, (KeyModifiers)step.Modifiers, KeyboardConvention.Standard, null),
            };
            dotnet.Add(StateOf(sheet, claimed));
        }

        var program = $$"""
            import { CellRef, EdgeInsets, SheetController, SheetGrid, SheetRange } from '{{Runtime()}}';
            const steps = {{JsonSerializer.Serialize(Session.Select(step => new[] { step.Kind, step.Value, step.Modifiers.ToString() }))}};
            const sheet = new SheetController(200, 30);
            const grid = new SheetGrid(null, 0, 0);
            grid.measure = (text) => text.length * 10;
            grid.lineHeight = 20;
            grid.cellPadding = EdgeInsets.symmetric(6, 4);
            sheet.grid = grid;
            sheet.selection = new SheetRange(new CellRef(1, 1));
            const name = (b) => (b ? 'True' : 'False');
            for (const [kind, value, modifiers] of steps) {
              const claimed = kind === 'text' ? sheet.handleText(value)
                : kind === 'comp' ? sheet.setComposition(value)
                : sheet.handleKey(value, Number(modifiers), 'standard', null);
              const line = (text) => text.replaceAll('\n', '⏎');
              console.log('=' + [name(claimed), name(sheet.editing), sheet.editMode, line(sheet.entry.document.text),
                `${sheet.entry.caret.line}:${sheet.entry.caret.column}`, `${sheet.activeCell.row}:${sheet.activeCell.col}`,
                line(sheet.document.getCell(new CellRef(1, 1))), line(sheet.document.getCell(new CellRef(1, 2)))].join('|'));
            }
            """;

        var web = Lines(JsExecutor.Run(program, timeoutMs: 120_000));
        web.Should().HaveCount(dotnet.Count, "every step is answered on the web");
        for (var i = 0; i < dotnet.Count; i++)
            web[i].Should().Be(dotnet[i], $"step {i} ({Session[i]}) answers alike");
    }

    // ---- the date and the time in a page's culture ------------------------------------------------------------

    [SkippableTheory]
    [InlineData("en-US")]
    [InlineData("pt-BR")]
    public void TheDateAndTheTimeChords_WriteWhatDotNetWritesInTheCulture(string culture)
    {
        OnBun();
        var moment = new DateTime(2026, 10, 25, 15, 45, 0);
        var before = CultureInfo.CurrentCulture;
        string dotnet;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var sheet = new SheetController(20, 5) { Now = () => moment };
            ISheetSurfaceModel model = sheet;
            model.HandleKey(";", KeyModifiers.Command, KeyboardConvention.Standard, null);
            model.HandleText(" ");
            model.HandleKey(":", KeyModifiers.Command | KeyModifiers.Shift, KeyboardConvention.Standard, null);
            dotnet = sheet.Entry.Document.Text;
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }

        var program = $$"""
            {{ConformanceRunner.CultureInstall(culture)}}
            import { DateTime, SheetController } from '{{Runtime()}}';
            const sheet = new SheetController(20, 5);
            sheet.now = () => new DateTime({{moment.Ticks}}n);
            sheet.handleKey(';', 4, 'standard', null);
            sheet.handleText(' ');
            sheet.handleKey(':', 5, 'standard', null);
            console.log('=' + sheet.entry.document.text);
            """;

        Lines(JsExecutor.Run(program, timeoutMs: 120_000)).Should().Equal(dotnet);
    }

    // ---- Go To ----------------------------------------------------------------------------------------------

    private static readonly string[] References =
    [
        "B7", "$B$7", "b7", "B7:D9", "d9:b7", "3:5", "$5:3", "B:D", "xfd1048576", "B0", "XFE1", "A1048577",
        "A99999999999", "1A", "A", "ABCD1", "B7:", ":", "A1:B", "Budget", "B 7", "",
    ];

    [SkippableFact]
    public void GoTo_ReadsEveryReferenceAsDotNetDoes()
    {
        OnBun();
        var dotnet = References.Select(text =>
        {
            var sheet = new SheetController(1_048_576, CellRef.MaxCols) { Selection = new SheetRange(new CellRef(2, 2)) };
            var went = sheet.GoTo(text);
            var s = sheet.Selection;
            return $"{went}|{s.TopRow},{s.LeftCol},{s.BottomRow},{s.RightCol}|{sheet.ActiveCell.Row},{sheet.ActiveCell.Col}";
        }).ToList();

        var program = $$"""
            import { CellRef, SheetController, SheetRange } from '{{Runtime()}}';
            for (const text of {{JsonSerializer.Serialize(References)}}) {
              const sheet = new SheetController(1048576, 16384);
              sheet.selection = new SheetRange(new CellRef(2, 2));
              const went = sheet.goTo(text);
              const s = sheet.selection;
              console.log('=' + [went ? 'True' : 'False', `${s.topRow},${s.leftCol},${s.bottomRow},${s.rightCol}`,
                `${sheet.activeCell.row},${sheet.activeCell.col}`].join('|'));
            }
            """;

        Lines(JsExecutor.Run(program, timeoutMs: 120_000)).Should().Equal(dotnet);
    }
}
