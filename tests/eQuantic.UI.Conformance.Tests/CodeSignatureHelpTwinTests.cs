using System.Text;
using System.Text.Json;
using eQuantic.UI.Code;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The signature help of the code engine (<see cref="CodeSignatureHelpSession"/>) answers the same on both
/// sides: the C# engine, and its twin in the bundle the Server serves, run by the SDK's embedded Bun. A page
/// draws the panel the web opened and an IDE on Photon the one .NET opened, so two panels for one call would
/// be two editors (#298).
/// <para>
/// Compared over whole sessions: an editor over a real source file, a language service in miniature as its
/// provider (written once here and once in the program the twin runs), driven by seeded keystrokes (a call
/// opened, its arguments, a named one, a closing parenthesis, the arrows over the overloads, ⌥↓, ⇧⌘Space,
/// Escape), the panel read after every one: whether it shows, the signature and the argument it marks, and
/// the caret and the line it leaves. On the web every answer arrives on a later turn of the event loop, so
/// each step there waits for one.
/// </para>
/// </summary>
public class CodeSignatureHelpTwinTests
{
    /// <summary>One keystroke: text typed, or a key with its modifiers.</summary>
    private sealed record Step(string? Text, string? Key, int Modifiers);

    private static CodeSignature Signature(string label, params string[] parameters) =>
        new(label) { Parameters = [.. parameters.Select(parameter => new CodeSignatureParameter(parameter))] };

    /// <summary>
    /// A language service in miniature: the call the caret is in (the innermost parenthesis still open before
    /// it on its line), the name before that parenthesis, the argument the caret is on (the commas at the
    /// call's depth) or the one it names (<c>gap:</c>, <c>main:</c>), and the first overload with room for that
    /// argument. Nothing outside a call, or for a name it does not know. The program the twin runs carries the
    /// same function, line for line.
    /// </summary>
    private sealed class Calls : ICodeSignatureProvider
    {
        private static readonly Dictionary<string, CodeSignature[]> Known = new(StringComparer.Ordinal)
        {
            ["Add"] =
            [
                Signature("Add(int value)", "int value"),
                Signature("Add(int value, int times)", "int value", "int times"),
                Signature("Add(string text)", "string text"),
            ],
            ["Column"] = [Signature("Column(float gap, MainAlign main)", "float gap", "MainAlign main")],
        };

        public Task<CodeSignatureHelp?> SignatureHelpAsync(CodeDocument document, CodePosition position,
            CodeSignatureContext context, CancellationToken cancellation) =>
            Task.FromResult(Answer(document.Line(position.Line), position.Column));

        private static CodeSignatureHelp? Answer(string line, int caret)
        {
            var end = Math.Min(caret, line.Length);
            var depth = 0;
            var open = -1;
            for (var i = end - 1; i >= 0 && open < 0; i--)
            {
                if (line[i] == ')') depth++;
                else if (line[i] == '(')
                {
                    if (depth == 0) open = i;
                    else depth--;
                }
            }
            if (open < 0) return null;
            var start = open;
            while (start > 0 && CodeDocument.IsWordChar(line[start - 1])) start--;
            if (!Known.TryGetValue(line[start..open], out var signatures)) return null;

            var argument = 0;
            var from = open + 1;
            depth = 0;
            for (var i = open + 1; i < end; i++)
            {
                if (line[i] == '(') depth++;
                else if (line[i] == ')') depth--;
                else if (line[i] == ',' && depth == 0)
                {
                    argument++;
                    from = i + 1;
                }
            }
            var typed = line[from..end].Trim();
            var active = typed.StartsWith("gap:", StringComparison.Ordinal) ? 0
                : typed.StartsWith("main:", StringComparison.Ordinal) ? 1
                : argument;
            var chosen = 0;
            for (var s = 0; s < signatures.Length; s++)
            {
                if (signatures[s].Parameters.Count <= argument) continue;
                chosen = s;
                break;
            }
            return new CodeSignatureHelp(signatures) { ActiveSignature = chosen, ActiveParameter = active };
        }
    }

