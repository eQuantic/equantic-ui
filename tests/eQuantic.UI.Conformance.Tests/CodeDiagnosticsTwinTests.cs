using System.Text;
using System.Text.Json;
using eQuantic.UI.Code;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The problems of the code engine (<see cref="CodeDiagnostics"/>) travel the same on both sides: the
/// C# engine, and its twin in the bundle the Server serves, run by the SDK's embedded Bun. A page draws
/// the squiggles the web carried through an edit and an IDE on Photon the ones .NET carried, so two
/// answers for one edit would be two editors (#299).
/// <para>
/// Compared two ways. A range carried through an edit (<see cref="CodeEdit.Carry"/>), over seeded
/// random ranges and edits that insert, delete and replace text across lines. And whole sessions: an
/// editor over a real source file holding seeded problems, driven by seeded keystrokes (typing, line
/// breaks, deletions, undo and redo, F8 and Shift+F8, Escape), its problems, its caret and its card read
/// after every one.
/// </para>
/// </summary>
public class CodeDiagnosticsTwinTests
{
    // ---- carrying a range --------------------------------------------------------------------------

    private static string Describe(CodeRange range) =>
        $"{range.Start.Line}:{range.Start.Column}-{range.End.Line}:{range.End.Column}";

    [SkippableFact]
    public void ARangeCarriedThroughAnEdit_LandsWhereDotNetPutsIt()
    {
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");

        var random = new Random(2026_10_19);
        CodePosition Position() => new(random.Next(6), random.Next(8));
        CodeRange Ordered(CodePosition a, CodePosition b) => a <= b ? new CodeRange(a, b) : new CodeRange(b, a);
        var cases = new List<(CodeRange Edit, string Inserted, CodeRange Range)>();
        for (var i = 0; i < 4000; i++)
        {
            var at = Position();
            var edit = random.Next(3) == 0 ? new CodeRange(at) : Ordered(at, Position());
            var inserted = random.Next(4) switch
            {
                0 => "",
                1 => "ab",
                2 => "x\nyz",
                _ => "\n\nq",
            };
            var start = Position();
            var range = random.Next(4) == 0 ? new CodeRange(start) : Ordered(start, Position());
            cases.Add((edit, inserted, range));
        }

        var encoded = JsonSerializer.Serialize(cases.Select(c => new object[]
        {
            c.Edit.Start.Line, c.Edit.Start.Column, c.Edit.End.Line, c.Edit.End.Column, c.Inserted,
            c.Range.Start.Line, c.Range.Start.Column, c.Range.End.Line, c.Range.End.Column,
        }));
        var program = $$"""
            import { CodeEdit, CodeRange, CodePosition } from '{{runtime}}';
            const cases = {{encoded}};
            const out = [];
            const range = (a, b, c, d) => new CodeRange(new CodePosition(a, b), new CodePosition(c, d));
            for (const [a, b, c, d, inserted, e, f, g, h] of cases) {
              const edit = new CodeEdit(range(a, b, c, d), '', inserted, range(a, b, a, b), range(a, b, a, b), false);
              const carried = edit.carry(range(e, f, g, h));
              out.push(`${carried.start.line}:${carried.start.column}-${carried.end.line}:${carried.end.column}`);
            }
            console.log('=' + out.join('\n='));
            """;
        var web = JsExecutor.Run(program, timeoutMs: 120_000).Split('\n');

        var differences = new StringBuilder();
        var count = 0;
        for (var i = 0; i < cases.Count; i++)
        {
            var (editRange, inserted, range) = cases[i];
            var edit = new CodeEdit(editRange, "", inserted, new CodeRange(editRange.Start), new CodeRange(editRange.Start), false);
            var dotnet = Describe(edit.Carry(range));
            var twin = i < web.Length && web[i].StartsWith('=') ? web[i].TrimEnd('\r')[1..] : "(no answer)";
            if (dotnet == twin) continue;
            if (count++ < 5)
                differences.Append($"\n  {Describe(range)} through {Describe(editRange)} <- {JsonSerializer.Serialize(inserted)}:\n    .NET    {dotnet}\n    the web {twin}");
        }

        count.Should().Be(0, $"a range is carried the same on both sides:{differences}");
    }

