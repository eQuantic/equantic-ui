using System.Diagnostics;
using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace eQuantic.UI.Design;

/// <summary>
/// One editor session: the project's Roslyn compilation, built once, and a compiler driven against
/// the EDITOR'S buffer on every request.
/// <para>
/// This is the whole reason the design host is a process and not a build. <c>eqc</c> reads files and
/// the hot-reload service watches the filesystem, so neither can show you the text you are currently
/// looking at — only the last text you saved. <see cref="ComponentCompiler.CompileSource"/> takes a
/// string, and <see cref="SemanticModelProvider"/> swaps the buffer's tree in for the file's own by
/// path, so an unsaved edit compiles against the real project: its other types, its global usings,
/// its generated sources, its exact MSBuild reference set.
/// </para>
/// </summary>
public sealed class DesignSession
{
    // DesignMode: every node construction is stamped with the C# span that built it, which is the
    // identity click-to-select runs on. Nothing else in the product turns this on.
    private readonly ComponentCompiler _compiler = new() { TypeAnnotations = false, DesignMode = true };
    private Compilation? _compilation;

    /// <summary>The compilation with the editor's unsaved buffers applied — what every question is
    /// actually answered against. Equal to <see cref="_compilation"/> until something is open.</summary>
    private Compilation? _current;
    private string _projectDir = "";

    /// <summary>Emitted JS per dependency FILE, keyed by path and invalidated by write time — a page's
    /// neighbours do not change while you are typing in the page.</summary>
    private readonly Dictionary<string, (DateTime Stamp, List<CompilationResult> Modules)> _dependencies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Files the editor holds UNSAVED, by full path. A screen is rarely one file — a shell, a row, a
    /// data helper — and reading a page's neighbours from disk showed the last saved version of
    /// everything except the one file being typed in, which is the confusing half of a stale preview.
    /// </summary>
    private readonly Dictionary<string, string> _open = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Which file declares a given TYPE — including the ones a source generator wrote, which
    /// no filename search would find. Built once, from the compilation itself.</summary>
    private readonly Dictionary<string, string> _declaringFile = new(StringComparer.Ordinal);

    /// <summary>
    /// Installs the editor's unsaved buffers and rebuilds the compilation on top of them, so the
    /// SEMANTIC MODEL agrees with what the author is looking at across every open file — not only the
    /// one being previewed. Anything previously open and now absent is forgotten, so a file closed
    /// without saving goes back to what is on disk.
    /// </summary>
    public void SyncBuffers(IEnumerable<OpenBuffer>? buffers)
    {
        if (buffers is null || _compilation is null) return;

        var next = buffers
            .Where(b => !string.IsNullOrEmpty(b.Path))
            .ToDictionary(b => Path.GetFullPath(b.Path), b => b.Text, StringComparer.OrdinalIgnoreCase);

        if (next.Count == _open.Count && next.All(entry => _open.TryGetValue(entry.Key, out var had) && had == entry.Value))
            return;

        _open.Clear();
        foreach (var entry in next) _open[entry.Key] = entry.Value;

        var current = _compilation;
        foreach (var entry in _open)
        {
            current = Swap(current, CSharpSyntaxTree.ParseText(entry.Value, path: entry.Key));
        }

        _current = current;
        _compiler.SetProjectCompilation(_current);
    }

    /// <summary>The file as the AUTHOR currently sees it: their unsaved buffer if there is one, and
    /// what is on disk otherwise.</summary>
    private string ReadAsOpen(string path) =>
        _open.TryGetValue(Path.GetFullPath(path), out var text) ? text : File.ReadAllText(path);

    /// <summary>
    /// Builds the compilation the whole session runs on. Measured at ~200 ms for a real project
    /// (71 files, 316 references), paid once at activation rather than on the first keystroke.
    /// </summary>
    public InitializeResult Initialize(string projectDir, string? refsFile, string? generatedDir)
    {
        var watch = Stopwatch.StartNew();
        _projectDir = projectDir;

        var sources = new List<string>();
        sources.AddRange(ProjectCompilationHelper.GetProjectSourceFiles(projectDir));
        // What <ImplicitUsings> and the SDK put in every file without an import. Without these the
        // semantic model rejects List<T>, LINQ and the whole declarative factory surface, and every
        // call falls back to the no-model path — which emits plausible, wrong JavaScript.
        sources.AddRange(ProjectCompilationHelper.GetGeneratedGlobalUsingsFiles(projectDir));
        sources.AddRange(string.IsNullOrEmpty(generatedDir)
            ? ProjectCompilationHelper.GetCompilerGeneratedFiles(projectDir)
            : ProjectCompilationHelper.GetCompilerGeneratedFiles(projectDir, generatedDir));

        var references = ReadReferences(refsFile);

        // A degraded reference set is the one failure that produces WRONG OUTPUT WITH NO ERROR:
        // without the semantic model the compiler passes named arguments through in syntactic order,
        // so a component renders with its values in the wrong slots and nothing anywhere says so.
        // Refuse the session instead — the extension turns this into a visible message.
        if (references.Count == 0)
        {
            throw new InvalidOperationException(
                $"No assembly references resolved{(string.IsNullOrEmpty(refsFile) ? "" : $" from {refsFile}")}. "
                + "Build the project once (dotnet build) so the SDK writes equantic.refs.txt, then reopen the preview.");
        }

        var assemblyName = Directory.GetFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly) is [var csproj, ..]
            ? Path.GetFileNameWithoutExtension(csproj)
            : "DesignAssembly";

        _compilation = WithDocumentation(ProjectCompilationHelper.CreateCompilationFromSources(
            sources, references, assemblyName: assemblyName, addStandardReferences: false));
        _current = _compilation;
        _compiler.SetProjectCompilation(_current);

