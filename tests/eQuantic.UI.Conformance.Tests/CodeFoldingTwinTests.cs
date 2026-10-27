using System.Text;
using System.Text.Json;
using eQuantic.UI.Code;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The code engine's folding (#1119) folds the same regions on both sides: the C# engine, and its twin in the
/// bundle the Server serves, run by the SDK's embedded Bun. A page folds through the twin and an IDE on Photon
/// through .NET, so a region joined in another order, a fold carried to another line by an edit or a caret left on
/// a hidden line would be two editors.
/// <para>
/// Compared over whole sessions over a real source file, whose regions are what Roslyn's syntax tree braces in it
/// (every type, member, accessor list, initializer and block, from the line its header ends on to the line of its
/// closing brace), as a language service answers them, some sessions with a second provider answering regions that
/// share a start line, cross another or fall outside the document, and some with none, so the indentation folds.
/// Seeded steps press the seven keys in the session's convention and the arrows, press a chevron, put the caret
/// anywhere, type, delete lines, join a line to the next and undo, read after every one: the caret, every region
/// and whether it is folded, what is collapsed, and the rows that are left.
/// </para>
/// </summary>
public class CodeFoldingTwinTests
{
    /// <summary>One step: an operation and its arguments, as the program on the web reads them.</summary>
    private sealed record Step(string Op, string? Text, int A, int B);

    /// <summary>A session: its keyboard, where the caret starts, which providers answer (0 Roslyn's, 1 Roslyn's and a
    /// noisy one, 2 none), and the noisy one's regions.</summary>
    private sealed record Session(bool Apple, int Line, int Providers, List<CodeFold> Noise, List<Step> Steps);

    private const int Shift = (int)KeyModifiers.Shift;
    private const int Command = (int)KeyModifiers.Command;
    private const int Alt = (int)KeyModifiers.Alt;

    // ---- Roslyn's regions -------------------------------------------------------------------------------