    // ---- whole sessions ----------------------------------------------------------------------------

    /// <summary>One keystroke: text typed, or a key with its modifiers.</summary>
    private sealed record Step(string? Text, string? Key, int Modifiers);

    private static string Severity(CodeDiagnosticSeverity severity)
    {
        var name = severity.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>What the editor holds after a step, as one line: every problem, the caret, the card.</summary>
    private static string State(CodeEditorController editor)
    {
        var problems = string.Join(",", editor.Diagnostics.Items.Select(problem =>
            $"{Describe(problem.Range)}/{Severity(problem.Severity)}"));
        var card = editor.Hover.IsOpen ? string.Join(";", editor.Hover.Problems.Select(problem => problem.Message)) : "-";
        return $"{problems}|{editor.Caret.Line}:{editor.Caret.Column}|{card}";
    }

    private static List<CodeDiagnostic> Problems(string text, int seed)
    {
        var random = new Random(seed);
        var document = CodeDocument.FromText(text);
        var problems = new List<CodeDiagnostic>();
        for (var i = 0; i < 40; i++)
        {
            var line = random.Next(document.LineCount);
            var length = document.Line(line).Length;
            var from = random.Next(length + 1);
            var to = Math.Min(length, from + random.Next(0, 12));
            problems.Add(new CodeDiagnostic(new CodeRange(new CodePosition(line, from), new CodePosition(line, to)),
                $"problem {i}", (CodeDiagnosticSeverity)random.Next(4)));
        }
        return problems;
    }

    private static List<(CodePosition Caret, List<Step> Steps)> Sessions(string text, int count, int seed)
    {
        var random = new Random(seed);
        var document = CodeDocument.FromText(text);
        var sessions = new List<(CodePosition, List<Step>)>();
        for (var s = 0; s < count; s++)
        {
            var line = random.Next(document.LineCount);
            var caret = new CodePosition(line, random.Next(document.Line(line).Length + 1));
            var steps = new List<Step>();
            for (var step = random.Next(10, 30); step > 0; step--)
            {
                steps.Add(random.Next(14) switch
                {
                    0 or 1 or 2 => new Step(random.Next(3) switch { 0 => "a", 1 => "(", _ => " " }, null, 0),
                    3 => new Step(null, "Enter", 0),
                    4 => new Step(null, "Backspace", 0),
                    5 => new Step(null, "Delete", 0),
                    6 => new Step(null, "z", (int)KeyModifiers.Command),
                    7 => new Step(null, "z", (int)(KeyModifiers.Command | KeyModifiers.Shift)),
                    8 => new Step(null, "F8", 0),
                    9 => new Step(null, "F8", (int)KeyModifiers.Shift),
                    10 => new Step(null, "Escape", 0),
                    11 => new Step(null, "ArrowDown", (int)KeyModifiers.Shift),
                    12 => new Step(null, "ArrowRight", 0),
                    _ => new Step(null, "Home", 0),
                });
            }
            sessions.Add((caret, steps));
        }
        return sessions;
    }

    private static List<string> RunOnDotNet(string text, IReadOnlyList<CodeDiagnostic> problems, CodePosition caret,
        IReadOnlyList<Step> steps)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.Diagnostics.Report(problems);
        editor.Selection = new CodeRange(caret, caret);
        var states = new List<string>();
        foreach (var step in steps)
        {
            if (step.Text is { } typed) editor.HandleText(typed);
            else editor.HandleKey(step.Key!, (KeyModifiers)step.Modifiers, KeyboardConvention.Standard, null);
            states.Add(State(editor));
        }
        return states;
    }

