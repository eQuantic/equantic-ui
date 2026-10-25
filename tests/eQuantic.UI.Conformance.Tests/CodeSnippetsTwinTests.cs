using System.Text;
using System.Text.Json;
using eQuantic.UI.Code;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The code engine's snippets (#1013) leave the same text, the same carets and the same session on both
/// sides: the C# engine, and its twin in the bundle the Server serves, run by the SDK's embedded Bun. A page
/// fills a snippet in through the twin and an IDE on Photon through .NET, so a stop placed a column off, a
/// mirror that missed a keystroke or a choice written over the wrong places would be two editors.
/// <para>
/// Compared over whole sessions: an editor over a real source file, a snippet of every construct inserted at
/// one caret or at several, then seeded keys that walk its stops, type in them, take a choice, delete, leave
/// it, undo and redo, read after every one: whether it was claimed, every selection, the primary first, the
/// line count, a hash of the whole text, whether a snippet is being filled in and whether a list shows.
/// </para>
/// </summary>
public class CodeSnippetsTwinTests
{
    /// <summary>One step: a snippet inserted (its index in <see cref="Bodies"/>), text typed, or a key.</summary>
    private sealed record Step(int Snippet, string? Text, string? Key, int Modifiers);

    private sealed record Session(bool Apple, CodePosition Caret, List<Step> Steps);

    private const int Shift = (int)KeyModifiers.Shift;
    private const int Alt = (int)KeyModifiers.Alt;
    private const int Command = (int)KeyModifiers.Command;

    /// <summary>A snippet of every construct: mirrors, a choice, a nested placeholder, the variables, an
    /// unknown variable, lines that take the indentation, and what is not syntax.</summary>
    private static readonly string[] Bodies =
    [
        "for (var ${1:i} = 0; $1 < ${2:count}; $1++)\n{\n\t$0\n}",
        "${1:name} = $1;",
        "${1|one,two,three|} = $1",
        "${1:new ${2:Thing}()}$0",
        "Log($TM_SELECTED_TEXT);",
        "${AUTHOR} wrote $1 on line $TM_LINE_NUMBER",
        "if ($1)\n{\n\t$2\n}$0",
        "${1:a}$2;",
        @"cost: $, ${1 and \$2 ${3|x\,y,z|}",
        "${TM_CURRENT_WORD:word}($1)$0",
    ];

    private static readonly string[] Typed = ["x", "count", "(", " ", "b"];

