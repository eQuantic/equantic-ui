using System.Text;
using System.Text.Json;
using eQuantic.UI.Code;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The line operations of the code engine (<see cref="CodeEditorController.MoveLines"/> and its siblings)
/// leave the same text and the same selection on both sides: the C# engine, and its twin in the bundle the
/// Server serves, run by the SDK's embedded Bun. A page edits through the twin and an IDE on Photon through
/// .NET, so a sort that orders two lines another way, or a join that leaves one space more, would be two
/// editors (#1011).
/// <para>
/// Compared two ways. The sorts over lines whose order only Unicode's case knows, where a translation to
/// JavaScript's own comparison would part from .NET's. And whole sessions: an editor over a real source
/// file, driven in one of the two keyboard conventions by seeded keystrokes (the selection grown over
/// lines, the caret moved, text typed with the white space .NET and JavaScript disagree on among it, every
/// line key, the two sorts no key runs, undo and redo), read after every one: whether it was claimed, the
/// selection, the line count, a hash of the whole text and the caret's line.
/// </para>
/// </summary>
public class CodeLinesTwinTests
{
    // ---- the sorts ---------------------------------------------------------------------------------

    /// <summary>
    /// Lines whose order is decided by case: accented letters and their capitals, the sharp s beside the
    /// two letters it upper-cases to, the titlecase digraphs, the dotless and the dotted i, the three
    /// sigmas, a ligature, the signs that are letters' capitals (Kelvin, Angstrom), a fullwidth pair, a
    /// letter outside the Basic Multilingual Plane with its small form, a combining accent beside the
    /// precomposed one, and the empty and the blank lines that sort first.
    /// </summary>
    private static readonly string[] CasedLines =
    [
        "e", "E", "\u00E9", "\u00C9", "ss", "SS", "\u00DF", "\u1E9E", "\u01C4", "\u01C5", "\u01C6",
        "i", "I", "\u0131", "\u0130", "\u03A3", "\u03C3", "\u03C2", "ff", "FF", "\uFB00", "k", "K",
        "\u212A", "\u00E5", "\u00C5", "\u212B", "\u00B5", "\u039C", "\u03BC", "\uFF41", "\uFF21",
        "\uD801\uDC00", "\uD801\uDC28", "a\u0301", "\u00E1", "A", "a", "", " ", "\t", "Abc", "abC", "ABC",
    ];

