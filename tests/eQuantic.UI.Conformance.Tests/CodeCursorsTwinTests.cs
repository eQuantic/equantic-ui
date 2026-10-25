using System.Text;
using System.Text.Json;
using eQuantic.UI.Code;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The code engine's several carets (#1012) leave the same text and the same carets on both sides: the C#
/// engine, and its twin in the bundle the Server serves, run by the SDK's embedded Bun. A page edits through
/// the twin and an IDE on Photon through .NET, so a placement that put one caret a column off, or a merge
/// that kept two carets the other side made one, would be two editors.
/// <para>
/// Compared over whole sessions: an editor over a real source file, driven in one of the two keyboard
/// conventions by seeded keystrokes that add carets (above and below, the next occurrence, every
/// occurrence) and take them away again, and between them type, delete, indent, comment, move and copy
/// lines, undo and redo, read after every one: whether it was claimed, every selection, the primary first,
/// the line count and a hash of the whole text.
/// </para>
/// </summary>
public class CodeCursorsTwinTests
{
    /// <summary>One step: text typed, or a key with its modifiers.</summary>
    private sealed record Step(string? Text, string? Key, int Modifiers);

    private sealed record Session(bool Apple, CodePosition Caret, List<Step> Steps);

    private const int Shift = (int)KeyModifiers.Shift;
    private const int Alt = (int)KeyModifiers.Alt;
    private const int Command = (int)KeyModifiers.Command;

    /// <summary>What a session types: a letter, a word, a bracket that pairs, a quote, white space.</summary>
    private static readonly string[] Typed = ["x", "count", "(", "\"", " ", "\t", "{", "}"];

    /// <summary>The steps the coverage counts, by what each one is there to do with the carets.</summary>
    private static readonly Dictionary<(string Key, int Modifiers), string> CaretKeys = new()
    {
        [("ArrowDown", Alt | Command)] = "AddCursorBelow",
        [("ArrowUp", Alt | Command)] = "AddCursorAbove",
        [("d", Command)] = "AddNextOccurrence",
        [("l", Shift | Command)] = "SelectAllOccurrences",
        [("Escape", 0)] = "RemoveSecondaryCursors",
    };

