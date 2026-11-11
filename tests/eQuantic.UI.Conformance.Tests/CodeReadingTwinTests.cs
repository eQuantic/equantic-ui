using System.Text.Json;
using eQuantic.UI.Code;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// What a screen reader is handed of a code surface (<see cref="CodeEditorController.Reading"/>, #1333) is the
/// same on both sides: the C# engine Photon reads, and its twin in the bundle the Server serves, which the web
/// writes into the field. A page and a window would otherwise read the same caret on different lines.
/// <para>
/// Compared over a real source file with selections placed by a seeded random: carets at the first and the
/// last lines (where the window is cut short), forward and backward selections, ones that run past the
/// window on either side, and the line a surrogate pair stands on, read as the text, the anchor and the focus.
/// </para>
/// </summary>
public class CodeReadingTwinTests
{
    private sealed record Placed(int AL, int AC, int FL, int FC);

    private static List<Placed> Selections(string text, int count, int seed)
    {
        var lines = text.Split('\n');
        var random = new Random(seed);
        CodePosition Any()
        {
            var line = random.Next(lines.Length);
            return new CodePosition(line, random.Next(lines[line].Length + 1));
        }
        var placed = new List<Placed>
        {
            new(0, 0, 0, 0),
            new(lines.Length - 1, lines[^1].Length, lines.Length - 1, lines[^1].Length),
            new(0, 0, lines.Length - 1, 0),
            new(lines.Length - 1, 0, 0, 0),
        };
        for (var i = 0; i < count; i++)
        {
            var anchor = Any();
            var focus = random.Next(3) == 0 ? anchor : Any();
            placed.Add(new Placed(anchor.Line, anchor.Column, focus.Line, focus.Column));
        }
        return placed;
    }

    private static string OnDotNet(string text, Placed at)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp)
        {
            Selection = new CodeRange(new CodePosition(at.AL, at.AC), new CodePosition(at.FL, at.FC)),
        };
        var reading = editor.Reading;
        return JsonSerializer.Serialize(new object[] { reading.Text, reading.Anchor, reading.Focus });
    }

    [SkippableFact]
    public void TheReading_IsTheSameOnBothSides_WhereverTheSelectionStands()
    {
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        // A real file, with a line outside the Basic Multilingual Plane among its own, so an offset counted in
        // characters rather than in UTF-16 code units would part.
        var text = File.ReadAllText(Path.Combine(RepoRoot.Find()!, "src", "eQuantic.UI.Code", "Editing", "CodeKeymap.cs"))
            .Replace("\r\n", "\n") + "\n// 😀 smile 𐐨 end";
        var selections = Selections(text, 60, 2026_11_11);

        var program = $$"""
            import { CodeEditorController, CodeLanguages, CodeRange, CodePosition } from '{{runtime}}';
            const text = {{JsonSerializer.Serialize(text)}};
            const selections = {{JsonSerializer.Serialize(selections)}};
            for (const at of selections) {
              const editor = new CodeEditorController(text, CodeLanguages.cSharp);
              editor.selection = new CodeRange(new CodePosition(at.AL, at.AC), new CodePosition(at.FL, at.FC));
              const reading = editor.reading;
              console.log('=' + JSON.stringify([reading.text, reading.anchor, reading.focus]));
            }
            """;
        var web = JsExecutor.Run(program, timeoutMs: 120_000).Split('\n')
            .Where(line => line.StartsWith('=')).Select(line => line[1..]).ToList();

        web.Should().HaveCount(selections.Count, "every selection is read on the web");
        for (var i = 0; i < selections.Count; i++)
            web[i].Should().Be(OnDotNet(text, selections[i]), $"the selection {selections[i]} reads alike");
    }

    [Fact]
    public void TheReading_IsTheLinesAroundTheCaret_WithTheSelectionCountedInThem()
    {
        var text = string.Join("\n", Enumerable.Range(0, 40).Select(line => $"line {line}"));
        var editor = new CodeEditorController(text)
        {
            Selection = new CodeRange(new CodePosition(20, 2), new CodePosition(21, 4)),
        };

        var reading = editor.Reading;

        var lines = reading.Text.Split('\n');
        lines.Should().HaveCount(2 * CodeEditorController.ReadingLines + 1, "the caret's line and a page on each side");
        lines[0].Should().Be($"line {21 - CodeEditorController.ReadingLines}");
        lines[^1].Should().Be($"line {21 + CodeEditorController.ReadingLines}");
        reading.Text[reading.Focus..].Should().StartWith(" 21", "the focus is the caret, at line 21's column 4");
        reading.Text[reading.Anchor..].Should().StartWith("ne 20", "the anchor is line 20's column 2");

        editor.Selection = new CodeRange(new CodePosition(0, 0), new CodePosition(39, 0));
        editor.Reading.Anchor.Should().Be(0, "an anchor above the window stands at its start");

        editor.Selection = new CodeRange(new CodePosition(39, 0), new CodePosition(0, 0));
        editor.Reading.Anchor.Should().Be(editor.Reading.Text.Length, "an anchor below the window stands at its end");
    }
}