    private static List<string> SortOnDotNet(string text)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.Execute(CodeCommand.SelectAll);
        editor.Execute(CodeCommand.SortLinesAscending);
        var ascending = editor.Document.Text;
        editor.Execute(CodeCommand.SortLinesDescending);
        return [ascending, editor.Document.Text];
    }

    [SkippableFact]
    public void TheSorts_OrderAsDotNetDoes_LinesWhoseOrderOnlyCaseDecides()
    {
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        var random = new Random(2026_10_24);
        // The same lines in several orders: a comparison that is not one order would sort them differently.
        var texts = Enumerable.Range(0, 8).Select(_ => string.Join("\n", CasedLines.OrderBy(_ => random.Next()))).ToList();

        var program = $$"""
            import { CodeEditorController, CodeLanguages } from '{{runtime}}';
            const texts = {{JsonSerializer.Serialize(texts)}};
            for (const text of texts) {
              const editor = new CodeEditorController(text, CodeLanguages.cSharp);
              editor.execute('selectAll');
              editor.execute('sortLinesAscending');
              const ascending = editor.document.text;
              editor.execute('sortLinesDescending');
              console.log('=' + JSON.stringify([ascending, editor.document.text]));
            }
            """;
        var web = JsExecutor.Run(program, timeoutMs: 120_000).Split('\n').Where(line => line.StartsWith('=')).ToList();

        web.Should().HaveCount(texts.Count, "every text is sorted on the web");
        for (var i = 0; i < texts.Count; i++)
        {
            var dotnet = SortOnDotNet(texts[i]);
            var twin = JsonSerializer.Deserialize<List<string>>(web[i].TrimEnd('\r')[1..])!;
            twin.Should().Equal(dotnet, $"text {i} sorts the same on both sides");
        }
        // The order is case's, the code units' only between lines case alone tells apart: ordinally every
        // capital would come before every small letter.
        SortOnDotNet(string.Join("\n", CasedLines))[0].Split('\n').Should().ContainInOrder("A", "a", "ABC", "Abc", "abC", "E", "e");
    }

    // ---- the joins ---------------------------------------------------------------------------------

    /// <summary>
    /// The characters a join must tell apart: the space and the tab it takes, and the white space it leaves
    /// alone, on which .NET and JavaScript do not agree. .NET's <see cref="char.IsWhiteSpace(char)"/> takes
    /// the next line (U+0085), which a JavaScript trim does not; a JavaScript trim takes the byte order mark
    /// (U+FEFF), which .NET does not; both take the no-break, the wide and the ideographic spaces, the line
    /// separator, the vertical tab and the form feed; and the zero-width space and the Mongolian vowel
    /// separator are white space to neither.
    /// </summary>
    private static readonly string[] Blanks =
    [
        " ", "\t", "\u00A0", "\u2003", "\u3000", "\u0085", "\uFEFF", "\u180E", "\u200B", "\u2028", "\u000B", "\u000C",
    ];

    private static List<string> JoinOnDotNet(string text)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.Execute(CodeCommand.JoinLines);
        var caret = editor.Document.Text + "|" + editor.Caret.Line + ":" + editor.Caret.Column;
        editor.Undo();
        editor.Execute(CodeCommand.SelectAll);
        editor.Execute(CodeCommand.JoinLines);
        return [caret, editor.Document.Text];
    }

    [SkippableFact]
    public void TheJoins_TakeSpacesAndTabsAlone_OnBothSides()
    {
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        // Each blank at the end of a line, at the start of the next, alone on a line, and beside a space.
        var texts = Blanks.Select(blank => string.Join("\n",
            $"a{blank}", $"{blank}b", blank, $"c {blank}", $"{blank} d", $"e{blank} ", $"  {blank}f")).ToList();

        var program = $$"""
            import { CodeEditorController, CodeLanguages } from '{{runtime}}';
            const texts = {{JsonSerializer.Serialize(texts)}};
            for (const text of texts) {
              const editor = new CodeEditorController(text, CodeLanguages.cSharp);
              editor.execute('joinLines');
              const caret = editor.document.text + '|' + editor.caret.line + ':' + editor.caret.column;
              editor.undo();
              editor.execute('selectAll');
              editor.execute('joinLines');
              console.log('=' + JSON.stringify([caret, editor.document.text]));
            }
            """;
        var web = JsExecutor.Run(program, timeoutMs: 120_000).Split('\n').Where(line => line.StartsWith('=')).ToList();

        web.Should().HaveCount(texts.Count, "every text is joined on the web");
        for (var i = 0; i < texts.Count; i++)
        {
            var twin = JsonSerializer.Deserialize<List<string>>(web[i].TrimEnd('\r')[1..])!;
            twin.Should().Equal(JoinOnDotNet(texts[i]), $"the text around U+{(int)Blanks[i][0]:X4} joins the same on both sides");
        }
        // A no-break space is text to a join, as to VS Code's: it stays on both sides of the break.
        JoinOnDotNet("a\u00A0\n\u00A0b")[1].Should().Be("a\u00A0 \u00A0b");
    }

    // ---- the sessions ------------------------------------------------------------------------------

    /// <summary>One step: text typed, a key with its modifiers, or a command run by name, which is how the
    /// sorts run: no key does.</summary>
    private sealed record Step(string? Text, string? Key, int Modifiers, string? Command = null);

    private sealed record Session(bool Apple, CodePosition Caret, List<Step> Steps);

    private const int Shift = (int)KeyModifiers.Shift;
    private const int Alt = (int)KeyModifiers.Alt;
    private const int Command = (int)KeyModifiers.Command;
    private const int Control = (int)KeyModifiers.Control;

    /// <summary>
    /// What a session types: the spaces and tabs a join collapses, the white space it leaves alone, among it
    /// what only one of the two calls white space (the next line, U+0085, is to .NET; the byte order mark,
    /// U+FEFF, is to JavaScript), letters whose case a sort reads, and the braces a new line indents after.
    /// </summary>
    private static readonly string[] Typed =
    [
        " ", "   ", "\t", "\u00A0", "\u2003", "\u0085", "\uFEFF", "x", "\u00C9a", "\u00E9A", "\u00DF", "{", "}",
    ];

    /// <summary>The steps the coverage counts, by the command each one is there to run.</summary>
    private static readonly Dictionary<(string Key, int Modifiers), string> LineKeys = new()
    {
        [("ArrowUp", Alt)] = "MoveLinesUp",
        [("ArrowDown", Alt)] = "MoveLinesDown",
        [("ArrowUp", Shift | Alt)] = "CopyLinesUp",
        [("ArrowDown", Shift | Alt)] = "CopyLinesDown",
        [("k", Shift | Command)] = "DeleteLines",
        [("j", Control)] = "JoinLines",
        [("j", Command)] = "JoinLines",
        [("Enter", Command)] = "InsertLineBelow",
        [("Enter", Shift | Command)] = "InsertLineAbove",
    };

    private static string? LineCommandOf(Step step) =>
        step.Command ?? (step.Key is { } key && LineKeys.TryGetValue((key, step.Modifiers), out var command) ? command : null);

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
        var selection = editor.Selection;
        var document = editor.Document;
        return $"{(claimed ? 1 : 0)}|{selection.Anchor.Line}:{selection.Anchor.Column}|{selection.Focus.Line}:{selection.Focus.Column}"
            + $"|{document.LineCount}|{Hash(document.Text)}|{document.Line(editor.Caret.Line)}";
    }

    private static List<string> RunOnDotNet(string text, Session session)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.Selection = new CodeRange(session.Caret);
        var convention = session.Apple ? KeyboardConvention.Apple : KeyboardConvention.Standard;
        var states = new List<string>();
        foreach (var step in session.Steps)
        {
            var claimed = step.Text is { } typed ? editor.HandleText(typed)
                : step.Command is { } command ? editor.Execute(Enum.Parse<CodeCommand>(command))
                : editor.HandleKey(step.Key!, (KeyModifiers)step.Modifiers, convention, null);
            states.Add(State(editor, claimed));
        }
        return states;
    }

    /// <summary>
    /// Seeded sessions over <paramref name="text"/>: from a random place, rounds of a selection grown or a
    /// caret moved, a line key or a sort, text, and undo and redo, weighted towards selections over several
    /// lines, which are what the operations take.
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
            for (var step = random.Next(12, 32); step > 0; step--)
            {
                steps.Add(random.Next(42) switch
                {
                    < 6 => new Step(null, "ArrowDown", Shift),
                    < 8 => new Step(null, "ArrowUp", Shift),
                    < 10 => new Step(null, "ArrowDown", 0),
                    < 12 => new Step(null, "ArrowUp", 0),
                    < 13 => new Step(null, "ArrowRight", Shift),
                    < 14 => new Step(null, "Home", 0),
                    < 15 => new Step(null, "End", 0),
                    < 17 => new Step(null, "ArrowUp", Alt),
                    < 19 => new Step(null, "ArrowDown", Alt),
                    < 20 => new Step(null, "ArrowUp", Shift | Alt),
                    < 21 => new Step(null, "ArrowDown", Shift | Alt),
                    < 22 => new Step(null, "k", Shift | Command),
                    < 24 => new Step(null, "j", random.Next(2) == 0 ? Control : Command),
                    < 25 => new Step(null, "Enter", Command),
                    < 26 => new Step(null, "Enter", Shift | Command),
                    < 28 => new Step(null, null, 0, "SortLinesAscending"),
                    < 29 => new Step(null, null, 0, "SortLinesDescending"),
                    < 33 => new Step(Typed[random.Next(Typed.Length)], null, 0),
                    < 34 => new Step(null, "Enter", 0),
                    < 35 => new Step(null, "Tab", 0),
                    < 36 => new Step(null, "Backspace", 0),
                    < 39 => new Step(null, "z", Command),
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
            steps = session.Steps.Select(step => new object?[]
            {
                step.Text, step.Key, step.Modifiers,
                step.Command is { } command ? char.ToLowerInvariant(command[0]) + command[1..] : null,
            }),
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
              const selection = editor.selection;
              const document = editor.document;
              return `${claimed ? 1 : 0}|${selection.anchor.line}:${selection.anchor.column}|${selection.focus.line}:${selection.focus.column}`
                + `|${document.lineCount}|${hash(document.text)}|${document.line(editor.caret.line)}`;
            };
            for (const session of sessions) {
              const editor = new CodeEditorController(text, CodeLanguages.cSharp);
              editor.selection = new CodeRange(new CodePosition(session.line, session.column));
              const convention = session.apple ? 'apple' : 'standard';
              const states = [];
              for (const [typed, key, modifiers, command] of session.steps) {
                const claimed = typed != null ? editor.handleText(typed)
                  : command != null ? editor.execute(command)
                  : editor.handleKey(key, modifiers, convention, null);
                states.push(state(editor, claimed));
              }
              console.log('=' + JSON.stringify(states));
            }
            """;
    }

    [SkippableFact]
    public void Sessions_OverARealFile_LeaveTheSameTextAndSelectionAfterEveryKey()
    {
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        var text = File.ReadAllText(Path.Combine(RepoRoot.Find()!, "src", "eQuantic.UI.Code", "Editing", "CodeKeymap.cs"))
            .Replace("\r\n", "\n");
        var sessions = Sessions(text, 80, 2026_10_24);

        var web = JsExecutor.Run(SessionProgram(runtime, text, sessions), timeoutMs: 300_000)
            .Split('\n').Where(line => line.StartsWith('=')).ToList();

        var differences = new StringBuilder();
        var count = 0;
        var steps = 0;
        var ran = new Dictionary<string, int>();
        for (var i = 0; i < sessions.Count; i++)
        {
            var dotnet = RunOnDotNet(text, sessions[i]);
            steps += dotnet.Count;
            for (var step = 0; step < dotnet.Count; step++)
            {
                if (dotnet[step][0] == '1' && LineCommandOf(sessions[i].Steps[step]) is { } command)
                    ran[command] = ran.GetValueOrDefault(command) + 1;
            }
            var twin = i < web.Count ? JsonSerializer.Deserialize<List<string>>(web[i].TrimEnd('\r')[1..])! : [];
            var at = Enumerable.Range(0, dotnet.Count).FirstOrDefault(step => step >= twin.Count || dotnet[step] != twin[step], -1);
            if (at < 0) continue;
            if (count++ < 3)
            {
                var keys = string.Join(" ", sessions[i].Steps.Take(at + 1).Select(step =>
                    step.Text is { } t ? JsonSerializer.Serialize(t) : step.Command ?? $"[{step.Key}+{step.Modifiers}]"));
                differences.Append($"\n  session {i} ({(sessions[i].Apple ? "apple" : "standard")}), step {at} after {keys}:"
                    + $"\n    .NET    {dotnet[at]}\n    the web {(at < twin.Count ? twin[at] : "(none)")}");
            }
        }

        steps.Should().BeGreaterThan(1200, "the sessions are what is compared");
        foreach (var command in LineKeys.Values.Distinct().Append("SortLinesAscending").Append("SortLinesDescending"))
            ran.GetValueOrDefault(command).Should().BeGreaterThan(5, $"{command} runs often enough to compare");
        count.Should().Be(0, $"the line operations behave the same on both sides:{differences}");
    }
}
