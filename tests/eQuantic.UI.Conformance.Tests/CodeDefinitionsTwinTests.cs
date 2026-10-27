using System.Globalization;
using System.Text;
using System.Text.Json;
using eQuantic.UI.Code;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The code engine's definitions and references (#1084) go to the same places on both sides: the C# engine, and
/// its twin in the bundle the Server serves, run by the SDK's embedded Bun. A page goes to a definition through the
/// twin and an IDE on Photon through .NET, so a list ordered otherwise, a place carried elsewhere, a link shown on
/// one side only or a walk that wrapped differently would be two editors.
/// <para>
/// Compared over whole sessions over a real source file, whose answers are Roslyn's: the semantic model of the
/// engine's own project says, for every name in <c>CodeGoTo.cs</c>, where it is defined (in that file or in another
/// of the project, which the editor hands to the app), of what type, where it is implemented, and where in the
/// file it is used. Seeded steps press F12, ⌘F12, ⇧F12, F4 and ⇧F4, Escape and the walk back, filter and walk the
/// list, accept or close it, move the caret onto a name, type, scroll, hold ⌘ over a name and click it, read after
/// every one: the caret, the list and what it selects and washes, the places held, what was said, the link and
/// the pointer over it, the documents asked to open, the jump list and the scroll the editor asked of its view.
/// </para>
/// </summary>
public class CodeDefinitionsTwinTests
{
    private sealed record Step(string Op, string? Text, int A, int B);

    private sealed record Session(bool Apple, CodePosition Caret, List<Step> Steps);

    private const int Shift = (int)KeyModifiers.Shift;
    private const int Command = (int)KeyModifiers.Command;
    private const int Control = (int)KeyModifiers.Control;
    private const int Alt = (int)KeyModifiers.Alt;

    /// <summary>A place: the document's index in <see cref="Answers.Documents"/>, -1 for the file itself, and its
    /// range.</summary>
    private sealed record Where(int D, int L1, int C1, int L2, int C2);

    /// <summary>What Roslyn answers about one symbol: the places of each question.</summary>
    private sealed record Answer(List<Where> Defined, List<Where> Typed, List<Where> Implemented, List<Where> Used);

    /// <summary>A name of the file, where it stands, and the answer about the symbol it names.</summary>
    private sealed record Name(int L, int S, int E, int Answer);

    private sealed record Answers(List<string> Documents, List<Answer> Symbols, List<Name> Names);

    // ---- Roslyn's answers ---------------------------------------------------------------------------------

