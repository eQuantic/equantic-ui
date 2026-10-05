using System.Text;
using System.Text.Json;
using eQuantic.UI.Code;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The completion of the code engine (<see cref="CodeCompletion"/>) answers the same on both sides:
/// the C# engine, and its twin in the bundle the Server serves, run by the SDK's embedded Bun. A page
/// draws the list the web filters and an IDE on Photon the list .NET filters, so two rankings of one
/// word would be two editors.
/// <para>
/// Compared two ways. The filter (<see cref="CodeFuzzyMatch"/>) over every label of a real language
/// service's answer, Roslyn's recorded one, against patterns cut from those labels. And whole
/// sessions: an editor over a real source file with the language's words and the document's as its
/// providers, driven by seeded random keystrokes (a word started, its letters, the list's keys, a
/// dot, a space, ⌃Space), its state read after every one: whether the list shows, what it shows
/// first, what is selected, and the line and the caret it leaves. On the web every answer arrives on
/// a later turn of the event loop, so each step there waits for one.
/// </para>
/// </summary>
public class CodeCompletionTwinTests
{
    // ---- the filter --------------------------------------------------------------------------------

    private static string Describe(CodeFuzzyMatch? match) =>
        match is null ? "-" : $"{match.Score}:{string.Join(",", match.Positions)}";

    [SkippableFact]
    public void TheFilter_ScoresAndMarksAsDotNetDoes_OverARealAnswer()
    {
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");

        var labels = RecordedLabels();
        var random = new Random(2026_10_05);
        var cases = new List<(string Pattern, string Word, bool Anywhere)>();
        for (var i = 0; i < 6000; i++)
        {
            var word = labels[random.Next(labels.Count)];
            var source = labels[random.Next(labels.Count)];
            // A pattern cut from a label (its start, a scatter of its letters, either case), or noise.
            var pattern = random.Next(4) switch
            {
                0 => source[..Math.Min(source.Length, random.Next(1, 5))],
                1 => new string(source.Where((_, at) => random.Next(3) == 0).Take(4).ToArray()),
                2 => source[..Math.Min(source.Length, random.Next(1, 4))].ToLowerInvariant(),
                _ => new string(Enumerable.Range(0, random.Next(1, 4)).Select(_ => (char)random.Next('a', 'z' + 1)).ToArray()),
            };
            cases.Add((pattern, word, random.Next(5) == 0));
        }

        var program = $$"""
            import { CodeFuzzyMatch } from '{{runtime}}';
            const cases = {{JsonSerializer.Serialize(cases.Select(c => new object[] { c.Pattern, c.Word, c.Anywhere }))}};
            const out = [];
            for (const [pattern, word, anywhere] of cases) {
              const match = CodeFuzzyMatch.of(pattern, word, anywhere);
              out.push(match == null ? '-' : `${match.score}:${match.positions.join(',')}`);
            }
            console.log('=' + out.join('\n='));
            """;
        var web = JsExecutor.Run(program, timeoutMs: 120_000).Split('\n');

        var differences = new StringBuilder();
        var count = 0;
        for (var i = 0; i < cases.Count; i++)
        {
            var dotnet = Describe(CodeFuzzyMatch.Of(cases[i].Pattern, cases[i].Word, cases[i].Anywhere));
            var twin = i < web.Length && web[i].StartsWith('=') ? web[i].TrimEnd('\r')[1..] : "(no answer)";
            if (dotnet == twin) continue;
            if (count++ < 5)
                differences.Append($"\n  {cases[i].Pattern} over {cases[i].Word}:\n    .NET    {dotnet}\n    the web {twin}");
        }

        count.Should().Be(0, $"the filter scores the same on both sides:{differences}");
    }

    private static List<string> RecordedLabels()
    {
        var path = Path.Combine(RepoRoot.Find()!, "tests", "eQuantic.UI.Native.Engine.Tests", "Fixtures",
            "roslyn-completions.fixture.json");
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        return json.RootElement.GetProperty("answers").EnumerateArray()
            .SelectMany(answer => answer.GetProperty("items").EnumerateArray())
            .Select(item => item.GetProperty("label").GetString()!)
            .Distinct()
            .ToList();
    }

    // ---- whole sessions ----------------------------------------------------------------------------

    /// <summary>One keystroke: text typed, or a key with its modifiers.</summary>
    private sealed record Step(string? Text, string? Key, int Modifiers);

    /// <summary>What the list and the editor show after a step, as one line.</summary>
    private static string State(CodeEditorController editor)
    {
        var completion = editor.Completion;
        var first = string.Join(",", completion.Items.Take(6).Select(match => match.Item.Label));
        var line = editor.Document.Line(editor.Caret.Line);
        return $"{(completion.IsOpen ? "open" : "shut")}|{completion.Selected}|{first}|{editor.Caret.Line}:{editor.Caret.Column}|{line}";
    }