    /// <summary>The program's copy of <see cref="Calls"/>, line for line.</summary>
    private const string CallsInJs = """
        const signature = (label, ...parameters) => {
          const made = new CodeSignature(label);
          made.parameters = parameters.map((parameter) => new CodeSignatureParameter(parameter));
          return made;
        };
        const known = new Map([
          ['Add', [
            signature('Add(int value)', 'int value'),
            signature('Add(int value, int times)', 'int value', 'int times'),
            signature('Add(string text)', 'string text'),
          ]],
          ['Column', [signature('Column(float gap, MainAlign main)', 'float gap', 'MainAlign main')]],
        ]);
        const answer = (line, caret) => {
          const end = Math.min(caret, line.length);
          let depth = 0;
          let open = -1;
          for (let i = end - 1; i >= 0 && open < 0; i--) {
            if (line[i] === ')') depth++;
            else if (line[i] === '(') {
              if (depth === 0) open = i;
              else depth--;
            }
          }
          if (open < 0) return null;
          let start = open;
          while (start > 0 && CodeDocument.isWordChar(line[start - 1])) start--;
          const signatures = known.get(line.slice(start, open));
          if (signatures === undefined) return null;
          let argument = 0;
          let from = open + 1;
          depth = 0;
          for (let i = open + 1; i < end; i++) {
            if (line[i] === '(') depth++;
            else if (line[i] === ')') depth--;
            else if (line[i] === ',' && depth === 0) {
              argument++;
              from = i + 1;
            }
          }
          const typed = line.slice(from, end).trim();
          const active = typed.startsWith('gap:') ? 0 : typed.startsWith('main:') ? 1 : argument;
          let chosen = 0;
          for (let s = 0; s < signatures.length; s++) {
            if (signatures[s].parameters.length <= argument) continue;
            chosen = s;
            break;
          }
          const help = new CodeSignatureHelp(signatures);
          help.activeSignature = chosen;
          help.activeParameter = active;
          return help;
        };
        const calls = {
          triggerCharacters: ['('],
          retriggerCharacters: [','],
          signatureHelpAsync: (document, position) => Promise.resolve(answer(document.line(position.line), position.column)),
        };
        """;

    /// <summary>What the panel and the editor show after a step, as one line.</summary>
    private static string State(CodeEditorController editor)
    {
        var help = editor.SignatureHelp;
        var line = editor.Document.Line(editor.Caret.Line);
        return $"{(help.IsOpen ? "open" : "shut")}|{help.ActiveSignature}|{help.ActiveParameter}|{help.Signature?.Label ?? "-"}"
            + $"|{editor.Caret.Line}:{editor.Caret.Column}|{line}";
    }

    private static List<string> RunOnDotNet(string text, CodePosition caret, IReadOnlyList<Step> steps)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.SignatureHelp.Delay = TimeSpan.Zero;
        editor.SignatureHelp.Providers.Add(new Calls());
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

    /// <summary>
    /// Seeded sessions over <paramref name="text"/>: from the end of a random line, a new line, then rounds of
    /// a call opened (one the provider knows, or one it does not) and what a person does inside it.
    /// </summary>
    private static List<(CodePosition Caret, List<Step> Steps)> Sessions(string text, int count, int seed)
    {
        var random = new Random(seed);
        var document = CodeDocument.FromText(text);
        string[] calls = ["Add(", "x.Add(", "Column(", "new Column(", "Nope("];
        var sessions = new List<(CodePosition, List<Step>)>();
        for (var s = 0; s < count; s++)
        {
            var line = random.Next(document.LineCount);
            var caret = new CodePosition(line, document.Line(line).Length);
            var steps = new List<Step> { new(null, "Enter", 0) };
            for (var round = random.Next(1, 4); round > 0; round--)
            {
                foreach (var c in calls[random.Next(calls.Length)]) steps.Add(new Step(c.ToString(), null, 0));
                // Weighted towards what keeps the caret in the call, so the panel shows for a good part of
                // the steps; what leaves it (a parenthesis closed or opened, Escape, End) comes now and then,
                // and ⇧⌘Space asks again after it.
                for (var step = random.Next(3, 12); step > 0; step--)
                {
                    switch (random.Next(24))
                    {
                        case < 3: steps.Add(new Step("1", null, 0)); break;
                        case < 6: steps.Add(new Step(",", null, 0)); break;
                        case < 7: steps.Add(new Step(" ", null, 0)); break;
                        case < 9:
                            foreach (var c in random.Next(2) == 0 ? "gap: " : "main: ") steps.Add(new Step(c.ToString(), null, 0));
                            break;
                        case < 11: steps.Add(new Step(null, "ArrowDown", 0)); break;
                        case < 13: steps.Add(new Step(null, "ArrowUp", 0)); break;
                        case < 15: steps.Add(new Step(null, "ArrowDown", (int)KeyModifiers.Alt)); break;
                        case < 17: steps.Add(new Step(null, " ", (int)(KeyModifiers.Command | KeyModifiers.Shift))); break;
                        case < 18: steps.Add(new Step(null, "ArrowLeft", 0)); break;
                        case < 19: steps.Add(new Step(null, "ArrowRight", 0)); break;
                        case < 20: steps.Add(new Step(null, "Backspace", 0)); break;
                        case < 21: steps.Add(new Step(")", null, 0)); break;
                        case < 22: steps.Add(new Step("(", null, 0)); break;
                        case < 23: steps.Add(new Step(null, "Escape", 0)); break;
                        default: steps.Add(new Step(null, "End", 0)); break;
                    }
                }
            }
            sessions.Add((caret, steps));
        }
        return sessions;
    }

