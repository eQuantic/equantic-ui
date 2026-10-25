using System.Globalization;
using System.Text;
using System.Text.Json;
using eQuantic.UI.Code;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The code engine's navigation (#1083) goes to the same places on both sides: the C# engine, and its twin in
/// the bundle the Server serves, run by the SDK's embedded Bun. A page goes to a line or a symbol through the
/// twin and an IDE on Photon through .NET, so a line read a column off, a symbol ranked in another order or a
/// jump list that kept another place would be two editors.
/// <para>
/// Compared over whole sessions over a real source file, whose symbols are what Roslyn's syntax tree declares
/// in it (the namespace, every type and every member, each ranged by its declaration and named where its name
/// is written), so the outline is a language service's in size and in shape: seeded steps open the field on a
/// line or on the symbols, type in it, walk its list, accept or close it, move the caret, type in the code,
/// and walk the jump list, read after every one: the caret, the field, what it lists and selects, what it
/// washes, the jump list and the scroll the editor asked of its view.
/// </para>
/// </summary>
public class CodeNavigationTwinTests
{
    /// <summary>One step: an operation and its arguments, as the program on the web reads them.</summary>
    private sealed record Step(string Op, string? Text, int A, int B);

    private sealed record Session(bool Apple, CodePosition Caret, List<Step> Steps);

    private const int Shift = (int)KeyModifiers.Shift;
    private const int Command = (int)KeyModifiers.Command;
    private const int Control = (int)KeyModifiers.Control;
    private const int Alt = (int)KeyModifiers.Alt;

    // ---- Roslyn's outline -------------------------------------------------------------------------------