    private static CodeEditorController Editor(string text, CodePosition caret)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.Completion.Providers.Add(new CodeKeywordCompletionProvider());
        editor.Completion.Providers.Add(new CodeWordCompletionProvider());
        editor.Selection = new CodeRange(caret, caret);
        return editor;
    }

    private static List<string> RunOnDotNet(string text, CodePosition caret, IReadOnlyList<Step> steps)
    {
        var editor = Editor(text, caret);
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
    /// Seeded sessions over <paramref name="text"/>: from the end of a random line, a new line, then
    /// rounds of a word started from the document's own words, its letters typed one at a time, and
    /// what a person does with a list (walk it, take it, leave it, go on typing past it).
    /// </summary>
    private static List<(CodePosition Caret, List<Step> Steps)> Sessions(string text, int count, int seed)
    {
        var random = new Random(seed);
        var document = CodeDocument.FromText(text);
        var words = text.Split(text.Where(c => !CodeDocument.IsWordChar(c)).Distinct().ToArray(),
                StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length >= 3 && !char.IsDigit(word[0]))
            .Distinct()
            .OrderBy(word => word, StringComparer.Ordinal)
            .ToList();
        var sessions = new List<(CodePosition, List<Step>)>();
        for (var s = 0; s < count; s++)
        {
            var line = random.Next(document.LineCount);
            var caret = new CodePosition(line, document.Line(line).Length);
            var steps = new List<Step> { new(null, "Enter", 0) };
            for (var round = random.Next(1, 4); round > 0; round--)
            {
                var word = words[random.Next(words.Count)];
                var typed = random.Next(1, Math.Min(word.Length, 5));
                foreach (var c in word[..typed]) steps.Add(new Step(c.ToString(), null, 0));
                for (var walk = random.Next(0, 3); walk > 0; walk--)
                    steps.Add(new Step(null, random.Next(3) switch { 0 => "ArrowUp", 1 => "PageDown", _ => "ArrowDown" }, 0));
                steps.Add(random.Next(9) switch
                {
                    0 => new Step(null, "Enter", 0),
                    1 => new Step(null, "Tab", 0),
                    2 => new Step(null, "Escape", 0),
                    3 => new Step(null, "Backspace", 0),
                    4 => new Step(".", null, 0),
                    5 => new Step(" ", null, 0),
                    6 => new Step(null, " ", (int)KeyModifiers.Command),
                    7 => new Step(word[typed].ToString(), null, 0),
                    _ => new Step("(", null, 0),
                });
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
            import { CodeEditorController, CodeLanguages, CodeKeywordCompletionProvider, CodeWordCompletionProvider,
              CodeRange, CodePosition } from '{{runtime}}';
            const text = {{JsonSerializer.Serialize(text)}};
            const sessions = {{encoded}};
            // An answer arrives on a later turn on the web: every step waits for one.
            const settle = () => new Promise((resolve) => setTimeout(resolve, 0));
            const state = (editor) => {
              const completion = editor.completion;
              const first = completion.items.slice(0, 6).map((match) => match.item.label).join(',');
              const line = editor.document.line(editor.caret.line);
              return `${completion.isOpen ? 'open' : 'shut'}|${completion.selected}|${first}|${editor.caret.line}:${editor.caret.column}|${line}`;
            };
            for (const session of sessions) {
              const editor = new CodeEditorController(text, CodeLanguages.cSharp);
              editor.completion.providers.push(new CodeKeywordCompletionProvider());
              editor.completion.providers.push(new CodeWordCompletionProvider());
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
    public void Sessions_OverARealFile_ShowTheSameListAfterEveryKey()
    {
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        var text = File.ReadAllText(Path.Combine(RepoRoot.Find()!, "src", "eQuantic.UI.Code", "Intelligence", "CodeCompletion.cs"))
            .Replace("\r\n", "\n");
        var sessions = Sessions(text, 60, 2026_10_05);

        var web = JsExecutor.Run(SessionProgram(runtime, text, sessions), timeoutMs: 300_000).Split('\n');

        var differences = new StringBuilder();
        var count = 0;
        var steps = 0;
        var open = 0;
        for (var i = 0; i < sessions.Count; i++)
        {
            var dotnet = RunOnDotNet(text, sessions[i].Caret, sessions[i].Steps);
            steps += dotnet.Count;
            open += dotnet.Count(state => state.StartsWith("open|", StringComparison.Ordinal));
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

        steps.Should().BeGreaterThan(300, "the sessions are what is compared");
        open.Should().BeGreaterThan(steps / 3, "a list shows for most of the steps, or there is little to compare");
        count.Should().Be(0, $"the completion behaves the same on both sides:{differences}");
    }
}