        _declaringFile.Clear();
        foreach (var tree in _compilation.SyntaxTrees)
        {
            if (string.IsNullOrEmpty(tree.FilePath)) continue;
            foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                // First declaration wins: a partial type spread over several files is compiled from
                // whichever one holds it, and eqc emits one module per type either way.
                _declaringFile.TryAdd(declaration.Identifier.Text, tree.FilePath);
            }
        }

        watch.Stop();
        return new InitializeResult(assemblyName, sources.Count, references.Count, (int)watch.ElapsedMilliseconds);
    }

    /// <summary>
    /// The theme the preview renders under, as the same JSON the SSR shell hands the client — so
    /// <c>materializeTheme</c> in the webview rebuilds exactly what a served page would have.
    /// <para>
    /// It is the framework BASELINE, not the app's own: the app selects its theme by calling
    /// <c>AddUI(…).UseTheme(…)</c> at startup, and reading that back would mean running the app's
    /// composition root. A preview under the baseline is honest and never wrong about the shapes;
    /// matching a rebranded palette is a later question, and a visible one.
    /// </para>
    /// </summary>
    public string Theme() => eQuantic.UI.Web.ThemeBridge.SerializeJson(eQuantic.UI.Material.MaterialTheme.Instance);

    /// <summary>
    /// Re-attaches each reference's XML documentation, so the inspector can describe a framework
    /// component and not only the developer's own.
    /// <para>
    /// It has to be done by hand because MSBuild hands eqc REF ASSEMBLIES — <c>obj/…/ref/X.dll</c> —
    /// and the documentation is written a directory above them, beside the implementation. Roslyn
    /// looks only next to the file it was given, finds nothing, and every framework symbol comes back
    /// undocumented. This is the whole reason turning on <c>GenerateDocumentationFile</c> was not by
    /// itself enough.
    /// </para>
    /// </summary>
    private static Compilation WithDocumentation(Compilation compilation) =>
        compilation.WithReferences(compilation.References.Select(reference =>
            reference is PortableExecutableReference { FilePath: { Length: > 0 } file } portable
            && DocumentationBeside(file) is { } xml
                ? MetadataReference.CreateFromFile(file, portable.Properties, XmlDocumentationProvider.CreateFromFile(xml))
                : reference));

    private static string? DocumentationBeside(string assemblyPath)
    {
        var beside = Path.ChangeExtension(assemblyPath, ".xml");
        if (File.Exists(beside)) return beside;

        // `obj/<cfg>/<tfm>/ref/X.dll` → `obj/<cfg>/<tfm>/X.xml`.
        var directory = Path.GetDirectoryName(assemblyPath);
        if (directory is null || !string.Equals(Path.GetFileName(directory), "ref", StringComparison.OrdinalIgnoreCase))
            return null;

        var parent = Path.GetDirectoryName(directory);
        if (parent is null) return null;

        var above = Path.Combine(parent, Path.GetFileNameWithoutExtension(assemblyPath) + ".xml");
        return File.Exists(above) ? above : null;
    }

    private static List<string> ReadReferences(string? refsFile)
    {
        if (string.IsNullOrEmpty(refsFile) || !File.Exists(refsFile)) return [];
        return File.ReadAllLines(refsFile)
            .Select(line => line.Trim())
            .Where(path => path.Length > 0 && File.Exists(path))
            .ToList();
    }

    /// <summary>
    /// The squiggles, spans and all — binding without transpiling, which is a fraction of a compile
    /// and can be asked while someone is still typing.
    /// </summary>
    public DesignMark[] Diagnose(string path, string text)
    {
        if (_compilation is null) throw new InvalidOperationException("initialize first");

        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        var compilation = Swap(_current!, tree);

        var marks = new List<DesignMark>();
        foreach (var diagnostic in compilation.GetSemanticModel(tree).GetDiagnostics())
        {
            if (diagnostic.Severity is not (DiagnosticSeverity.Error or DiagnosticSeverity.Warning)) continue;

            var span = diagnostic.Location.GetLineSpan();
            if (!span.IsValid) continue;

            var startLine = span.StartLinePosition.Line;
            var startColumn = span.StartLinePosition.Character;
            var endLine = span.EndLinePosition.Line;
            var endColumn = span.EndLinePosition.Character;
            // A zero-width span — an expected token, a missing brace — would underline nothing.
            if (endLine == startLine && endColumn <= startColumn) endColumn = startColumn + 1;

            marks.Add(new DesignMark(startLine, startColumn, endLine, endColumn,
                diagnostic.GetMessage(), diagnostic.Id,
                diagnostic.Severity == DiagnosticSeverity.Error));
        }

        return marks.ToArray();
    }

    /// <summary>
    /// What may be done with the node an origin names — see <see cref="OriginTier"/>.
    /// <para>
    /// This is computed, not guessed: the origin is an exact span, so the question "is there a loop
    /// or a conditional between this expression and the Build method it lives in" has a real answer
    /// from the syntax tree. Structural correlation could never answer it, which is why the tiers
    /// wait on origins rather than the other way round.
    /// </para>
    /// </summary>
    public OriginTier Classify(string path, string text, string origin)
    {
        var parts = origin.Split('|');
        if (parts.Length != 3) return new OriginTier("foreign", "This element carries no readable origin.", null);

        // Another FILE is foreign before anything is parsed: the buffer in hand is not its source.
        if (!SamePath(parts[0], path))
        {
            var file = Path.GetFileName(parts[0]);
            return new OriginTier("foreign", $"Defined in {file}, which is not the file being previewed.", file);
        }

        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        var root = tree.GetRoot();
        var source = tree.GetText();

        var (line, character) = ParsePosition(parts[1]);
        if (line >= source.Lines.Count) return new OriginTier("foreign", "This origin is past the end of the file.", null);

        var position = source.Lines[line].Start + character;
        if (position > source.Length) return new OriginTier("foreign", "This origin is past the end of the file.", null);

        var node = root.FindToken(position).Parent;
        if (node is null) return new OriginTier("foreign", "Nothing in this file sits at that position.", null);

        // A LOCAL FUNCTION is its own member for this purpose: a node built there is written once and
        // reached by a call, exactly like a helper method.
        foreach (var ancestor in node.AncestorsAndSelf())
        {
            switch (ancestor)
            {
                case LocalFunctionStatementSyntax local:
                    return new OriginTier("foreign",
                        $"Built by the local function {local.Identifier.Text}(), not here.", local.Identifier.Text);

                case ForEachStatementSyntax:
                case ForStatementSyntax:
                case WhileStatementSyntax:
                case DoStatementSyntax:
                    return new OriginTier("derived",
                        "Built inside a loop — every repetition comes from this one expression, so there is no single row to move or delete.",
                        null);

                case IfStatementSyntax:
                case SwitchStatementSyntax:
                case SwitchExpressionSyntax:
                case ConditionalExpressionSyntax:
                    return new OriginTier("derived",
                        "Built inside a conditional — it exists only when that branch is taken.", null);

                case SimpleLambdaExpressionSyntax:
                case ParenthesizedLambdaExpressionSyntax:
                case AnonymousMethodExpressionSyntax:
                    return new OriginTier("derived",
                        "Built inside a callback — it exists only when that callback runs.", null);

                case MethodDeclarationSyntax method:
                    return method.Identifier.Text == "Build"
                        ? new OriginTier("literal", "Built unconditionally — safe to edit in place.", null)
                        : new OriginTier("foreign",
                            $"Built by {method.Identifier.Text}(), not by Build().", method.Identifier.Text);

                case PropertyDeclarationSyntax property:
                    return new OriginTier("foreign",
                        $"Built by the {property.Identifier.Text} property, not by Build().", property.Identifier.Text);
            }
        }

        return new OriginTier("foreign", "Built outside any member this tool can place.", null);
    }

    /// <summary>
    /// What the node an origin names IS, and what could be set on it.
    /// <para>
    /// Read from the semantic model rather than from a generated catalogue, because the question is
    /// about THIS call: which parameters it actually supplies, what it wrote for them, and which of
    /// the type's members the form it is written in can even reach. A catalogue answers "what does
    /// Button have"; the panel needs "what does this Button say, and what may I change".
    /// </para>
    /// </summary>
    public InspectResult? Inspect(string path, string text, string origin)
    {
        if (_compilation is null) throw new InvalidOperationException("initialize first");

        // ONE resolution, shared with SetProperty. There were two, written weeks apart, and only one
        // of them got the getInnermostNodeForTie fix — so the panel kept naming a FormInput "Void"
        // long after the edit path had stopped doing it. Two copies of "which node is this" is two
        // answers to the same question.
        if (Locate(path, text, origin) is not var (_, _, construction, model) || construction is null) return null;

        var symbol = model.GetSymbolInfo(construction).Symbol as IMethodSymbol;
        if (symbol is null) return null;

        // A factory is a method NAMED like the type it returns; a constructor carries the type
        // itself. Both end at the same component — the difference is only what may be reached.
        var isFactory = construction is InvocationExpressionSyntax;
        var component = isFactory ? symbol.ReturnType : symbol.ContainingType;
        var initializer = (construction as ObjectCreationExpressionSyntax)?.Initializer;

        var arguments = construction switch
        {
            ObjectCreationExpressionSyntax creation => creation.ArgumentList?.Arguments,
            InvocationExpressionSyntax invocation => invocation.ArgumentList.Arguments,
            _ => null,
        };

        var properties = new List<NodeProperty>();
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < symbol.Parameters.Length; i++)
        {
            var parameter = symbol.Parameters[i];
            covered.Add(parameter.Name);
            // A child is STRUCTURE, not a value. Offering `child` as a text box invites editing a
            // whole subtree as a string, which is the one edit most likely to destroy something —
            // and the tree is navigated on the canvas, by clicking into it, not typed here.
            if (IsStructural(parameter.Type)) continue;

            var written = arguments is null ? null : ArgumentFor(arguments.Value, parameter, i);
            properties.Add(new NodeProperty(
                parameter.Name,
                parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                written is null ? "unset" : "argument",
                written?.Expression.ToString(),
                true,
                null,
                OptionsFor(parameter.Type),
                ParameterSummary(parameter)));
        }

        // Settable members the constructor did not already cover, INHERITED ONES INCLUDED: a Row's
        // Width and Height live on FlexNode and VisualNode, and GetMembers only answers for the type
        // it is asked about — so the first version of this listed none of the properties an author
        // most often reaches for. On a factory call there is no initializer to put them in, and
        // inventing one would rewrite the form the author chose.
        foreach (var property in Inherited(component).OfType<IPropertySymbol>())
        {
            if (property.DeclaredAccessibility != Accessibility.Public) continue;
            if (property.IsStatic || property.SetMethod is null) continue;
            // The design stamp is the TOOL's, not the author's. Offering Origin as an editable
            // property would be the inspector inviting someone to edit its own scaffolding.
            if (property.Name is "Origin" or "OriginLabel") continue;
            if (!covered.Add(property.Name)) continue;
            if (IsStructural(property.Type)) continue;

            var written = initializer?.Expressions
                .OfType<AssignmentExpressionSyntax>()
                .FirstOrDefault(a => a.Left.ToString() == property.Name);

            properties.Add(new NodeProperty(
                property.Name,
                property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                written is null ? "unset" : "initializer",
                written?.Right.ToString(),
                written is not null || initializer is not null,
                written is not null || initializer is not null
                    ? null
                    : isFactory
                        ? "Set through an object initializer, which this factory call does not have. Rewriting it into `new` form is a change to how the file is authored, so it is not done for you."
                        : "This construction has no object initializer to set it in.",
                OptionsFor(property.Type),
                Summary(property)));
        }

        var (childCount, insertReason) = ChildrenList(symbol, arguments) switch
        {
            { } list => (list.Elements.Count, (string?)null),
            _ => (-1, InsertRefusal(symbol, arguments)),
        };

        return new InspectResult(
            component.Name,
            isFactory ? "factory" : "new",
            Summary(component),
            properties.ToArray(),
            childCount,
            insertReason);
    }

    /// <summary>
    /// Sets one property on the node an origin names, as a TEXT REPLACEMENT for the editor to apply.
    /// <para>
    /// Three shapes are supported and nothing else: replace an argument that is already written,
    /// add a named argument for a constructor parameter the call omitted, and replace a member in an
    /// object initializer that already exists. What is deliberately NOT here is form transformation —
    /// turning <c>Row(…)</c> into <c>new Row(…) { … }</c> so a field becomes editable would rewrite
    /// how the author's file is authored, and that is their decision, not the panel's.
    /// </para>
    /// </summary>
    public EditResult SetProperty(string path, string text, string origin, string property, string value)
    {
        if (_compilation is null) throw new InvalidOperationException("initialize first");

        // The value is C# and goes into a C# file; a fragment that does not parse would be written
        // verbatim and break the document.
        var parsedValue = SyntaxFactory.ParseExpression(value);
        if (parsedValue.ContainsDiagnostics || parsedValue.ToString().Trim() != value.Trim())
            return EditResult.Refused($"'{value}' is not a C# expression.");

        var located = Locate(path, text, origin);
        if (located is not var (tree, source, construction, model) || construction is null)
            return EditResult.Refused("That element's origin does not name anything in this file.");

        if (model.GetSymbolInfo(construction).Symbol is not IMethodSymbol symbol)
            return EditResult.Refused("The compiler cannot resolve that construction.");

        var arguments = construction switch
        {
            ObjectCreationExpressionSyntax creation => creation.ArgumentList?.Arguments,
            InvocationExpressionSyntax invocation => invocation.ArgumentList.Arguments,
            _ => null,
        };

        // `style.Padding` — a member of the value an argument carries. See SetNested.
        if (property.Split('.') is [var argumentName, var member])
        {
            return SetNested(source, arguments, symbol, argumentName, member, value, text, path);
        }

        // 1. An argument already written — replace just its expression, so trivia and the name colon
        //    survive untouched.
        var parameterIndex = -1;
        for (var i = 0; i < symbol.Parameters.Length; i++)
        {
            if (symbol.Parameters[i].Name == property) { parameterIndex = i; break; }
        }

        if (parameterIndex >= 0 && arguments is { } list
            && ArgumentFor(list, symbol.Parameters[parameterIndex], parameterIndex) is { } written)
        {
            return Replace(source, written.Expression.Span, value, text, path);
        }

        // 2. A member already in an object initializer.
        var initializer = (construction as ObjectCreationExpressionSyntax)?.Initializer;
        if (initializer?.Expressions.OfType<AssignmentExpressionSyntax>()
                .FirstOrDefault(a => a.Left.ToString() == property) is { } assignment)
        {
            return Replace(source, assignment.Right.Span, value, text, path);
        }

        // 3. A constructor parameter the call omitted — added NAMED, and placed before a trailing
        //    `children:` so the tree stays last, which is how every screen in the repo is written.
        if (parameterIndex >= 0 && arguments is { } existing)
        {
            var children = existing.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == "children");
            var addition = $"{property}: {value}";

            if (children is not null)
            {
                return Insert(source, children.SpanStart, addition + ", ", text, path);
            }

            return existing.Count == 0
                ? Insert(source, construction.Span.End - 1, addition, text, path)
                : Insert(source, existing[^1].Span.End, ", " + addition, text, path);
        }

        return EditResult.Refused(
            $"'{property}' is not a constructor parameter of {symbol.ContainingType?.Name ?? "this component"} "
            + "and there is no object initializer to set it in. Adding one would change how this file is written.");
    }

    /// <summary>
    /// A member of a VALUE an argument carries — <c>style.Padding</c>, where <c>style</c> is the
    /// argument and <c>Padding</c> a member of the <c>BoxStyle</c> written into it.
    /// <para>
    /// This exists because the things an author most wants to change — padding, background, corner
    /// radius — are not on the node at all; they are on a <c>BoxStyle</c>, which is data rather than
    /// tree and therefore carries no origin of its own. Without descending one level, the panel could
    /// offer a Box's <c>gap</c> and nothing anyone came for.
    /// </para>
    /// <para>
    /// One level, and only into an initializer that already exists: the same fence as everywhere else
    /// in this phase. <c>BoxStyle</c> is the case it was built for, and it is a <c>readonly record
    /// struct</c> with no positional parameters, so every member of it is a plain add-or-replace with
    /// no ordinal arithmetic anywhere.
    /// </para>
    /// </summary>
    private EditResult SetNested(
        SourceText source, SeparatedSyntaxList<ArgumentSyntax>? arguments, IMethodSymbol symbol,
        string argumentName, string member, string value, string text, string path)
    {
        if (arguments is not { } list) return EditResult.Refused("That construction takes no arguments.");

        var index = -1;
        for (var i = 0; i < symbol.Parameters.Length; i++)
        {
            if (symbol.Parameters[i].Name == argumentName) { index = i; break; }
        }
        if (index < 0) return EditResult.Refused($"'{argumentName}' is not a parameter of {symbol.Name}.");

        if (ArgumentFor(list, symbol.Parameters[index], index)?.Expression is not ObjectCreationExpressionSyntax written)
            return EditResult.Refused($"'{argumentName}' is not written as a value this panel can edit in place.");

        if (written.Initializer is null)
            return EditResult.Refused(
                $"The {argumentName} here has no object initializer to set '{member}' in. "
                + "Adding one would change how this file is written.");

        var assignment = written.Initializer.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault(a => a.Left.ToString() == member);

        if (assignment is not null) return Guarded(source, assignment.Right.Span, value, text, path);

        // Appended to the initializer that is already there — never one invented for the purpose.
        var last = written.Initializer.Expressions.LastOrDefault();
        return last is null
            ? Guarded(source, new TextSpan(written.Initializer.OpenBraceToken.Span.End, 0), $" {member} = {value}", text, path)
            : Guarded(source, new TextSpan(last.Span.End, 0), $", {member} = {value}", text, path);
    }

    private EditResult Replace(SourceText source, Microsoft.CodeAnalysis.Text.TextSpan span, string value, string text, string path) =>
        Guarded(source, span, value, text, path);

    private EditResult Insert(SourceText source, int position, string value, string text, string path) =>
        Guarded(source, new Microsoft.CodeAnalysis.Text.TextSpan(position, 0), value, text, path);

    /// <summary>
    /// The edit, checked before it is offered: the whole file is re-parsed with the change applied,
    /// and if that introduces a C# error the file did not already have, the edit is refused instead
    /// of returned. A panel that writes a broken file and lets the preview report it has still
    /// broken the file.
    /// </summary>
    private EditResult Guarded(
        SourceText source, Microsoft.CodeAnalysis.Text.TextSpan span, string value, string text, string path)
    {
        // Compared as a MULTISET of (code, message), never by count and never by position: an edit
        // shifts every line after it, and a file that already had an error would otherwise have that
        // error reported back as though this edit had caused it.
        var before = _compiler.GetLanguageErrors(text, path)
            .GroupBy(e => (e.Code, e.Message))
            .ToDictionary(g => g.Key, g => g.Count());

        var introduced = _compiler.GetLanguageErrors(source.Replace(span, value).ToString(), path)
            .GroupBy(e => (e.Code, e.Message))
            .Select(g => (g.Key, Added: g.Count() - (before.TryGetValue(g.Key, out var had) ? had : 0), First: g.First()))
            .FirstOrDefault(entry => entry.Added > 0);

        if (introduced.First is { } error)
        {
            return EditResult.Refused($"That would not compile: {error.Code} {error.Message}.");
        }

        var start = source.Lines.GetLinePosition(span.Start);
        var end = source.Lines.GetLinePosition(span.End);
        return new EditResult(true, null, start.Line, start.Character, end.Line, end.Character, value);
    }

    /// <summary>The construction an origin names, with the model that can answer for it.</summary>
    private (SyntaxTree Tree, SourceText Source, SyntaxNode? Construction, SemanticModel Model)? Locate(
        string path, string text, string origin)
    {
        var parts = origin.Split('|');
        if (parts.Length != 3 || !SamePath(parts[0], path)) return null;

        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        var source = tree.GetText();
        var (startLine, startCharacter) = ParsePosition(parts[1]);
        var (endLine, endCharacter) = ParsePosition(parts[2]);
        if (startLine >= source.Lines.Count || endLine >= source.Lines.Count) return null;

        var start = source.Lines[startLine].Start + startCharacter;
        var end = source.Lines[endLine].Start + endCharacter;
        if (start > source.Length || end > source.Length || end < start) return null;

        // getInnermostNodeForTie, and it is not a nicety. `card.Add(new FormInput(…))` gives the
        // ArgumentSyntax exactly the same span as the construction inside it, and FindNode's default
        // returns the OUTERMOST of a tie — so the walk up landed on `Add(…)`, whose symbol returns
        // void. The panel then introduced a FormInput as "Void" and offered to edit `child`, the
        // Add parameter. Every node that is a call's only argument had this.
        var construction = tree.GetRoot()
            .FindNode(TextSpan.FromBounds(start, end), getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .FirstOrDefault(n => n is ObjectCreationExpressionSyntax or InvocationExpressionSyntax);

        return (tree, source, construction, Swap(_current!, tree).GetSemanticModel(tree));
    }

    /// <summary>
    /// What a palette can offer, and the smallest call of each that compiles.
    /// <para>
    /// Derived from the factory surface in the COMPILATION rather than from a list someone typed:
    /// the surface is generated for the app's own components too, so a hand-written list would be
    /// permanently one component behind whatever the developer just wrote.
    /// </para>
    /// <para>
    /// A component whose required parameters cannot be filled with a literal is simply not offered.
    /// Refusing to guess is the point — a palette that inserts something not compiling has broken the
    /// file to show a menu.
    /// </para>
    /// </summary>
    public PaletteEntry[] Palette()
    {
        if (_current is null) throw new InvalidOperationException("initialize first");

        var surface = _current.GetTypeByMetadataName("eQuantic.UI.Components.UI");
        if (surface is null) return [];

        var entries = new List<PaletteEntry>();
        foreach (var method in surface.GetMembers().OfType<IMethodSymbol>())
        {
            if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public) continue;
            if (!method.ReturnType.IsVisualNode()) continue;
            if (Snippet(method) is not { } snippet) continue;

            entries.Add(new PaletteEntry(method.Name, snippet, Summary(method.ReturnType)));
        }

        return entries.DistinctBy(e => e.Name).OrderBy(e => e.Name, StringComparer.Ordinal).ToArray();
    }

    /// <summary>The smallest call of a factory that compiles, or null when one cannot be written.</summary>
    private static string? Snippet(IMethodSymbol factory)
    {
        var arguments = new List<string>();
        foreach (var parameter in factory.Parameters)
        {
            if (parameter.HasExplicitDefaultValue || parameter.IsParams) continue;

            var literal = Literal(parameter.Type, factory.Name);
            if (literal is null) return null;
            arguments.Add(literal);
        }

        return $"{factory.Name}({string.Join(", ", arguments)})";
    }

    /// <summary>
    /// A literal for a required parameter. Deliberately narrow: a type this cannot write is a
    /// component the palette does not offer, which is better than offering one that will not compile.
    /// </summary>
    private static string? Literal(ITypeSymbol type, string componentName)
    {
        var bare = type is INamedTypeSymbol { Name: "Nullable" } nullable && nullable.TypeArguments.Length == 1
            ? nullable.TypeArguments[0]
            : type;

        // A child slot gets something VISIBLE. An empty Box inside a Card is indistinguishable from
        // the insert having done nothing at all.
        if (bare.IsVisualNode()) return $"Text(\"{componentName}\")";

        if (bare.TypeKind == TypeKind.Enum)
        {
            var first = bare.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(f => f.HasConstantValue);
            return first is null ? null : $"{bare.Name}.{first.Name}";
        }

        return bare.SpecialType switch
        {
            SpecialType.System_String => $"\"{componentName}\"",
            SpecialType.System_Boolean => "false",
            SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Int16 => "0",
            SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal => "0",
            _ => null,
        };
    }

    /// <summary>
    /// Inserts a child into a declarative <c>children: [ … ]</c> at a position.
    /// <para>
    /// Anchored on an ELEMENT's span, never on the brackets: the list is written multi-line with a
    /// trailing comma in every screen in this repo, and a naive splice before the <c>]</c> lands
    /// after that comma and produces a list with a hole in it. Inserting after element N-1 puts the
    /// text exactly where a person would have typed it.
    /// </para>
    /// </summary>
    public EditResult InsertChild(string path, string text, string origin, int index, string snippet)
    {
        if (_compilation is null) throw new InvalidOperationException("initialize first");

        var parsed = SyntaxFactory.ParseExpression(snippet);
        if (parsed.ContainsDiagnostics) return EditResult.Refused($"'{snippet}' is not a C# expression.");

        if (Locate(path, text, origin) is not var (_, source, construction, model) || construction is null)
            return EditResult.Refused("That element's origin does not name anything in this file.");

        if (model.GetSymbolInfo(construction).Symbol is not IMethodSymbol symbol)
            return EditResult.Refused("The compiler cannot resolve that container.");

        var arguments = construction switch
        {
            ObjectCreationExpressionSyntax creation => creation.ArgumentList?.Arguments,
            InvocationExpressionSyntax invocation => invocation.ArgumentList.Arguments,
            _ => null,
        };

        if (ChildrenList(symbol, arguments) is not { } children)
        {
            return EditResult.Refused(InsertRefusal(symbol, arguments)
                ?? $"{symbol.Name} does not take a list of children.");
        }

        var elements = children.Elements;
        var position = Math.Clamp(index, 0, elements.Count);

        // The indentation of the element it is going beside, so the inserted line reads as one the
        // author wrote rather than one a tool dropped in.
        var anchor = elements.Count == 0 ? null : elements[Math.Min(position, elements.Count - 1)];
        var indent = anchor is null ? "" : Indentation(source, anchor.SpanStart);

        if (elements.Count == 0)
        {
            return Guarded(source, new TextSpan(children.OpenBracketToken.Span.End, 0), snippet, text, path);
        }

        return position == elements.Count
            ? Guarded(source, new TextSpan(elements[^1].Span.End, 0), $",\n{indent}{snippet}", text, path)
            : Guarded(source, new TextSpan(elements[position].SpanStart, 0), $"{snippet},\n{indent}", text, path);
    }

    /// <summary>The whitespace at the start of the line a position sits on.</summary>
    private static string Indentation(SourceText source, int position)
    {
        var line = source.Lines.GetLineFromPosition(position);
        var textOfLine = source.ToString(line.Span);
        return textOfLine[..(textOfLine.Length - textOfLine.TrimStart().Length)];
    }

    /// <summary>
    /// The declarative <c>children: [ … ]</c> of a container call, or null when there is not one.
    /// <para>
    /// A collection expression is the ONLY shape an insertion can be spliced into safely: its
    /// elements are a list with real spans, so a new one goes between two of them without touching
    /// anything else. The other two shapes a container is written in — a collection initializer, and
    /// <c>.Add(…)</c> statements — are not lists at all in the place that matters, and inserting into
    /// them is statement-level dataflow.
    /// </para>
    /// </summary>
    private static CollectionExpressionSyntax? ChildrenList(
        IMethodSymbol symbol, SeparatedSyntaxList<ArgumentSyntax>? arguments)
    {
        if (arguments is not { } list) return null;

        for (var i = 0; i < symbol.Parameters.Length; i++)
        {
            if (symbol.Parameters[i].Name != "children") continue;
            return ArgumentFor(list, symbol.Parameters[i], i)?.Expression as CollectionExpressionSyntax;
        }
        return null;
    }

    /// <summary>
    /// Why a container cannot take an insertion. ALWAYS a reason when there is no list — an
    /// affordance that appears and then refuses reads as a bug, so the panel needs to know before it
    /// offers anything.
    /// </summary>
    private static string InsertRefusal(IMethodSymbol symbol, SeparatedSyntaxList<ArgumentSyntax>? arguments)
    {
        // A CONSTRUCTOR takes only the layout knobs — children are appended by the factory, or added
        // by statements afterwards. So `new Column(gap: …)` has no children parameter at all, and the
        // first version of this said nothing about the commonest container in the repo.
        if (!symbol.Parameters.Any(p => p.Name == "children"))
        {
            // A constructor's own Name is ".ctor"; what the reader recognises is the type.
            var named = symbol.MethodKind == MethodKind.Constructor
                ? symbol.ContainingType?.Name ?? symbol.Name
                : symbol.Name;
            return $"This {named} is written as `new {named}(…)`, whose "
                + "children are added one statement at a time. Inserting there is a change to the method's flow, "
                + "not to a list, so it is not done for you.";
        }

        return arguments is { } list && list.Any(a => a.NameColon?.Name.Identifier.Text == "children")
            ? "This container's children are not written as a [ … ] list, so there is no list to insert into."
            : "This container was called without a children list. Adding one would change how it is written, "
                + "so it is not done for you.";
    }

    /// <summary>A type's own members and every base's, nearest first — <c>object</c> excluded, which
    /// contributes nothing an inspector would show.</summary>
    private static IEnumerable<ISymbol> Inherited(ITypeSymbol type)
    {
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var member in current.GetMembers()) yield return member;
        }
    }

    /// <summary>The argument bound to a parameter — by NAME first, because the declarative surface is
    /// written with named arguments and their order is the author's, not the signature's.</summary>
    private static ArgumentSyntax? ArgumentFor(
        SeparatedSyntaxList<ArgumentSyntax> arguments, IParameterSymbol parameter, int position)
    {
        var named = arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == parameter.Name);
        if (named is not null) return named;

        // Positional only up to the first named one: after that, position means nothing.
        var firstNamed = arguments.IndexOf(a => a.NameColon is not null);
        var positional = firstNamed < 0 ? arguments.Count : firstNamed;
        return position < positional ? arguments[position] : null;
    }

    /// <summary>
    /// Whether a member holds TREE rather than a value — a node, or a collection of them.
    /// <para>
    /// Those are navigated, never typed: a text box over a subtree is an invitation to replace a
    /// screen with a typo. The canvas already offers the gesture that belongs here — click the child.
    /// </para>
    /// </summary>
    private static bool IsStructural(ITypeSymbol type)
    {
        var bare = type is INamedTypeSymbol { Name: "Nullable" } nullable && nullable.TypeArguments.Length == 1
            ? nullable.TypeArguments[0]
            : type;

        if (bare is IArrayTypeSymbol array) return array.ElementType.IsVisualNode();
        if (bare is INamedTypeSymbol { TypeArguments.Length: 1 } generic && generic.TypeArguments[0].IsVisualNode())
            return true;

        return bare.IsVisualNode();
    }

    /// <summary>An enum's members — the closed set a panel offers instead of a text box.</summary>
    private static string[]? OptionsFor(ITypeSymbol type)
    {
        var underlying = type is INamedTypeSymbol { Name: "Nullable" } nullable && nullable.TypeArguments.Length == 1
            ? nullable.TypeArguments[0]
            : type;

        // QUALIFIED — `CrossAlign.Center`, not `Center`. What the panel offers is what gets written
        // into the file, so a bare member name would be a pick that cannot compile.
        return underlying.TypeKind == TypeKind.Enum
            ? underlying.GetMembers().OfType<IFieldSymbol>()
                .Where(f => f.HasConstantValue)
                .Select(f => $"{underlying.Name}.{f.Name}")
                .ToArray()
            : null;
    }

    /// <summary>
    /// The <c>&lt;summary&gt;</c> of a symbol's doc comment, flattened to one line.
    /// <para>
    /// It comes from wherever the symbol does: the app's OWN components are in this compilation as
    /// source, so their prose is in the tree; the framework's arrive as metadata, and Roslyn can only
    /// read their comments from the XML file beside the assembly — which is why the framework builds
    /// one. A component whose project does not is simply undocumented here, never wrong.
    /// </para>
    /// </summary>
    private static string? Summary(ISymbol symbol) =>
        Prose(symbol.GetDocumentationCommentXml(), "summary");

    /// <summary>
    /// A parameter's description, which lives in its METHOD's doc comment as
    /// <c>&lt;param name="gap"&gt;</c> — asking the parameter symbol for its own XML returns nothing,
    /// so the first version of this showed a constructor's summary against every one of its
    /// parameters, or more often nothing at all.
    /// </summary>
    private static string? ParameterSummary(IParameterSymbol parameter) =>
        Prose(parameter.ContainingSymbol?.GetDocumentationCommentXml(), $"param name=\"{parameter.Name}\"", "param");

    private static string? Prose(string? xml, string open, string? close = null)
    {
        if (string.IsNullOrWhiteSpace(xml)) return null;

        var match = Regex.Match(xml, $"<{Regex.Escape(open)}>(.*?)</{close ?? open}>", RegexOptions.Singleline);
        if (!match.Success) return null;

        var prose = Regex.Replace(match.Groups[1].Value, @"<[^>]+>", "");
        prose = Regex.Replace(prose, @"\s+", " ").Trim();
        return prose.Length == 0 ? null : prose;
    }

    private static (int Line, int Character) ParsePosition(string value)
    {
        var parts = value.Split(':');
        return parts.Length == 2 && int.TryParse(parts[0], out var line) && int.TryParse(parts[1], out var character)
            ? (line, character)
            : (0, 0);
    }

    private static Compilation Swap(Compilation compilation, SyntaxTree tree)
    {
        var existing = compilation.SyntaxTrees.FirstOrDefault(t => SamePath(t.FilePath, tree.FilePath));
        return existing is null ? compilation.AddSyntaxTrees(tree) : compilation.ReplaceSyntaxTree(existing, tree);
    }

    private static bool SamePath(string? a, string? b) =>
        !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)
        && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Buffer in, browser-ready JavaScript out.
    /// <para>
    /// C# errors are checked FIRST and stop the compile. Roslyn parses leniently, so a missing brace
    /// still yields a tree the transpiler will happily walk, emitting a module that mounts and
    /// throws — which arrives on screen as a blank white frame with nothing to explain it. A missing
    /// semicolon has to read as "CS1002 on line 12".
    /// </para>
    /// </summary>
    public CompileResult Compile(string path, string text)
    {
        if (_compilation is null) throw new InvalidOperationException("initialize first");

        var watch = Stopwatch.StartNew();

        var languageErrors = _compiler.GetLanguageErrors(text, path);
        if (languageErrors.Count > 0)
        {
            watch.Stop();
            var marks = languageErrors
                .OrderBy(e => e.Line).ThenBy(e => e.Column)
                .Take(MaxReportedErrors)
                // CompilationError is 1-based and span-less; the editor's own model is 0-based, and
                // one character is the least that can be drawn.
                .Select(e => new DesignMark(e.Line - 1, e.Column - 1, e.Line - 1, e.Column, e.Message, e.Code, true))
                .ToArray();
            return new CompileResult(false, "", "", marks, (int)watch.ElapsedMilliseconds);
        }

        var modules = _compiler.CompileSource(text, path)
            .Where(r => r.Success && r.TypeScript.Length > 0)
            .ToList();

        if (modules.Count == 0)
        {
            watch.Stop();
            return new CompileResult(false, "", "", [new DesignMark(0, 0, 0, 1,
                "No component in this file. Declare a class extending StatefulComponent or StatelessComponent.",
                "EQD001", true)], (int)watch.ElapsedMilliseconds);
        }

        // One FILE yields one module per TYPE, so a page beside a record comes back as several. The
        // one to mount is the page, then the last class that extends something — never simply the
        // first, which is how a page declaring `record Row` mounted `Row` and reported
        // ".mount is not a function" while the page itself never loaded.
        var component = modules.FirstOrDefault(r => r.IsPage)
            ?? modules.LastOrDefault(r => r.TypeScript.Contains(" extends "))
            ?? modules[^1];

        var js = Bundle(modules, component);
        watch.Stop();
        return new CompileResult(true, js, component.ComponentName, [], (int)watch.ElapsedMilliseconds);
    }

    /// <summary>One broken token cascades into dozens of CS errors; past the first few they are
    /// noise, and the gutter cannot draw them anyway.</summary>
    private const int MaxReportedErrors = 8;

    private static readonly Regex RuntimeImport =
        new(@"^import\s*\{([^}]*)\}\s*from\s*""@equantic/runtime"";?", RegexOptions.Compiled);

    private static readonly Regex LocalImport =
        new(@"^import\s*\{([^}]*)\}\s*from\s*""\./([^""]+)"";?", RegexOptions.Compiled);

    /// <summary>
    /// The modules as ONE inline script. There is no server behind the preview to resolve
    /// <c>./StatTile</c> from, so a local import is not dropped but FOLLOWED: the file that declares
    /// the type is compiled too and inlined ahead of its user. Dependencies first and the mounted
    /// component last, because a class must be declared before the line that constructs it —
    /// <c>class</c> does not hoist the way <c>function</c> does.
    /// </summary>
    private string Bundle(IReadOnlyList<CompilationResult> modules, CompilationResult component)
    {
        var runtimeImports = new SortedSet<string>(StringComparer.Ordinal);
        var bodies = new List<string>();
        var emitted = new HashSet<string>(StringComparer.Ordinal);

        void Take(CompilationResult module, int depth)
        {
            if (!emitted.Add(module.ComponentName)) return;

            var body = new List<string>();
            foreach (var line in module.TypeScript.Split('\n'))
            {
                var trimmed = line.TrimStart();

                if (RuntimeImport.Match(trimmed) is { Success: true } runtime)
                {
                    foreach (var name in runtime.Groups[1].Value.Split(','))
                        if (name.Trim().Length > 0) runtimeImports.Add(name.Trim());
                    continue;
                }

                if (LocalImport.Match(trimmed) is { Success: true } local)
                {
                    // Depth-capped rather than cycle-checked alone: a component graph that points
                    // back at the page is legal C#, and the preview must not hang on it.
                    if (depth < MaxDependencyDepth)
                        foreach (var sibling in ResolveDependency(local.Groups[2].Value))
                            Take(sibling, depth + 1);
                    continue;
                }

                body.Add(line);
            }

            bodies.Add(string.Join("\n", body).Trim());
        }

        foreach (var module in modules.Where(m => !ReferenceEquals(m, component))) Take(module, 0);
        Take(component, 0);

        var header = runtimeImports.Count > 0
            ? $"import {{ {string.Join(", ", runtimeImports)} }} from \"@equantic/runtime\";\n\n"
            : "";
        return header + string.Join("\n\n", bodies) + "\n";
    }

    private const int MaxDependencyDepth = 8;

    /// <summary>
    /// The project file that declares a module the page imported, compiled WHOLE.
    /// <para>
    /// Whole, not just the type that was named: one C# file yields one module per TYPE, and a helper
    /// class almost always sits beside the records it hands back. Taking only the imported name
    /// inlined <c>ConsoleData</c> without the <c>Payment</c> record its own getter constructs, and
    /// the preview mounted and then died on "Payment is not defined".
    /// </para>
    /// <para>
    /// From DISK, deliberately: the buffer is the file being edited, and its neighbours are whatever
    /// was last saved — which is also what the running app would be using.
    /// </para>
    /// </summary>
    private List<CompilationResult> ResolveDependency(string moduleName)
    {
        // Asked of the COMPILATION, not of the filesystem. Matching a file NAME was wrong twice over:
        // a file may declare a type called something else, and a source generator's output is called
        // `SignUpForm.g.cs` and lives under obj/ — which the old search excluded on purpose, to avoid
        // build artifacts. Generated types are part of the program csc compiled, so a page that calls
        // one has to find it here or fail at runtime with "X is not defined".
        if (!_declaringFile.TryGetValue(moduleName, out var file)) return [];

        // An OPEN file is never cached by write time: its text changes without the file being touched,
        // which is exactly the case the cache would answer wrongly and never notice.
        var open = _open.TryGetValue(Path.GetFullPath(file), out var buffer);
        var stamp = File.GetLastWriteTimeUtc(file);
        if (!open && _dependencies.TryGetValue(file, out var cached) && cached.Stamp == stamp) return cached.Modules;

        var modules = _compiler.CompileSource(open ? buffer! : File.ReadAllText(file), file)
            .Where(r => r.Success && r.TypeScript.Length > 0)
            .ToList();

        if (!open) _dependencies[file] = (stamp, modules);
        return modules;
    }
}