    private static string SessionProgram(string runtime, string text, IReadOnlyList<CodeDiagnostic> problems,
        IReadOnlyList<(CodePosition Caret, List<Step> Steps)> sessions)
    {
        var encodedProblems = JsonSerializer.Serialize(problems.Select(problem => new object[]
        {
            problem.Range.Start.Line, problem.Range.Start.Column, problem.Range.End.Line, problem.Range.End.Column,
            problem.Message, Severity(problem.Severity),
        }));
        var encoded = JsonSerializer.Serialize(sessions.Select(session => new
        {
            line = session.Caret.Line,
            column = session.Caret.Column,
            steps = session.Steps.Select(step => new object?[] { step.Text, step.Key, step.Modifiers }),
        }));
        return $$"""
            import { CodeEditorController, CodeLanguages, CodeDiagnostic, CodeRange, CodePosition } from '{{runtime}}';
            const text = {{JsonSerializer.Serialize(text)}};
            const problems = {{encodedProblems}};
            const sessions = {{encoded}};
            const describe = (range) => `${range.start.line}:${range.start.column}-${range.end.line}:${range.end.column}`;
            const state = (editor) => {
              const held = editor.diagnostics.items.map((problem) => `${describe(problem.range)}/${problem.severity}`).join(',');
              const card = editor.hover.isOpen ? editor.hover.problems.map((problem) => problem.message).join(';') : '-';
              return `${held}|${editor.caret.line}:${editor.caret.column}|${card}`;
            };
            for (const session of sessions) {
              const editor = new CodeEditorController(text, CodeLanguages.cSharp);
              editor.diagnostics.report(problems.map(([a, b, c, d, message, severity]) =>
                new CodeDiagnostic(new CodeRange(new CodePosition(a, b), new CodePosition(c, d)), message, severity)));
              const caret = new CodePosition(session.line, session.column);
              editor.selection = new CodeRange(caret, caret);
              const states = [];
              for (const [typed, key, modifiers] of session.steps) {
                if (typed != null) editor.handleText(typed);
                else editor.handleKey(key, modifiers, 'standard', null);
                states.push(state(editor));
              }
              console.log('=' + JSON.stringify(states));
            }
            """;
    }

    [SkippableFact]
    public void Sessions_OverARealFile_HoldTheSameProblemsAfterEveryKey()
    {
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        var text = File.ReadAllText(Path.Combine(RepoRoot.Find()!, "src", "eQuantic.UI.Code", "Intelligence", "CodeDiagnostics.cs"))
            .Replace("\r\n", "\n");
        var problems = Problems(text, 2026_10_19);
        var sessions = Sessions(text, 60, 2026_10_19);

        var web = JsExecutor.Run(SessionProgram(runtime, text, problems, sessions), timeoutMs: 300_000).Split('\n');

        var differences = new StringBuilder();
        var count = 0;
        var steps = 0;
        var carded = 0;
        for (var i = 0; i < sessions.Count; i++)
        {
            var dotnet = RunOnDotNet(text, problems, sessions[i].Caret, sessions[i].Steps);
            steps += dotnet.Count;
            carded += dotnet.Count(state => !state.EndsWith("|-", StringComparison.Ordinal));
            var twin = i < web.Length && web[i].StartsWith('=')
                ? JsonSerializer.Deserialize<List<string>>(web[i].TrimEnd('\r')[1..])!
                : [];
            var at = Enumerable.Range(0, dotnet.Count).FirstOrDefault(step => step >= twin.Count || dotnet[step] != twin[step], -1);
            if (at < 0) continue;
            if (count++ < 3)
            {
                var keys = string.Join(" ", sessions[i].Steps.Take(at + 1).Select(step => step.Text is { } t ? JsonSerializer.Serialize(t) : $"[{step.Key}+{step.Modifiers}]"));
                differences.Append($"\n  session {i}, step {at} after {keys}:\n    .NET    {dotnet[at][..Math.Min(400, dotnet[at].Length)]}\n    the web {(at < twin.Count ? twin[at][..Math.Min(400, twin[at].Length)] : "(none)")}");
            }
        }

        steps.Should().BeGreaterThan(600, "the sessions are what is compared");
        carded.Should().BeGreaterThan(20, "F8 shows a card often enough to compare what it holds");
        count.Should().Be(0, $"the problems travel the same on both sides:{differences}");
    }
}