    private static uint Hash(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text) hash = (hash ^ c) * 16777619u;
        return hash;
    }

    private static string State(CodeEditorController editor, bool claimed)
    {
        var selections = string.Join(",", editor.Selections.Select(selection =>
            $"{selection.Anchor.Line}:{selection.Anchor.Column}>{selection.Focus.Line}:{selection.Focus.Column}"));
        return $"{(claimed ? 1 : 0)}|{selections}|{editor.Document.LineCount}|{Hash(editor.Document.Text)}"
            + $"|{(editor.Snippet.IsActive ? 1 : 0)}|{(editor.Completion.IsOpen ? 1 : 0)}";
    }

    private static List<(string State, bool Active)> RunOnDotNet(string text, Session session)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.Selection = new CodeRange(session.Caret);
        var convention = session.Apple ? KeyboardConvention.Apple : KeyboardConvention.Standard;
        var states = new List<(string, bool)>();
        foreach (var step in session.Steps)
        {
            var claimed = step.Snippet >= 0 ? editor.InsertSnippet(CodeSnippet.Parse(Bodies[step.Snippet]))
                : step.Text is { } typed ? editor.HandleText(typed)
                : editor.HandleKey(step.Key!, (KeyModifiers)step.Modifiers, convention, null);
            states.Add((State(editor, claimed), editor.Snippet.IsActive));
        }
        return states;
    }

    /// <summary>
    /// Seeded sessions over <paramref name="text"/>: from a random place, sometimes a caret or two added below,
    /// then rounds of a snippet inserted and filled in, weighted towards the keys a snippet answers.
    /// </summary>
    private static List<Session> Sessions(string text, int count, int seed)
    {
        var random = new Random(seed);
        var document = CodeDocument.FromText(text);
        var sessions = new List<Session>();
        for (var s = 0; s < count; s++)
        {
            var line = random.Next(document.LineCount - 4);
            var caret = new CodePosition(line, random.Next(document.Line(line).Length + 1));
            var steps = new List<Step>();
            for (var added = random.Next(3); added > 0; added--) steps.Add(new Step(-1, null, "ArrowDown", Alt | Command));
            for (var round = random.Next(2, 4); round > 0; round--)
            {
                steps.Add(new Step(random.Next(Bodies.Length), null, null, 0));
                for (var step = random.Next(4, 12); step > 0; step--)
                {
                    steps.Add(random.Next(30) switch
                    {
                        < 8 => new Step(-1, null, "Tab", 0),
                        < 10 => new Step(-1, null, "Tab", Shift),
                        < 16 => new Step(-1, Typed[random.Next(Typed.Length)], null, 0),
                        < 18 => new Step(-1, null, "Backspace", 0),
                        < 20 => new Step(-1, null, "ArrowDown", 0),
                        < 22 => new Step(-1, null, "Enter", 0),
                        < 23 => new Step(-1, null, "Escape", 0),
                        < 24 => new Step(-1, null, "End", 0),
                        < 25 => new Step(-1, null, "ArrowLeft", 0),
                        < 28 => new Step(-1, null, "z", Command),
                        _ => new Step(-1, null, "z", Shift | Command),
                    });
                }
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
            steps = session.Steps.Select(step => new object?[] { step.Snippet, step.Text, step.Key, step.Modifiers }),
        }));
        return $$"""
            import { CodeEditorController, CodeLanguages, CodeRange, CodePosition, CodeSnippet } from '{{runtime}}';
            const text = {{JsonSerializer.Serialize(text)}};
            const bodies = {{JsonSerializer.Serialize(Bodies)}};
            const sessions = {{encoded}};
            // An answer arrives on a later turn on the web, a choice's list among them: every step waits for one.
            const settle = () => new Promise((resolve) => setTimeout(resolve, 0));
            const hash = (text) => {
              let hash = 2166136261;
              for (let i = 0; i < text.length; i++) hash = Math.imul(hash ^ text.charCodeAt(i), 16777619) >>> 0;
              return hash;
            };
            const state = (editor, claimed) => {
              const selections = editor.selections.map((selection) =>
                `${selection.anchor.line}:${selection.anchor.column}>${selection.focus.line}:${selection.focus.column}`).join(',');
              return `${claimed ? 1 : 0}|${selections}|${editor.document.lineCount}|${hash(editor.document.text)}`
                + `|${editor.snippet.isActive ? 1 : 0}|${editor.completion.isOpen ? 1 : 0}`;
            };
            for (const session of sessions) {
              const editor = new CodeEditorController(text, CodeLanguages.cSharp);
              editor.selection = new CodeRange(new CodePosition(session.line, session.column));
              const convention = session.apple ? 'apple' : 'standard';
              const states = [];
              for (const [snippet, typed, key, modifiers] of session.steps) {
                const claimed = snippet >= 0 ? editor.insertSnippet(CodeSnippet.parse(bodies[snippet]))
                  : typed != null ? editor.handleText(typed)
                  : editor.handleKey(key, modifiers, convention, null);
                await settle();
                states.push(state(editor, claimed));
              }
              console.log('=' + JSON.stringify(states));
            }
            """;
    }

    [SkippableFact]
    public void Sessions_OverARealFile_LeaveTheSameTextCaretsAndSnippetAfterEveryKey()
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
        var filling = 0;
        var walked = 0;
        for (var i = 0; i < sessions.Count; i++)
        {
            var dotnet = RunOnDotNet(text, sessions[i]);
            steps += dotnet.Count;
            for (var step = 0; step < dotnet.Count; step++)
            {
                if (dotnet[step].Active) filling++;
                if (sessions[i].Steps[step].Key == "Tab" && dotnet[step].State[0] == '1') walked++;
            }
            var twin = i < web.Count ? JsonSerializer.Deserialize<List<string>>(web[i].TrimEnd('\r')[1..])! : [];
            var at = Enumerable.Range(0, dotnet.Count)
                .FirstOrDefault(step => step >= twin.Count || dotnet[step].State != twin[step], -1);
            if (at < 0) continue;
            if (count++ < 3)
            {
                var keys = string.Join(" ", sessions[i].Steps.Take(at + 1).Select(step =>
                    step.Snippet >= 0 ? $"<{Bodies[step.Snippet]}>"
                    : step.Text is { } t ? JsonSerializer.Serialize(t) : $"[{step.Key}+{step.Modifiers}]"));
                differences.Append($"\n  session {i} ({(sessions[i].Apple ? "apple" : "standard")}), step {at} after {keys}:"
                    + $"\n    .NET    {dotnet[at].State}\n    the web {(at < twin.Count ? twin[at] : "(none)")}");
            }
        }

        steps.Should().BeGreaterThan(1000, "the sessions are what is compared");
        filling.Should().BeGreaterThan(300, "most steps are made while a snippet is being filled in");
        walked.Should().BeGreaterThan(100, "Tab walks the stops often enough to compare");
        count.Should().Be(0, $"the snippets behave the same on both sides:{differences}");
    }
}