    /// <summary>
    /// The answers of the semantic model of every source file of <c>eQuantic.UI.Code</c>, compiled as the project
    /// compiles them (its implicit usings, against the framework and the vocabulary), for every identifier of
    /// <paramref name="file"/>: the symbol it names or declares, the places that symbol is declared, the places its
    /// type is, the places it is implemented (an interface's implementations, an abstract or virtual member's
    /// overrides, a concrete member itself, as Roslyn's <c>FindImplementations</c> answers), and every identifier of
    /// the file that names it, with its declarations.
    /// </summary>
    private static Answers Roslyn(string root, string file)
    {
        var project = Path.Combine(root, "src", "eQuantic.UI.Code");
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var trees = Directory.GetFiles(project, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path).Replace("\r\n", "\n"), parse,
                Path.GetRelativePath(project, path).Replace('\\', '/')))
            .ToList();
        trees.Add(CSharpSyntaxTree.ParseText(
            "global using System; global using System.Collections.Generic; global using System.IO; global using System.Linq; "
            + "global using System.Net.Http; global using System.Threading; global using System.Threading.Tasks;", parse, "Usings.cs"));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => !Path.GetFileName(path).StartsWith("eQuantic.UI.Code", StringComparison.Ordinal))
            .Select(path => TestReferences.Of(path));
        var compilation = CSharpCompilation.Create("Code", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var target = trees.Single(tree => tree.FilePath == file);
        var model = compilation.GetSemanticModel(target);
        var types = compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<INamedTypeSymbol>().ToList();

        var documents = new List<string>();
        Where Place(Location location)
        {
            var span = location.GetLineSpan();
            var document = -1;
            if (span.Path != file)
            {
                document = documents.IndexOf(span.Path);
                if (document < 0)
                {
                    documents.Add(span.Path);
                    document = documents.Count - 1;
                }
            }
            return new Where(document, span.StartLinePosition.Line, span.StartLinePosition.Character,
                span.EndLinePosition.Line, span.EndLinePosition.Character);
        }
        List<Where> Places(IEnumerable<ISymbol> symbols) =>
            symbols.SelectMany(symbol => symbol.Locations).Where(location => location.IsInSource).Select(Place).ToList();

        var named = new List<(SyntaxToken Token, ISymbol Symbol)>();
        foreach (var token in target.GetRoot().DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)))
        {
            var node = token.Parent!;
            var info = model.GetSymbolInfo(node);
            var symbol = model.GetDeclaredSymbol(node) ?? info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
            if (symbol is not null) named.Add((token, symbol.OriginalDefinition));
        }

        // One answer for each symbol the file names, every name of it pointing at it.
        var answered = new Dictionary<ISymbol, int>(SymbolEqualityComparer.Default);
        var symbols = new List<Answer>();
        foreach (var group in named.GroupBy(one => one.Symbol, SymbolEqualityComparer.Default))
        {
            var symbol = group.Key;
            var used = group.Select(one => Place(one.Token.GetLocation())).Concat(Places([symbol])).ToList();
            answered[symbol] = symbols.Count;
            symbols.Add(new Answer(Places([symbol]), TypeOf(symbol) is { } type ? Places([type]) : [],
                Places(Implementations(symbol, types)), used));
        }
        var names = named.Select(one =>
        {
            var at = one.Token.GetLocation().GetLineSpan();
            return new Name(at.StartLinePosition.Line, at.StartLinePosition.Character, at.EndLinePosition.Character,
                answered[one.Symbol]);
        }).ToList();
        return new Answers(documents, symbols, names);
    }

    private static ISymbol? TypeOf(ISymbol symbol)
    {
        var type = symbol switch
        {
            ILocalSymbol local => local.Type,
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            IParameterSymbol parameter => parameter.Type,
            IMethodSymbol method => method.ReturnType,
            IEventSymbol happening => happening.Type,
            ITypeSymbol itself => itself,
            _ => null,
        };
        while (type is IArrayTypeSymbol array) type = array.ElementType;
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        return type?.OriginalDefinition;
    }

    private static IEnumerable<ISymbol> Implementations(ISymbol symbol, List<INamedTypeSymbol> types)
    {
        if (symbol is INamedTypeSymbol { TypeKind: TypeKind.Interface } contract)
            return types.Where(type => type.AllInterfaces.Any(one => SymbolEqualityComparer.Default.Equals(one.OriginalDefinition, contract)));
        if (symbol.ContainingType is { TypeKind: TypeKind.Interface } && symbol is IMethodSymbol or IPropertySymbol or IEventSymbol)
            return types.Select(type => type.FindImplementationForInterfaceMember(symbol)).Where(found => found is not null)!;
        if (symbol.IsAbstract || symbol.IsVirtual)
            return types.SelectMany(type => type.GetMembers(symbol.Name))
                .Where(member => Overridden(member) is { } base_ && SymbolEqualityComparer.Default.Equals(base_.OriginalDefinition, symbol));
        return symbol is IMethodSymbol or IPropertySymbol or IEventSymbol ? [symbol] : [];
    }

    private static ISymbol? Overridden(ISymbol member) => member switch
    {
        IMethodSymbol method => method.OverriddenMethod,
        IPropertySymbol property => property.OverriddenProperty,
        IEventSymbol happening => happening.OverriddenEvent,
        _ => null,
    };

    /// <summary>The name a position falls on, as the program on the web finds it: the first, in the file's order,
    /// on the position's line whose start is at or before it and whose end is at or after it.</summary>
    private static Name? NameAt(Answers answers, CodePosition position) =>
        answers.Names.FirstOrDefault(name => name.L == position.Line && name.S <= position.Column && position.Column <= name.E);

    private static IReadOnlyList<CodeLocation> Locations(Answers answers, List<Where> places) =>
        places.Select(place => new CodeLocation(new CodeRange(new CodePosition(place.L1, place.C1), new CodePosition(place.L2, place.C2)))
        {
            Document = place.D < 0 ? null : answers.Documents[place.D],
        }).ToList();

    private sealed class Defining(Answers answers) : ICodeDefinitionProvider
    {
        public Task<IReadOnlyList<CodeLocation>> DefinitionsAsync(CodeDocument document, CodePosition position,
            CodeDefinitionKind kind, CancellationToken cancellation)
        {
            if (NameAt(answers, position) is not { } name) return Task.FromResult<IReadOnlyList<CodeLocation>>([]);
            var answer = answers.Symbols[name.Answer];
            var places = kind switch
            {
                CodeDefinitionKind.TypeDefinition => answer.Typed,
                CodeDefinitionKind.Implementation => answer.Implemented,
                _ => answer.Defined,
            };
            return Task.FromResult(Locations(answers, places));
        }
    }

    private sealed class Referring(Answers answers) : ICodeReferenceProvider
    {
        public Task<IReadOnlyList<CodeLocation>> ReferencesAsync(CodeDocument document, CodePosition position,
            CancellationToken cancellation) =>
            Task.FromResult(NameAt(answers, position) is { } name ? Locations(answers, answers.Symbols[name.Answer].Used) : []);
    }

    // ---- the state after a step -------------------------------------------------------------------------

    private static string Describe(CodeRange range) =>
        $"{range.Anchor.Line}:{range.Anchor.Column}>{range.Focus.Line}:{range.Focus.Column}";

    private static string Number(float value) => ((double)value).ToString(CultureInfo.InvariantCulture);

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private sealed class Watched
    {
        public int Opened;
        public string Last = "-";
        public Point Pointer;
    }

    private static string State(CodeEditorController editor, Watched watched, bool claimed)
    {
        var goTo = editor.GoTo;
        var selected = goTo.Selected >= 0 ? goTo.Entries[goTo.Selected] : null;
        var entry = selected is null ? "-"
            : $"{selected.Name}@{selected.Container ?? "-"}@{selected.Document ?? "-"}#{string.Join(".", selected.Matches)}";
        var said = goTo.NotFound is { } nothing ? $"{Camel(nothing.Mode.ToString())}:{Camel(nothing.Kind.ToString())}:{nothing.Word}" : "-";
        return $"{(claimed ? 1 : 0)}|{Describe(editor.Selection)}|{(goTo.IsOpen ? 1 : 0)}|{Camel(goTo.Mode.ToString())}"
            + $"|{Camel(goTo.DefinitionKind.ToString())}|{goTo.Query}|{goTo.Entries.Count}|{goTo.Selected}|{entry}"
            + $"|{(goTo.Preview is { } preview ? Describe(preview) : "-")}"
            + $"|{goTo.PlaceIndex}/{string.Join(",", goTo.Places.Select(place => (place.Document ?? "") + Describe(place.Range)))}"
            + $"|{said}|{(editor.Definitions.Link is { } link ? Describe(link) : "-")}|{Camel(editor.CursorAt(watched.Pointer).ToString())}"
            + $"|{watched.Opened}:{watched.Last}"
            + $"|{editor.JumpList.Index}/{editor.JumpList.Places.Count}"
            + $"|{(editor.VerticalScroll is { } down ? $"{down.Version}:{Number(down.Offset)}" : "-")}"
            + $"|{(editor.HorizontalScroll is { } across ? $"{across.Version}:{Number(across.Offset)}" : "-")}";
    }

    /// <summary>Where a step points at the character at its position: a pixel into its cell, halfway down its row.</summary>
    private static Point On(CodeEditorController editor, int line, int column)
    {
        var at = editor.CaretRect(editor.Document.Clamp(new CodePosition(line, column)));
        return new Point(at.X + 1, at.Y + at.Height / 2);
    }

    private static bool Run(CodeEditorController editor, Watched watched, Step step, KeyboardConvention convention)
    {
        switch (step.Op)
        {
            case "key": return editor.HandleKey(step.Text!, (KeyModifiers)step.A, convention, null);
            case "query":
                editor.GoTo.Query = step.Text!;
                return editor.GoTo.IsOpen;
            case "next": return editor.GoTo.SelectNext();
            case "previous": return editor.GoTo.SelectPrevious();
            case "accept": return editor.GoTo.Accept();
            case "close": return editor.GoTo.Close();
            case "caret":
                editor.Selection = new CodeRange(new CodePosition(step.A, step.B));
                return true;
            case "type": return editor.HandleText(step.Text!);
            case "view":
                editor.View = new Rect(0, step.A, 600, 400);
                return true;
            case "link":
                watched.Pointer = On(editor, step.A, step.B);
                return editor.HandlePointer(PointerPhase.Hover, watched.Pointer, KeyModifiers.Command, 0);
            case "release": return editor.HandlePointer(PointerPhase.Hover, watched.Pointer, KeyModifiers.None, 0);
            case "click":
                watched.Pointer = On(editor, step.A, step.B);
                editor.HandlePointer(PointerPhase.Down, watched.Pointer, KeyModifiers.Command, 1);
                return editor.HandlePointer(PointerPhase.Up, watched.Pointer, KeyModifiers.Command, 1);
            default: throw new InvalidOperationException(step.Op);
        }
    }

    private static List<string> RunOnDotNet(string text, Answers answers, Session session)
    {
        var editor = new CodeEditorController(text, CodeLanguages.CSharp);
        editor.Definitions.Providers.Add(new Defining(answers));
        editor.References.Providers.Add(new Referring(answers));
        var watched = new Watched();
        editor.OpenRequested += place =>
        {
            watched.Opened++;
            watched.Last = place.Document + Describe(place.Range);
        };
        editor.Selection = new CodeRange(session.Caret);
        editor.View = new Rect(0, 0, 600, 400);
        var convention = session.Apple ? KeyboardConvention.Apple : KeyboardConvention.Standard;
        return session.Steps.Select(step => State(editor, watched, Run(editor, watched, step, convention))).ToList();
    }

    // ---- the sessions -----------------------------------------------------------------------------------

    private static List<Session> Sessions(string text, Answers answers, int count, int seed)
    {
        var random = new Random(seed);
        var document = CodeDocument.FromText(text);
        // A name of the file, and a position on it, which a step goes to often, so its questions have answers.
        (int Line, int Column) OnAName()
        {
            var name = answers.Names[random.Next(answers.Names.Count)];
            return (name.L, name.S + random.Next(name.E - name.S + 1));
        }
        var sessions = new List<Session>();
        for (var s = 0; s < count; s++)
        {
            var apple = random.Next(2) == 0;
            var (line, column) = OnAName();
            var steps = new List<Step>();
            for (var step = random.Next(12, 30); step > 0; step--)
            {
                var (l, c) = OnAName();
                steps.Add(random.Next(48) switch
                {
                    < 5 => new Step("key", "F12", 0, 0),
                    < 7 => new Step("key", "F12", Command, 0),
                    < 11 => new Step("key", "F12", Shift, 0),
                    < 15 => new Step("key", "F4", 0, 0),
                    < 17 => new Step("key", "F4", Shift, 0),
                    < 19 => new Step("key", "Escape", 0, 0),
                    < 21 => apple ? new Step("key", "-", Control, 0) : new Step("key", "ArrowLeft", Alt, 0),
                    < 23 => new Step("query", random.Next(4) == 0 ? "" : new[] { "var", "_", "(", "editor", "places", "if" }[random.Next(6)], 0, 0),
                    < 25 => new Step(random.Next(3) == 0 ? "previous" : "next", null, 0, 0),
                    < 28 => new Step("accept", null, 0, 0),
                    < 29 => new Step("close", null, 0, 0),
                    < 35 => new Step("caret", null, l, c),
                    < 37 => new Step("type", random.Next(3) == 0 ? "\n" : "x", 0, 0),
                    < 38 => new Step("view", null, random.Next(0, document.LineCount * 18), 0),
                    < 42 => new Step("link", null, l, c),
                    < 43 => new Step("release", null, 0, 0),
                    _ => new Step("click", null, l, c),
                });
            }
            sessions.Add(new Session(apple, new CodePosition(line, column), steps));
        }
        return sessions;
    }

    private static string SessionProgram(string runtime, string text, Answers answers, IReadOnlyList<Session> sessions)
    {
        var encoded = JsonSerializer.Serialize(sessions.Select(session => new
        {
            apple = session.Apple,
            line = session.Caret.Line,
            column = session.Caret.Column,
            steps = session.Steps.Select(step => new object?[] { step.Op, step.Text, step.A, step.B }),
        }));
        static int[][] Places(List<Where> places) => places.Select(p => new[] { p.D, p.L1, p.C1, p.L2, p.C2 }).ToArray();
        var names = JsonSerializer.Serialize(answers.Names.Select(name => new[] { name.L, name.S, name.E, name.Answer }));
        var symbols = JsonSerializer.Serialize(answers.Symbols.Select(answer => new[]
        {
            Places(answer.Defined), Places(answer.Typed), Places(answer.Implemented), Places(answer.Used),
        }));
        return $$"""
            import { CodeEditorController, CodeLanguages, CodeRange, CodePosition, CodeLocation, Rect, Point } from '{{runtime}}';
            const text = {{JsonSerializer.Serialize(text)}};
            const documents = {{JsonSerializer.Serialize(answers.Documents)}};
            const names = {{names}};
            const symbols = {{symbols}};
            const sessions = {{encoded}};
            const nameAt = (p) => names.find(([l, s, e]) => l === p.line && s <= p.column && p.column <= e);
            const locations = (places) => places.map(([d, l1, c1, l2, c2]) => {
              const place = new CodeLocation(new CodeRange(new CodePosition(l1, c1), new CodePosition(l2, c2)));
              place.document = d < 0 ? null : documents[d];
              return place;
            });
            const defining = {
              definitionsAsync: async (document, position, kind) => {
                const name = nameAt(position);
                if (!name) return [];
                const answer = symbols[name[3]];
                return locations(kind === 'typeDefinition' ? answer[1] : kind === 'implementation' ? answer[2] : answer[0]);
              },
            };
            const referring = {
              referencesAsync: async (document, position) => {
                const name = nameAt(position);
                return name ? locations(symbols[name[3]][3]) : [];
              },
            };
            // An answer arrives on a later turn on the web: every step waits for one.
            const settle = () => new Promise((resolve) => setTimeout(resolve, 0));
            const describe = (r) => `${r.anchor.line}:${r.anchor.column}>${r.focus.line}:${r.focus.column}`;
            const state = (editor, watched, claimed) => {
              const goTo = editor.goTo;
              const selected = goTo.selected >= 0 ? goTo.entries[goTo.selected] : null;
              const entry = selected
                ? `${selected.name}@${selected.container ?? '-'}@${selected.document ?? '-'}#${selected.matches.join('.')}`
                : '-';
              const nothing = goTo.notFound;
              const said = nothing ? `${nothing.mode}:${nothing.kind}:${nothing.word}` : '-';
              const link = editor.definitions.link;
              const down = editor.verticalScroll;
              const across = editor.horizontalScroll;
              return `${claimed ? 1 : 0}|${describe(editor.selection)}|${goTo.isOpen ? 1 : 0}|${goTo.mode}`
                + `|${goTo.definitionKind}|${goTo.query}|${goTo.entries.length}|${goTo.selected}|${entry}`
                + `|${goTo.preview ? describe(goTo.preview) : '-'}`
                + `|${goTo.placeIndex}/${goTo.places.map((place) => (place.document ?? '') + describe(place.range)).join(',')}`
                + `|${said}|${link ? describe(link) : '-'}|${editor.cursorAt(watched.pointer)}`
                + `|${watched.opened}:${watched.last}`
                + `|${editor.jumpList.index}/${editor.jumpList.places.length}`
                + `|${down ? `${down.version}:${down.offset}` : '-'}`
                + `|${across ? `${across.version}:${across.offset}` : '-'}`;
            };
            const on = (editor, line, column) => {
              const at = editor.caretRect(editor.document.clamp(new CodePosition(line, column)));
              return new Point(at.x + 1, at.y + at.height / 2);
            };
            for (const session of sessions) {
              const editor = new CodeEditorController(text, CodeLanguages.cSharp);
              editor.definitions.providers.push(defining);
              editor.references.providers.push(referring);
              const watched = { opened: 0, last: '-', pointer: new Point(0, 0) };
              // A C# event is a delegate field on the twin, which nothing else here listens to.
              editor.openRequested = (place) => {
                watched.opened++;
                watched.last = place.document + describe(place.range);
              };
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
                  case 'accept': claimed = editor.goTo.accept(); break;
                  case 'close': claimed = editor.goTo.close(); break;
                  case 'caret': editor.selection = new CodeRange(new CodePosition(a, b)); claimed = true; break;
                  case 'type': claimed = editor.handleText(typed); break;
                  case 'view': editor.view = new Rect(0, a, 600, 400); claimed = true; break;
                  case 'link':
                    watched.pointer = on(editor, a, b);
                    claimed = editor.handlePointer('hover', watched.pointer, {{Command}}, 0);
                    break;
                  case 'release': claimed = editor.handlePointer('hover', watched.pointer, 0, 0); break;
                  case 'click':
                    watched.pointer = on(editor, a, b);
                    editor.handlePointer('down', watched.pointer, {{Command}}, 1);
                    claimed = editor.handlePointer('up', watched.pointer, {{Command}}, 1);
                    break;
                }
                await settle();
                states.push(state(editor, watched, claimed));
              }
              console.log('=' + JSON.stringify(states));
            }
            """;
    }

    [SkippableFact]
    public void Sessions_OverRoslynsAnswers_GoToTheSamePlaces()
    {
        Skip.IfNot(JsExecutor.EngineName == "bun", "The twin runs in the embedded Bun, and the engine here is not Bun.");
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        var root = RepoRoot.Find()!;
        const string file = "Navigation/CodeGoTo.cs";
        var text = File.ReadAllText(Path.Combine(root, "src", "eQuantic.UI.Code", file)).Replace("\r\n", "\n");
        var answers = Roslyn(root, file);
        var sessions = Sessions(text, answers, 60, 2026_10_26);

        var web = JsExecutor.Run(SessionProgram(runtime, text, answers, sessions), timeoutMs: 300_000)
            .Split('\n').Where(line => line.StartsWith('=')).ToList();

        var differences = new StringBuilder();
        var count = 0;
        var steps = 0;
        var listed = 0;
        var gone = 0;
        var said = 0;
        var linked = 0;
        var opened = 0;
        for (var i = 0; i < sessions.Count; i++)
        {
            var dotnet = RunOnDotNet(text, answers, sessions[i]);
            steps += dotnet.Count;
            for (var step = 0; step < dotnet.Count; step++)
            {
                var fields = dotnet[step].Split('|');
                if (fields[2] == "1" && fields[3] is "definitions" or "references") listed++;
                if (step > 0 && fields[1] != dotnet[step - 1].Split('|')[1] && sessions[i].Steps[step].Op is "key" or "accept" or "click") gone++;
                if (fields[11] != "-") said++;
                if (fields[12] != "-") linked++;
                if (step > 0 && fields[14] != dotnet[step - 1].Split('|')[14]) opened++;
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

        answers.Names.Should().HaveCountGreaterThan(500, "Roslyn names what the file uses");
        answers.Documents.Should().NotBeEmpty("the file uses names the project declares elsewhere");
        steps.Should().BeGreaterThan(1000, "the sessions are what is compared");
        listed.Should().BeGreaterThan(100, "places are listed often enough to compare");
        gone.Should().BeGreaterThan(50, "places are gone to often enough to compare");
        said.Should().BeGreaterThan(10, "nothing found is said often enough to compare");
        linked.Should().BeGreaterThan(50, "the link shows often enough to compare");
        opened.Should().BeGreaterThan(10, "another document is asked for often enough to compare");
        count.Should().Be(0, $"definitions and references behave the same on both sides:{differences}");
    }
}