    /// <summary>The 32-bit FNV-1a of the text's UTF-16 code units, the program the twin runs computing the
    /// same.</summary>
    private static uint Hash(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text) hash = (hash ^ c) * 16777619u;
        return hash;
    }

    /// <summary>What a step leaves, as one line.</summary>
    private static string State(CodeEditorController editor, bool claimed)
    {
        var selections = string.Join(",", editor.Selections.Select(selection =>
            $"{selection.Anchor.Line}:{selection.Anchor.Column}>{selection.Focus.Line}:{selection.Focus.Column}"));
        return $"{(claimed ? 1 : 0)}|{selections}|{editor.Document.LineCount}|{Hash(editor.Document.Text)}";
    }

    private static List<(string State, int Carets)> RunOnDotNet(string text, Session session)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.Selection = new CodeRange(session.Caret);
        var convention = session.Apple ? KeyboardConvention.Apple : KeyboardConvention.Standard;
        var states = new List<(string, int)>();
        foreach (var step in session.Steps)
        {
            var claimed = step.Text is { } typed ? editor.HandleText(typed)
                : editor.HandleKey(step.Key!, (KeyModifiers)step.Modifiers, convention, null);
            states.Add((State(editor, claimed), editor.Selections.Count));
        }
        return states;
    }

    /// <summary>
    /// Seeded sessions over <paramref name="text"/>: from a random place, rounds of carets added and taken
    /// away, and what every caret then does, weighted towards several carets at once, which are what is
    /// compared.
    /// </summary>
    private static List<Session> Sessions(string text, int count, int seed)
    {
        var random = new Random(seed);
        var document = CodeDocument.FromText(text);
        var sessions = new List<Session>();
        for (var s = 0; s < count; s++)
        {
            var line = random.Next(document.LineCount);
            var caret = new CodePosition(line, random.Next(document.Line(line).Length + 1));
            var steps = new List<Step>();
            for (var step = random.Next(16, 36); step > 0; step--)
            {
                steps.Add(random.Next(46) switch
                {
                    < 6 => new Step(null, "ArrowDown", Alt | Command),
                    < 8 => new Step(null, "ArrowUp", Alt | Command),
                    < 12 => new Step(null, "d", Command),
                    < 13 => new Step(null, "l", Shift | Command),
                    < 14 => new Step(null, "Escape", 0),
                    < 20 => new Step(Typed[random.Next(Typed.Length)], null, 0),
                    < 22 => new Step(null, "Backspace", 0),
                    < 23 => new Step(null, "Backspace", Alt),
                    < 24 => new Step(null, "Delete", 0),
                    < 25 => new Step(null, "Enter", 0),
                    < 26 => new Step(null, "Tab", 0),
                    < 27 => new Step(null, "Tab", Shift),
                    < 28 => new Step(null, "/", Command),
                    < 29 => new Step(null, "ArrowUp", Alt),
                    < 30 => new Step(null, "ArrowDown", Alt),
                    < 31 => new Step(null, "ArrowDown", Shift | Alt),
                    < 32 => new Step(null, "k", Shift | Command),
                    < 33 => new Step(null, "Enter", Command),
                    < 35 => new Step(null, "ArrowRight", Shift),
                    < 37 => new Step(null, "ArrowLeft", 0),
                    < 39 => new Step(null, "ArrowDown", 0),
                    < 40 => new Step(null, "End", 0),
                    < 43 => new Step(null, "z", Command),
                    _ => new Step(null, "z", Shift | Command),
                });
            }
            sessions.Add(new Session(random.Next(2) == 0, caret, steps));
        }
        return sessions;
    }

    private static string SessionProgram(string runtime, string text, IReadOnlyList<Session> sessions)
    {
        var encoded = JsonSerializer.Serialize(sessions.Select(session => new
        {
            apple = session.Apple,
            line = session.Caret.Line,
            column = session.Caret.Column,
            steps = session.Steps.Select(step => new object?[] { step.Text, step.Key, step.Modifiers }),
        }));
        return $$"""
            import { CodeEditorController, CodeLanguages, CodeRange, CodePosition } from '{{runtime}}';
            const text = {{JsonSerializer.Serialize(text)}};
            const sessions = {{encoded}};
            const hash = (text) => {
              let hash = 2166136261;
              for (let i = 0; i < text.length; i++) hash = Math.imul(hash ^ text.charCodeAt(i), 16777619) >>> 0;
              return hash;
            };
            const state = (editor, claimed) => {
              const selections = editor.selections.map((selection) =>
                `${selection.anchor.line}:${selection.anchor.column}>${selection.focus.line}:${selection.focus.column}`).join(',');
              return `${claimed ? 1 : 0}|${selections}|${editor.document.lineCount}|${hash(editor.document.text)}`;
            };
            for (const session of sessions) {
              const editor = new CodeEditorController(text, CodeLanguages.cSharp);
              editor.selection = new CodeRange(new CodePosition(session.line, session.column));
              const convention = session.apple ? 'apple' : 'standard';
              const states = [];
              for (const [typed, key, modifiers] of session.steps) {
                const claimed = typed != null ? editor.handleText(typed) : editor.handleKey(key, modifiers, convention, null);
                states.push(state(editor, claimed));
              }
              console.log('=' + JSON.stringify(states));
            }
            """;
    }

    [SkippableFact]
    public void Sessions_OverARealFile_LeaveTheSameTextAndCaretsAfterEveryKey()
    {
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        var text = File.ReadAllText(Path.Combine(RepoRoot.Find()!, "src", "eQuantic.UI.Code", "Editing", "CodeKeymap.cs"))
            .Replace("\r\n", "\n");
        var sessions = Sessions(text, 80, 2026_10_25);

        var web = JsExecutor.Run(SessionProgram(runtime, text, sessions), timeoutMs: 300_000)
            .Split('\n').Where(line => line.StartsWith('=')).ToList();

        var differences = new StringBuilder();
        var count = 0;
        var steps = 0;
        var several = 0;
        var ran = new Dictionary<string, int>();
        for (var i = 0; i < sessions.Count; i++)
        {
            var dotnet = RunOnDotNet(text, sessions[i]);
            steps += dotnet.Count;
            for (var step = 0; step < dotnet.Count; step++)
            {
                if (dotnet[step].Carets > 1) several++;
                var key = sessions[i].Steps[step];
                if (dotnet[step].State[0] == '1' && key.Key is { } name
                    && CaretKeys.TryGetValue((name, key.Modifiers), out var command))
                    ran[command] = ran.GetValueOrDefault(command) + 1;
            }
            var twin = i < web.Count ? JsonSerializer.Deserialize<List<string>>(web[i].TrimEnd('\r')[1..])! : [];
            var at = Enumerable.Range(0, dotnet.Count)
                .FirstOrDefault(step => step >= twin.Count || dotnet[step].State != twin[step], -1);
            if (at < 0) continue;
            if (count++ < 3)
            {
                var keys = string.Join(" ", sessions[i].Steps.Take(at + 1).Select(step =>
                    step.Text is { } t ? JsonSerializer.Serialize(t) : $"[{step.Key}+{step.Modifiers}]"));
                differences.Append($"\n  session {i} ({(sessions[i].Apple ? "apple" : "standard")}), step {at} after {keys}:"
                    + $"\n    .NET    {dotnet[at].State}\n    the web {(at < twin.Count ? twin[at] : "(none)")}");
            }
        }

        steps.Should().BeGreaterThan(1200, "the sessions are what is compared");
        several.Should().BeGreaterThan(400, "most steps are made with several carets");
        foreach (var command in CaretKeys.Values)
            ran.GetValueOrDefault(command).Should().BeGreaterThan(5, $"{command} runs often enough to compare");
        count.Should().Be(0, $"the carets behave the same on both sides:{differences}");
    }
}