    private static string SessionProgram(string runtime, string text, IReadOnlyList<(CodePosition Caret, List<Step> Steps)> sessions)
    {
        var encoded = JsonSerializer.Serialize(sessions.Select(session => new
        {
            line = session.Caret.Line,
            column = session.Caret.Column,
            steps = session.Steps.Select(step => new object?[] { step.Text, step.Key, step.Modifiers }),
        }));
        return $$"""
            import { $eq, CodeEditorController, CodeLanguages, CodeDocument, CodeSignature, CodeSignatureParameter,
              CodeSignatureHelp, CodeRange, CodePosition } from '{{runtime}}';
            {{CallsInJs}}
            const text = {{JsonSerializer.Serialize(text)}};
            const sessions = {{encoded}};
            // An answer arrives on a later turn on the web: every step waits for one.
            const settle = () => new Promise((resolve) => setTimeout(resolve, 0));
            const state = (editor) => {
              const help = editor.signatureHelp;
              const line = editor.document.line(editor.caret.line);
              return `${help.isOpen ? 'open' : 'shut'}|${help.activeSignature}|${help.activeParameter}|${help.signature?.label ?? '-'}`
                + `|${editor.caret.line}:${editor.caret.column}|${line}`;
            };
            for (const session of sessions) {
              const editor = new CodeEditorController(text, CodeLanguages.cSharp);
              editor.signatureHelp.delay = $eq.time.timeSpan.zero;
              editor.signatureHelp.providers.push(calls);
              const caret = new CodePosition(session.line, session.column);
              editor.selection = new CodeRange(caret, caret);
              const states = [];
              for (const [typed, key, modifiers] of session.steps) {
                if (typed != null) editor.handleText(typed);
                else editor.handleKey(key, modifiers, 'standard', null);
                await settle();
                states.push(state(editor));
              }
              console.log('=' + JSON.stringify(states));
            }
            """;
    }

    [SkippableFact]
    public void Sessions_OverARealFile_ShowTheSamePanelAfterEveryKey()
    {
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        var text = File.ReadAllText(Path.Combine(RepoRoot.Find()!, "src", "eQuantic.UI.Code", "Intelligence", "CodeSignatureHelpSession.cs"))
            .Replace("\r\n", "\n");
        var sessions = Sessions(text, 60, 2026_10_19);

        var web = JsExecutor.Run(SessionProgram(runtime, text, sessions), timeoutMs: 300_000).Split('\n');

        var differences = new StringBuilder();
        var count = 0;
        var steps = 0;
        var open = 0;
        var walked = 0;
        for (var i = 0; i < sessions.Count; i++)
        {
            var dotnet = RunOnDotNet(text, sessions[i].Caret, sessions[i].Steps);
            steps += dotnet.Count;
            open += dotnet.Count(state => state.StartsWith("open|", StringComparison.Ordinal));
            walked += dotnet.Count(state => state.StartsWith("open|2|", StringComparison.Ordinal));
            var twin = i < web.Length && web[i].StartsWith('=')
                ? JsonSerializer.Deserialize<List<string>>(web[i].TrimEnd('\r')[1..])!
                : [];
            var at = Enumerable.Range(0, dotnet.Count).FirstOrDefault(step => step >= twin.Count || dotnet[step] != twin[step], -1);
            if (at < 0) continue;
            if (count++ < 3)
            {
                var keys = string.Join(" ", sessions[i].Steps.Take(at + 1).Select(step => step.Text is { } t ? JsonSerializer.Serialize(t) : $"[{step.Key}+{step.Modifiers}]"));
                differences.Append($"\n  session {i}, step {at} after {keys}:\n    .NET    {dotnet[at]}\n    the web {(at < twin.Count ? twin[at] : "(none)")}");
            }
        }

        steps.Should().BeGreaterThan(600, "the sessions are what is compared");
        open.Should().BeGreaterThan(steps / 6, "a panel shows for a good part of the steps, or there is little to compare");
        walked.Should().BeGreaterThan(5, "the arrows walk to the third overload often enough to compare");
        count.Should().Be(0, $"the signature help behaves the same on both sides:{differences}");
    }
}