    /// <summary>What the syntax tree of <paramref name="text"/> braces, a region from the line its header ends on to
    /// the line of its closing brace, the ones on a single line left out.</summary>
    private static List<CodeFold> Braced(string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text);
        var lines = tree.GetText().Lines;
        var regions = new List<CodeFold>();
        foreach (var node in tree.GetRoot().DescendantNodes())
        {
            var (open, close) = node switch
            {
                BlockSyntax block => (block.OpenBraceToken, block.CloseBraceToken),
                BaseTypeDeclarationSyntax type => (type.OpenBraceToken, type.CloseBraceToken),
                NamespaceDeclarationSyntax space => (space.OpenBraceToken, space.CloseBraceToken),
                AccessorListSyntax accessors => (accessors.OpenBraceToken, accessors.CloseBraceToken),
                InitializerExpressionSyntax initializer => (initializer.OpenBraceToken, initializer.CloseBraceToken),
                SwitchStatementSyntax choice => (choice.OpenBraceToken, choice.CloseBraceToken),
                _ => (default, default),
            };
            if (open.IsKind(SyntaxKind.None) || close.IsMissing) continue;
            var header = open.GetPreviousToken();
            var start = lines.GetLinePosition((header.IsKind(SyntaxKind.None) ? open : header).Span.End).Line;
            var end = lines.GetLinePosition(close.SpanStart).Line;
            if (end > start) regions.Add(new CodeFold(start, end));
        }
        return regions;
    }

    private sealed class Answering(IReadOnlyList<CodeFold> regions) : ICodeFoldProvider
    {
        public Task<IReadOnlyList<CodeFold>> FoldsAsync(CodeDocument document, CancellationToken cancellation) =>
            Task.FromResult(regions);
    }

    // ---- the state after a step -------------------------------------------------------------------------

    private static string Describe(CodeRange range) =>
        $"{range.Anchor.Line}:{range.Anchor.Column}>{range.Focus.Line}:{range.Focus.Column}";

    private static string State(CodeEditorController editor, bool claimed)
    {
        var folding = editor.Folding;
        var regions = string.Join(",", folding.Regions.Select(region =>
            $"{region.StartLine}-{region.EndLine}{(folding.IsFolded(region.StartLine) ? "*" : "")}"));
        var collapses = string.Join(",", folding.Collapses.Select(collapse => $"{collapse.FirstLine}-{collapse.LastLine}"));
        var rows = editor.Grid.Rows?.RowCount ?? -1;
        return $"{(claimed ? 1 : 0)}|{string.Join(";", editor.Selections.Select(Describe))}|{editor.Document.LineCount}"
            + $"|{regions}|{collapses}|{rows}";
    }

    private static bool Run(CodeEditorController editor, Step step, KeyboardConvention convention) => step.Op switch
    {
        "key" => editor.HandleKey(step.Text!, (KeyModifiers)step.A, convention, null),
        "toggle" => editor.Folding.Toggle(step.A),
        "caret" => Caret(editor, step.A, step.B),
        "type" => editor.HandleText(step.Text!),
        "delete" => editor.Apply(new CodeRange(new CodePosition(step.A, 0), new CodePosition(step.B, 0)), ""),
        "join" => Join(editor, step.A, convention),
        _ => throw new InvalidOperationException(step.Op),
    };

    private static bool Caret(CodeEditorController editor, int line, int column)
    {
        editor.Selection = new CodeRange(new CodePosition(line, column));
        return true;
    }

    private static bool Join(CodeEditorController editor, int line, KeyboardConvention convention)
    {
        editor.Selection = new CodeRange(new CodePosition(line, editor.Document.Line(line).Length));
        return editor.HandleKey("Delete", KeyModifiers.None, convention, null);
    }

    private static List<string> RunOnDotNet(string text, List<CodeFold> braced, Session session)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.Folding.Delay = TimeSpan.Zero;
        if (session.Providers < 2) editor.Folding.Providers.Add(new Answering(braced));
        if (session.Providers == 1) editor.Folding.Providers.Add(new Answering(session.Noise));
        editor.Folding.Check();
        editor.Selection = new CodeRange(new CodePosition(session.Line, 0));
        var convention = session.Apple ? KeyboardConvention.Apple : KeyboardConvention.Standard;
        return session.Steps.Select(step => State(editor, Run(editor, step, convention))).ToList();
    }

    // ---- the sessions -----------------------------------------------------------------------------------

    /// <summary>A folding key of the session's convention: the seven commands, a bracket typed with ⌥ on a Mac or
    /// with ⇧ elsewhere, and the arrows that step over what is folded.</summary>
    private static Step Key(Random random, bool apple)
    {
        string[] chords = ["[", "]", "l", "0", "j"];
        return random.Next(12) switch
        {
            < 2 => new Step("key", apple ? "[" : random.Next(2) == 0 ? "[" : "{", apple ? Alt | Command : Shift | Command, 0),
            < 4 => new Step("key", apple ? "]" : random.Next(2) == 0 ? "]" : "}", apple ? Alt | Command : Shift | Command, 0),
            < 5 => new Step("key", apple ? (random.Next(2) == 0 ? "“" : "‘") : "[", apple ? Alt | Command : Shift | Command, 0),
            < 6 => new Step("key", "k", Command, 0),
            < 9 => new Step("key", chords[random.Next(chords.Length)], Command, 0),
            < 11 => new Step("key", random.Next(2) == 0 ? "ArrowDown" : "ArrowUp", random.Next(4) == 0 ? Shift : 0, 0),
            _ => new Step("key", "z", Command, 0),
        };
    }

    private static List<Session> Sessions(int lineCount, int count, int seed)
    {
        var random = new Random(seed);
        var sessions = new List<Session>();
        for (var s = 0; s < count; s++)
        {
            var apple = random.Next(2) == 0;
            var noise = new List<CodeFold>();
            for (var n = random.Next(4, 12); n > 0; n--)
            {
                var start = random.Next(-2, lineCount);
                noise.Add(new CodeFold(start, start + random.Next(-1, 60)) { Placeholder = random.Next(3) == 0 ? "{ … }" : null });
            }
            var steps = new List<Step>();
            for (var step = random.Next(20, 40); step > 0; step--)
            {
                var line = random.Next(lineCount);
                steps.Add(random.Next(40) switch
                {
                    < 14 => Key(random, apple),
                    < 20 => new Step("toggle", null, line, 0),
                    < 26 => new Step("caret", null, line, random.Next(6)),
                    < 30 => new Step("type", random.Next(3) == 0 ? "\n" : "x", 0, 0),
                    < 32 => new Step("key", "Backspace", 0, 0),
                    < 35 => new Step("delete", null, line, Math.Min(lineCount - 1, line + random.Next(1, 6))),
                    _ => new Step("join", null, Math.Min(line, lineCount - 2), 0),
                });
            }
            // A third of the sessions start with every region folded, so what is hidden is walked from the start.
            if (random.Next(3) == 0) steps.InsertRange(0, [new Step("key", "k", Command, 0), new Step("key", "0", Command, 0)]);
            sessions.Add(new Session(apple, random.Next(lineCount), random.Next(3), noise, steps));
        }
        return sessions;
    }

    private static string SessionProgram(string runtime, string text, List<CodeFold> braced, IReadOnlyList<Session> sessions)
    {
        var encoded = JsonSerializer.Serialize(sessions.Select(session => new
        {
            apple = session.Apple,
            line = session.Line,
            providers = session.Providers,
            noise = session.Noise.Select(fold => new object?[] { fold.StartLine, fold.EndLine, fold.Placeholder }),
            steps = session.Steps.Select(step => new object?[] { step.Op, step.Text, step.A, step.B }),
        }));
        return $$"""
            import { $eq, CodeEditorController, CodeLanguages, CodeRange, CodePosition, CodeFold } from '{{runtime}}';
            const text = {{JsonSerializer.Serialize(text)}};
            const braced = {{JsonSerializer.Serialize(braced.Select(fold => new[] { fold.StartLine, fold.EndLine }))}}
              .map(([start, end]) => new CodeFold(start, end));
            const sessions = {{encoded}};
            const answering = (regions) => ({ foldsAsync: async () => regions });
            // An answer arrives on a later turn on the web: every step waits for one.
            const settle = () => new Promise((resolve) => setTimeout(resolve, 0));
            const describe = (r) => `${r.anchor.line}:${r.anchor.column}>${r.focus.line}:${r.focus.column}`;
            const state = (editor, claimed) => {
              const folding = editor.folding;
              const regions = folding.regions
                .map((region) => `${region.startLine}-${region.endLine}${folding.isFolded(region.startLine) ? '*' : ''}`).join(',');
              const collapses = folding.collapses.map((collapse) => `${collapse.firstLine}-${collapse.lastLine}`).join(',');
              const rows = editor.grid.rows?.rowCount ?? -1;
              return `${claimed ? 1 : 0}|${editor.selections.map(describe).join(';')}|${editor.document.lineCount}`
                + `|${regions}|${collapses}|${rows}`;
            };
            for (const session of sessions) {
              const editor = new CodeEditorController(text, CodeLanguages.cSharp);
              editor.folding.delay = $eq.time.timeSpan.zero;
              if (session.providers < 2) editor.folding.providers.push(answering(braced));
              if (session.providers === 1) {
                editor.folding.providers.push(answering(session.noise.map(([start, end, placeholder]) => {
                  const fold = new CodeFold(start, end);
                  fold.placeholder = placeholder;
                  return fold;
                })));
              }
              editor.folding.check();
              await settle();
              editor.selection = new CodeRange(new CodePosition(session.line, 0), new CodePosition(session.line, 0));
              const convention = session.apple ? 'apple' : 'standard';
              const states = [];
              for (const [op, typed, a, b] of session.steps) {
                let claimed;
                switch (op) {
                  case 'key': claimed = editor.handleKey(typed, a, convention, null); break;
                  case 'toggle': claimed = editor.folding.toggle(a); break;
                  case 'caret': editor.selection = new CodeRange(new CodePosition(a, b), new CodePosition(a, b)); claimed = true; break;
                  case 'type': claimed = editor.handleText(typed); break;
                  case 'delete': claimed = editor.apply(new CodeRange(new CodePosition(a, 0), new CodePosition(b, 0)), ''); break;
                  case 'join': {
                    const end = new CodePosition(a, editor.document.line(a).length);
                    editor.selection = new CodeRange(end, end);
                    claimed = editor.handleKey('Delete', 0, convention, null);
                    break;
                  }
                }
                await settle();
                states.push(state(editor, claimed));
              }
              console.log('=' + JSON.stringify(states));
            }
            """;
    }

    [SkippableFact]
    public void Sessions_OverARealFile_FoldTheSameRegionsAndHideTheSameLines()
    {
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        var text = File.ReadAllText(Path.Combine(RepoRoot.Find()!, "src", "eQuantic.UI.Code", "Editing", "CodeEditorController.cs"))
            .Replace("\r\n", "\n");
        var braced = Braced(text);
        var lineCount = CodeDocument.FromText(text).LineCount;
        var sessions = Sessions(lineCount, 60, 2026_10_27);

        var web = JsExecutor.Run(SessionProgram(runtime, text, braced, sessions), timeoutMs: 300_000)
            .Split('\n').Where(line => line.StartsWith('=')).ToList();

        var differences = new StringBuilder();
        var count = 0;
        var steps = 0;
        var hiding = 0;
        for (var i = 0; i < sessions.Count; i++)
        {
            var dotnet = RunOnDotNet(text, braced, sessions[i]);
            steps += dotnet.Count;
            hiding += dotnet.Count(state => state.Split('|')[4].Length > 0);
            var twin = i < web.Count ? JsonSerializer.Deserialize<List<string>>(web[i].TrimEnd('\r')[1..])! : [];
            var at = Enumerable.Range(0, dotnet.Count)
                .FirstOrDefault(step => step >= twin.Count || dotnet[step] != twin[step], -1);
            if (at < 0) continue;
            if (count++ < 3)
            {
                var done = string.Join(" ", sessions[i].Steps.Take(at + 1).Select(step => $"[{step.Op} {step.Text} {step.A} {step.B}]"));
                differences.Append($"\n  session {i} ({(sessions[i].Apple ? "apple" : "standard")}, providers {sessions[i].Providers}),"
                    + $" step {at} after {done}:\n    .NET    {dotnet[at]}\n    the web {(at < twin.Count ? twin[at] : "(none)")}");
            }
        }

        braced.Should().HaveCountGreaterThan(100, "the file braces enough regions to fold");
        steps.Should().BeGreaterThan(1500, "the sessions are what is compared");
        hiding.Should().BeGreaterThan(300, "something is folded often enough to compare");
        count.Should().Be(0, $"folding behaves the same on both sides:{differences}");
    }
}