    /// <summary>What the syntax tree of <paramref name="text"/> declares, as a language service's outline
    /// lists it.</summary>
    private static List<CodeSymbol> Outline(string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text);
        return Declared(tree.GetRoot().ChildNodes(), tree.GetText()).ToList();
    }

    private static IEnumerable<CodeSymbol> Declared(IEnumerable<SyntaxNode> nodes, SourceText text)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case BaseNamespaceDeclarationSyntax space:
                    yield return Symbol(space.Name.ToString(), CodeSymbolKind.Namespace, space, space.Name.Span, text,
                        Declared(space.Members, text));
                    break;
                case EnumDeclarationSyntax type:
                    yield return Symbol(type.Identifier.Text, CodeSymbolKind.Enum, type, type.Identifier.Span, text,
                        Declared(type.Members, text));
                    break;
                case TypeDeclarationSyntax type:
                    var kind = type switch
                    {
                        InterfaceDeclarationSyntax => CodeSymbolKind.Interface,
                        StructDeclarationSyntax => CodeSymbolKind.Struct,
                        RecordDeclarationSyntax record when record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) =>
                            CodeSymbolKind.Struct,
                        _ => CodeSymbolKind.Class,
                    };
                    yield return Symbol(type.Identifier.Text, kind, type, type.Identifier.Span, text, Declared(type.Members, text));
                    break;
                case EnumMemberDeclarationSyntax member:
                    yield return Symbol(member.Identifier.Text, CodeSymbolKind.EnumMember, member, member.Identifier.Span, text);
                    break;
                case MethodDeclarationSyntax method:
                    yield return Symbol(method.Identifier.Text, CodeSymbolKind.Method, method, method.Identifier.Span, text);
                    break;
                case ConstructorDeclarationSyntax constructor:
                    yield return Symbol(constructor.Identifier.Text, CodeSymbolKind.Constructor, constructor,
                        constructor.Identifier.Span, text);
                    break;
                case PropertyDeclarationSyntax property:
                    yield return Symbol(property.Identifier.Text, CodeSymbolKind.Property, property,
                        property.Identifier.Span, text);
                    break;
                case IndexerDeclarationSyntax indexer:
                    yield return Symbol("this[]", CodeSymbolKind.Property, indexer, indexer.ThisKeyword.Span, text);
                    break;
                case EventDeclarationSyntax happening:
                    yield return Symbol(happening.Identifier.Text, CodeSymbolKind.Event, happening,
                        happening.Identifier.Span, text);
                    break;
                case BaseFieldDeclarationSyntax field:
                    var constant = field.Modifiers.Any(SyntaxKind.ConstKeyword);
                    foreach (var variable in field.Declaration.Variables)
                        yield return Symbol(variable.Identifier.Text,
                            field is EventFieldDeclarationSyntax ? CodeSymbolKind.Event
                            : constant ? CodeSymbolKind.Constant : CodeSymbolKind.Field,
                            field, variable.Identifier.Span, text);
                    break;
                case OperatorDeclarationSyntax op:
                    yield return Symbol($"operator {op.OperatorToken.Text}", CodeSymbolKind.Operator, op,
                        op.OperatorToken.Span, text);
                    break;
            }
        }
    }

    private static CodeSymbol Symbol(string name, CodeSymbolKind kind, SyntaxNode node, TextSpan named, SourceText text,
        IEnumerable<CodeSymbol>? children = null) =>
        new(name, kind, RangeOf(node.Span, text), RangeOf(named, text))
        {
            Children = children?.ToList() ?? [],
            Deprecated = node is MemberDeclarationSyntax member && member.AttributeLists
                .SelectMany(list => list.Attributes).Any(attribute => attribute.Name.ToString() is "Obsolete" or "ObsoleteAttribute"),
        };

    private static CodeRange RangeOf(TextSpan span, SourceText text)
    {
        var start = text.Lines.GetLinePosition(span.Start);
        var end = text.Lines.GetLinePosition(span.End);
        return new CodeRange(new CodePosition(start.Line, start.Character), new CodePosition(end.Line, end.Character));
    }

    private sealed class Outlined(IReadOnlyList<CodeSymbol> symbols) : ICodeSymbolProvider
    {
        public Task<IReadOnlyList<CodeSymbol>> SymbolsAsync(CodeDocument document, CancellationToken cancellation) =>
            Task.FromResult(symbols);
    }

    /// <summary>The outline as the program on the web builds it again: each symbol's name, kind, ranges,
    /// whether it is deprecated and its children.</summary>
    private static object Encode(CodeSymbol symbol) => new
    {
        n = symbol.Name,
        k = char.ToLowerInvariant(symbol.Kind.ToString()[0]) + symbol.Kind.ToString()[1..],
        r = new[] { symbol.Range.Start.Line, symbol.Range.Start.Column, symbol.Range.End.Line, symbol.Range.End.Column },
        s = new[] { symbol.SelectionRange.Start.Line, symbol.SelectionRange.Start.Column, symbol.SelectionRange.End.Line, symbol.SelectionRange.End.Column },
        d = symbol.Deprecated,
        c = symbol.Children.Select(Encode).ToList(),
    };

    // ---- the state after a step -------------------------------------------------------------------------

    private static string Describe(CodeRange range) =>
        $"{range.Anchor.Line}:{range.Anchor.Column}>{range.Focus.Line}:{range.Focus.Column}";

    private static string Number(float value) => ((double)value).ToString(CultureInfo.InvariantCulture);

    private static string State(CodeEditorController editor, bool claimed)
    {
        var goTo = editor.GoTo;
        var line = goTo.Line is { } read ? $"{read.Line}:{read.Column?.ToString(CultureInfo.InvariantCulture) ?? "-"}:{(read.ColumnStarted ? 1 : 0)}:{read.Columns}" : "-";
        var selected = goTo.Selected >= 0 ? goTo.Entries[goTo.Selected] : null;
        var entry = selected is null ? "-"
            : $"{selected.Name}@{selected.Container ?? "-"}#{string.Join(".", selected.Matches)}#{string.Join(".", selected.ContainerMatches)}";
        return $"{(claimed ? 1 : 0)}|{Describe(editor.Selection)}|{(goTo.IsOpen ? 1 : 0)}|{goTo.Mode.ToString().ToLowerInvariant()}"
            + $"|{goTo.Query}|{line}|{goTo.Entries.Count}|{goTo.Selected}|{entry}"
            + $"|{(goTo.Preview is { } preview ? Describe(preview) : "-")}"
            + $"|{editor.JumpList.Index}/{editor.JumpList.Places.Count}"
            + $"|{(editor.VerticalScroll is { } down ? $"{down.Version}:{Number(down.Offset)}" : "-")}"
            + $"|{(editor.HorizontalScroll is { } across ? $"{across.Version}:{Number(across.Offset)}" : "-")}";
    }

    private static bool Run(CodeEditorController editor, Step step, KeyboardConvention convention) => step.Op switch
    {
        "key" => editor.HandleKey(step.Text!, (KeyModifiers)step.A, convention, null),
        "query" => Query(editor, step.Text!),
        "next" => editor.GoTo.SelectNext(),
        "previous" => editor.GoTo.SelectPrevious(),
        "pageDown" => editor.GoTo.SelectNextPage(),
        "pageUp" => editor.GoTo.SelectPreviousPage(),
        "accept" => editor.GoTo.Accept(),
        "close" => editor.GoTo.Close(),
        "caret" => Caret(editor, step.A, step.B),
        "type" => editor.HandleText(step.Text!),
        "view" => View(editor, step.A),
        _ => throw new InvalidOperationException(step.Op),
    };

    private static bool Query(CodeEditorController editor, string text)
    {
        editor.GoTo.Query = text;
        return editor.GoTo.IsOpen;
    }

    private static bool Caret(CodeEditorController editor, int line, int column)
    {
        editor.Selection = new CodeRange(new CodePosition(line, column));
        return true;
    }

    private static bool View(CodeEditorController editor, int top)
    {
        editor.View = new Rect(0, top, 600, 400);
        return true;
    }

    private static List<string> RunOnDotNet(string text, IReadOnlyList<CodeSymbol> outline, Session session)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.Symbols.Providers.Add(new Outlined(outline));
        editor.Selection = new CodeRange(session.Caret);
        editor.View = new Rect(0, 0, 600, 400);
        var convention = session.Apple ? KeyboardConvention.Apple : KeyboardConvention.Standard;
        return session.Steps.Select(step => State(editor, Run(editor, step, convention))).ToList();
    }

    // ---- the sessions -----------------------------------------------------------------------------------

    /// <summary>
    /// What is typed in the field: a line, with or without a column, counted from the end, or not a number; a
    /// symbol by a piece of its name, by its initials, with its container's name after it, or by nothing.
    /// </summary>
    private static string Typed(Random random, CodeDocument document, IReadOnlyList<(string Name, string? Container)> names)
    {
        switch (random.Next(10))
        {
            case 0: return $":{random.Next(-20, document.LineCount + 40)}";
            case 1: return $":{random.Next(1, document.LineCount)}{",:#"[random.Next(3)]}{random.Next(-5, 60)}";
            case 2: return random.Next(2) == 0 ? ":" : ":x12";
            case 3: return "@";
            default:
                var (name, container) = names[random.Next(names.Count)];
                var piece = random.Next(3) switch
                {
                    0 => new string(name.Where(char.IsUpper).ToArray()).ToLowerInvariant(),
                    1 => name[..Math.Min(name.Length, random.Next(1, 5))],
                    _ => name.Substring(random.Next(name.Length), 1),
                };
                if (piece.Length == 0) piece = name[..1];
                return container is not null && random.Next(3) == 0 ? $"@{piece} {container[..Math.Min(3, container.Length)]}" : $"@{piece}";
        }
    }

    private static List<Session> Sessions(string text, IReadOnlyList<CodeSymbol> outline, int count, int seed)
    {
        var random = new Random(seed);
        var document = CodeDocument.FromText(text);
        var names = new List<(string, string?)>();
        void Collect(IEnumerable<CodeSymbol> level, string? container)
        {
            foreach (var symbol in level)
            {
                names.Add((symbol.Name, container));
                Collect(symbol.Children, symbol.Name);
            }
        }
        Collect(outline, null);
        var sessions = new List<Session>();
        for (var s = 0; s < count; s++)
        {
            var apple = random.Next(2) == 0;
            var line = random.Next(document.LineCount);
            var steps = new List<Step>();
            for (var step = random.Next(12, 30); step > 0; step--)
            {
                steps.Add(random.Next(40) switch
                {
                    < 3 => new Step("key", "g", apple ? Control : Command, 0),
                    < 6 => new Step("key", "o", Shift | Command, 0),
                    < 14 => new Step("query", Typed(random, document, names), 0, 0),
                    < 17 => new Step("next", null, 0, 0),
                    < 18 => new Step("previous", null, 0, 0),
                    < 19 => new Step(random.Next(2) == 0 ? "pageDown" : "pageUp", null, 0, 0),
                    < 23 => new Step("accept", null, 0, 0),
                    < 25 => new Step("close", null, 0, 0),
                    < 28 => new Step("caret", null, random.Next(document.LineCount), random.Next(12)),
                    < 30 => new Step("type", random.Next(3) == 0 ? "\n" : "x", 0, 0),
                    < 31 => new Step("view", null, random.Next(0, document.LineCount * 18), 0),
                    < 36 => apple ? new Step("key", "-", Control, 0) : new Step("key", "ArrowLeft", Alt, 0),
                    _ => apple ? new Step("key", "_", Shift | Control, 0) : new Step("key", "ArrowRight", Alt, 0),
                });
            }
            sessions.Add(new Session(apple, new CodePosition(line, 0), steps));
        }
        return sessions;
    }

    private static string SessionProgram(string runtime, string text, IReadOnlyList<CodeSymbol> outline,
        IReadOnlyList<Session> sessions)
    {
        var encoded = JsonSerializer.Serialize(sessions.Select(session => new
        {
            apple = session.Apple,
            line = session.Caret.Line,
            column = session.Caret.Column,
            steps = session.Steps.Select(step => new object?[] { step.Op, step.Text, step.A, step.B }),
        }));
        return $$"""
            import { CodeEditorController, CodeLanguages, CodeRange, CodePosition, CodeSymbol, Rect } from '{{runtime}}';
            const text = {{JsonSerializer.Serialize(text)}};
            const encoded = {{JsonSerializer.Serialize(outline.Select(Encode))}};
            const sessions = {{encoded}};
            const range = ([a, b, c, d]) => new CodeRange(new CodePosition(a, b), new CodePosition(c, d));
            const build = (one) => {
              const symbol = new CodeSymbol(one.n, one.k, range(one.r), range(one.s));
              symbol.deprecated = one.d;
              symbol.children = one.c.map(build);
              return symbol;
            };
            const outline = encoded.map(build);
            const provider = { symbolsAsync: async () => outline };
            // An answer arrives on a later turn on the web: every step waits for one.
            const settle = () => new Promise((resolve) => setTimeout(resolve, 0));
            const describe = (r) => `${r.anchor.line}:${r.anchor.column}>${r.focus.line}:${r.focus.column}`;
            const state = (editor, claimed) => {
              const goTo = editor.goTo;
              const read = goTo.line;
              const line = read ? `${read.line}:${read.column ?? '-'}:${read.columnStarted ? 1 : 0}:${read.columns}` : '-';
              const selected = goTo.selected >= 0 ? goTo.entries[goTo.selected] : null;
              const entry = selected
                ? `${selected.name}@${selected.container ?? '-'}#${selected.matches.join('.')}#${selected.containerMatches.join('.')}`
                : '-';
              const down = editor.verticalScroll;
              const across = editor.horizontalScroll;
              return `${claimed ? 1 : 0}|${describe(editor.selection)}|${goTo.isOpen ? 1 : 0}|${goTo.mode}`
                + `|${goTo.query}|${line}|${goTo.entries.length}|${goTo.selected}|${entry}`
                + `|${goTo.preview ? describe(goTo.preview) : '-'}`
                + `|${editor.jumpList.index}/${editor.jumpList.places.length}`
                + `|${down ? `${down.version}:${down.offset}` : '-'}`
                + `|${across ? `${across.version}:${across.offset}` : '-'}`;
            };
            for (const session of sessions) {
              const editor = new CodeEditorController(text, CodeLanguages.cSharp);
              editor.symbols.providers.push(provider);
              editor.selection = new CodeRange(new CodePosition(session.line, session.column));
              editor.view = new Rect(0, 0, 600, 400);
              const convention = session.apple ? 'apple' : 'standard';
              const states = [];
              for (const [op, typed, a, b] of session.steps) {
                let claimed;
                switch (op) {
                  case 'key': claimed = editor.handleKey(typed, a, convention, null); break;
                  case 'query': editor.goTo.query = typed; claimed = editor.goTo.isOpen; break;
                  case 'next': claimed = editor.goTo.selectNext(); break;
                  case 'previous': claimed = editor.goTo.selectPrevious(); break;
                  case 'pageDown': claimed = editor.goTo.selectNextPage(); break;
                  case 'pageUp': claimed = editor.goTo.selectPreviousPage(); break;
                  case 'accept': claimed = editor.goTo.accept(); break;
                  case 'close': claimed = editor.goTo.close(); break;
                  case 'caret': editor.selection = new CodeRange(new CodePosition(a, b)); claimed = true; break;
                  case 'type': claimed = editor.handleText(typed); break;
                  case 'view': editor.view = new Rect(0, a, 600, 400); claimed = true; break;
                }
                await settle();
                states.push(state(editor, claimed));
              }
              console.log('=' + JSON.stringify(states));
            }
            """;
    }

    [SkippableFact]
    public void Sessions_OverARealFile_GoToTheSamePlacesAndKeepTheSameList()
    {
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        var text = File.ReadAllText(Path.Combine(RepoRoot.Find()!, "src", "eQuantic.UI.Code", "Editing", "CodeEditorController.cs"))
            .Replace("\r\n", "\n");
        var outline = Outline(text);
        var sessions = Sessions(text, outline, 60, 2026_10_25);

        var web = JsExecutor.Run(SessionProgram(runtime, text, outline, sessions), timeoutMs: 300_000)
            .Split('\n').Where(line => line.StartsWith('=')).ToList();

        var differences = new StringBuilder();
        var count = 0;
        var steps = 0;
        var listed = 0;
        var walked = 0;
        for (var i = 0; i < sessions.Count; i++)
        {
            var dotnet = RunOnDotNet(text, outline, sessions[i]);
            steps += dotnet.Count;
            for (var step = 0; step < dotnet.Count; step++)
            {
                var fields = dotnet[step].Split('|');
                if (fields[3] == "symbol" && fields[6] != "0") listed++;
                if (sessions[i].Steps[step].Op == "key" && sessions[i].Steps[step].Text is "-" or "_" or "ArrowLeft" or "ArrowRight"
                    && fields[0] == "1") walked++;
            }
            var twin = i < web.Count ? JsonSerializer.Deserialize<List<string>>(web[i].TrimEnd('\r')[1..])! : [];
            var at = Enumerable.Range(0, dotnet.Count)
                .FirstOrDefault(step => step >= twin.Count || dotnet[step] != twin[step], -1);
            if (at < 0) continue;
            if (count++ < 3)
            {
                var done = string.Join(" ", sessions[i].Steps.Take(at + 1).Select(step => $"[{step.Op} {step.Text} {step.A} {step.B}]"));
                differences.Append($"\n  session {i} ({(sessions[i].Apple ? "apple" : "standard")}), step {at} after {done}:"
                    + $"\n    .NET    {dotnet[at]}\n    the web {(at < twin.Count ? twin[at] : "(none)")}");
            }
        }

        outline.Should().NotBeEmpty();
        steps.Should().BeGreaterThan(1000, "the sessions are what is compared");
        listed.Should().BeGreaterThan(150, "the symbols are listed often enough to compare");
        walked.Should().BeGreaterThan(50, "the jump list is walked often enough to compare");
        count.Should().Be(0, $"navigation behaves the same on both sides:{differences}");
    }
}
