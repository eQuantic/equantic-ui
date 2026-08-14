using System.Diagnostics;
using System.Text;
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
                OptionsFor(parameter.Type, written?.Expression, model),
                ParameterSummary(parameter),
                MembersOf(written?.Expression as ObjectCreationExpressionSyntax, model)));
        }

        // Settable members the constructor did not already cover, INHERITED ONES INCLUDED: a Row's
        // Width and Height live on FlexNode and VisualNode, and GetMembers only answers for the type
        // it is asked about — so the first version of this listed none of the properties an author
        // most often reaches for. On a factory call there is no initializer to put them in, and
        // inventing one would rewrite the form the author chose.
        foreach (var property in Inherited(component).OfType<IPropertySymbol>())
        {
            if (!Writable(property)) continue;
            // The design stamp is the TOOL's, not the author's. Offering Origin as an editable
            // property would be the inspector inviting someone to edit its own scaffolding.
            if (property.Name is "Origin" or "OriginLabel") continue;
            if (!covered.Add(property.Name)) continue;
            if (IsStructural(property.Type)) continue;

            var written = initializer?.Expressions
                .OfType<AssignmentExpressionSyntax>()
                .FirstOrDefault(a => a.Left.ToString() == property.Name);

            // A `new X(…)` can always be given an initializer — the braces are how that form is
            // spelled, so adding them rewrites nothing. A FACTORY call cannot, and says why.
            var reachable = written is not null || initializer is not null || !isFactory;

            properties.Add(new NodeProperty(
                property.Name,
                property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                written is null ? "unset" : "initializer",
                written?.Right.ToString(),
                reachable,
                reachable
                    ? null
                    : "Set through an object initializer, which this factory call does not have. Rewriting it into `new` form is a change to how the file is authored, so it is not done for you.",
                OptionsFor(property.Type, written?.Right, model),
                Summary(property)));
        }

        var (childCount, insertReason) = ChildrenList(symbol, arguments) switch
        {
            { } list => (list.Elements.Count, (string?)null),
            _ => (-1, InsertRefusal(symbol, arguments)),
        };

        var (siblingIndex, siblingCount) = Sibling(construction);

        return new InspectResult(
            component.Name,
            isFactory ? "factory" : "new",
            Summary(component),
            properties.ToArray(),
            childCount,
            insertReason,
            siblingIndex,
            siblingCount,
            Lists(symbol, arguments),
            EnclosingConstruction(construction) is { } parent
            && model.GetSymbolInfo(parent).Symbol is IMethodSymbol enclosing
            && IsLayered(enclosing.MethodKind == MethodKind.Constructor
                ? enclosing.ContainingType
                : enclosing.ReturnType));
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
            return SetNested(source, arguments, symbol, argumentName, member, value, text, path, construction, origin);
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
            return Replace(source, written.Expression.Span, value, text, path, construction, origin);
        }

        // 2. A member already in an object initializer.
        var initializer = (construction as ObjectCreationExpressionSyntax)?.Initializer;
        if (initializer?.Expressions.OfType<AssignmentExpressionSyntax>()
                .FirstOrDefault(a => a.Left.ToString() == property) is { } assignment)
        {
            return Replace(source, assignment.Right.Span, value, text, path, construction, origin);
        }

        // 3. A constructor parameter the call omitted — added NAMED, and placed before a trailing
        //    `children:` so the tree stays last, which is how every screen in the repo is written.
        if (parameterIndex >= 0 && arguments is { } existing)
        {
            var children = existing.FirstOrDefault(a => a.NameColon?.Name.Identifier.Text == "children");
            var addition = $"{property}: {value}";

            if (children is not null)
            {
                return Insert(source, children.SpanStart, addition + ", ", text, path, construction, origin);
            }

            return existing.Count == 0
                ? Insert(source, construction.Span.End - 1, addition, text, path, construction, origin)
                : Insert(source, existing[^1].Span.End, ", " + addition, text, path, construction, origin);
        }

        // 4. A member of an initializer that is already there — appended, not invented.
        //
        // Without this, only the FIRST property of a `new` node could be set: the branch below adds
        // the braces, and every later one fell through to a refusal that also called it a factory
        // call. Setting two things on the same node is the ordinary case, not the edge one.
        if (construction is ObjectCreationExpressionSyntax open && open.Initializer is not null
            && Settable(symbol.ContainingType, property))
        {
            var last = open.Initializer.Expressions.LastOrDefault();
            return last is null
                ? Insert(source, open.Initializer.OpenBraceToken.Span.End, $" {property} = {value}", text, path, construction, origin)
                : Insert(source, last.Span.End, $", {property} = {value}", text, path, construction, origin);
        }

        // 5. A member of a `new X(…)` written without an initializer — so give it one.
        //
        // The fence was drawn in the wrong place. Rewriting a FACTORY call into `new Button("Save")
        // { … }` changes the form the author chose, and the factory surface exists precisely so
        // nobody writes one. But a construction ALREADY written with `new` has chosen that form: the
        // braces are how it is spelled, and adding them refuses nothing and rewrites nothing. Without
        // this, every property of every `new Box(…)` on the screen answered "not from here", which is
        // most of the properties on most of the screens in this repo.
        if (construction is ObjectCreationExpressionSyntax bare && bare.Initializer is null
            && Settable(symbol.ContainingType, property))
        {
            return Insert(source, bare.ArgumentList?.Span.End ?? bare.Type.Span.End,
                $" {{ {property} = {value} }}", text, path, construction, origin);
        }

        return EditResult.Refused(
            $"'{property}' is not a constructor parameter of {symbol.ContainingType?.Name ?? "this component"}, "
            + "and this is a factory call, which has no object initializer to set it in. Rewriting it into "
            + "`new` form is a change to how the file is authored, so it is not done for you.");
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
        string argumentName, string member, string value, string text, string path,
        SyntaxNode construction, string origin)
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

        // Written as `new BoxStyle()` with nothing in it — the braces are part of that same form.
        if (written.Initializer is null)
        {
            var at = written.ArgumentList?.Span.End ?? written.Type.Span.End;
            return Insert(source, at, $" {{ {member} = {value} }}", text, path, construction, origin);
        }

        var assignment = written.Initializer.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault(a => a.Left.ToString() == member);

        if (assignment is not null)
            return Replace(source, assignment.Right.Span, value, text, path, construction, origin);

        // Appended to the initializer that is already there — never one invented for the purpose.
        var last = written.Initializer.Expressions.LastOrDefault();
        return last is null
            ? Insert(source, written.Initializer.OpenBraceToken.Span.End, $" {member} = {value}", text, path, construction, origin)
            : Insert(source, last.Span.End, $", {member} = {value}", text, path, construction, origin);
    }

    private EditResult Replace(
        SourceText source, Microsoft.CodeAnalysis.Text.TextSpan span, string value, string text, string path,
        SyntaxNode? node = null, string? origin = null) =>
        Guarded(source, span, value, text, path, Moved(source, span, value, node, origin));

    private EditResult Insert(
        SourceText source, int position, string value, string text, string path,
        SyntaxNode? node = null, string? origin = null) =>
        Guarded(source, new Microsoft.CodeAnalysis.Text.TextSpan(position, 0), value, text, path,
            Moved(source, new Microsoft.CodeAnalysis.Text.TextSpan(position, 0), value, node, origin));

    /// <summary>
    /// Where the edited node ENDS UP when the edit happens INSIDE it.
    /// <para>
    /// An origin is a span, and an edit within a node changes the node's length: unsetting a member
    /// shortens it, so the span the caller is holding now runs past its end — and a span that
    /// overshoots resolves to the smallest node CONTAINING it, which is the parent. The panel would
    /// quietly start describing, and then editing, the container of the thing it was asked about.
    /// </para>
    /// <para>
    /// The start never moves (the edit is inside), so only the end shifts, by exactly the difference
    /// in length. Nothing is computed when the caller did not say which node it was editing.
    /// </para>
    /// </summary>
    private static string? Moved(
        SourceText source, Microsoft.CodeAnalysis.Text.TextSpan edit, string value, SyntaxNode? node, string? origin)
    {
        if (node is null || origin is null) return null;
        if (!node.Span.Contains(edit.Start)) return null;

        var after = source.Replace(edit, value);
        var end = node.Span.End + value.Length - edit.Length;
        return Where(after, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(node.SpanStart, Math.Max(node.SpanStart, end)), origin);
    }

    /// <summary>
    /// The edit, checked before it is offered: the whole file is re-parsed with the change applied,
    /// and if that introduces a C# error the file did not already have, the edit is refused instead
    /// of returned. A panel that writes a broken file and lets the preview report it has still
    /// broken the file.
    /// </summary>
    private EditResult Guarded(
        SourceText source, Microsoft.CodeAnalysis.Text.TextSpan span, string value, string text, string path,
        string? movedTo = null)
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
        return new EditResult(true, null, start.Line, start.Character, end.Line, end.Character, value, movedTo);
    }

    /// <summary>The construction an origin names, with the model that can answer for it.</summary>
    private (SyntaxTree Tree, SourceText Source, SyntaxNode? Construction, SemanticModel Model)? Locate(
        string path, string text, string origin)
    {
        var parts = origin.Split('|');
        if (parts.Length != 3 || !SamePath(parts[0], path)) return null;

        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        var source = tree.GetText();

        return (tree, source, Find(tree, source, origin), Swap(_current!, tree).GetSemanticModel(tree));
    }

    /// <summary>
    /// The construction an origin names, in a tree already parsed.
    /// <para>
    /// Separate from <see cref="Locate"/> because an edit with two ends — moving a node from one
    /// container into another — has to resolve both in the SAME tree. Parsing twice gives two nodes
    /// that describe the same text and are not each other, so "is this the list it is already in?"
    /// answers no and the move writes a duplicate.
    /// </para>
    /// </summary>
    private static SyntaxNode? Find(SyntaxTree tree, SourceText source, string origin)
    {
        var parts = origin.Split('|');
        if (parts.Length != 3) return null;

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
        return tree.GetRoot()
            .FindNode(TextSpan.FromBounds(start, end), getInnermostNodeForTie: true)
            .AncestorsAndSelf()
            .FirstOrDefault(n => n is ObjectCreationExpressionSyntax or InvocationExpressionSyntax);
    }

    /// <summary>Where a node sits among its parent's declarative children, and how many there are —
    /// (-1, 0) when it is not an element of such a list, which is most nodes.</summary>
    private static (int Index, int Count) Sibling(SyntaxNode construction)
    {
        // The element is the construction itself: a collection expression holds expressions directly,
        // with no argument or wrapper in between.
        if (construction.Parent is not ExpressionElementSyntax element
            || element.Parent is not CollectionExpressionSyntax list)
        {
            return (-1, 0);
        }

        return (list.Elements.IndexOf(element), list.Elements.Count);
    }

    /// <summary>
    /// Moves a node up or down among its siblings.
    /// <para>
    /// The two elements swap places and everything BETWEEN them stays exactly where it is — the
    /// comma, the newline, the indentation. Rewriting the whole list from its element texts would be
    /// simpler and would silently discard any comment sitting between two children, which is a note
    /// someone left for a reason.
    /// </para>
    /// <para>
    /// The imperfection this leaves is worth naming: a comment written ABOVE a child stays with the
    /// position rather than travelling with the child. Moving the text and leaving the note is the
    /// lesser wrong of the two, and the alternative loses the note entirely.
    /// </para>
    /// </summary>
    public EditResult MoveChild(string path, string text, string origin, int delta)
    {
        if (_compilation is null) throw new InvalidOperationException("initialize first");
        if (delta is not (-1 or 1)) return EditResult.Refused("A child moves one place at a time.");

        if (Locate(path, text, origin) is not var (_, source, construction, _) || construction is null)
            return EditResult.Refused("That element's origin does not name anything in this file.");

        if (construction.Parent is not ExpressionElementSyntax element
            || element.Parent is not CollectionExpressionSyntax list)
        {
            return EditResult.Refused(
                "This node is not an element of a [ … ] list, so there is no order to move it within.");
        }

        var index = list.Elements.IndexOf(element);
        var target = index + delta;
        if (target < 0 || target >= list.Elements.Count)
        {
            return EditResult.Refused(delta < 0 ? "It is already first." : "It is already last.");
        }

        return Reorder(source, list, index, target, text, path, origin);
    }

    /// <summary>
    /// Puts a node at a given position among its siblings, however far that is.
    /// <para>
    /// The one-place move is this with a target one step away. A drag needs the general form: a child
    /// dropped three positions down is one gesture and must be one edit, not three swaps and three
    /// entries in the undo stack.
    /// </para>
    /// <para>
    /// <paramref name="index"/> is the GAP the node was dropped into, counted the way
    /// <see cref="InsertChild"/> counts one: 0 is before the first child and <c>Count</c> is after the
    /// last. Where the node ends up is one less whenever it travelled downwards, because taking it out
    /// of its old place closes that gap — arithmetic that belongs here, where a test can reach it,
    /// rather than in a pointer handler where it can only be found by dragging things about.
    /// </para>
    /// </summary>
    public EditResult ReorderChild(string path, string text, string origin, int index)
    {
        if (_compilation is null) throw new InvalidOperationException("initialize first");

        if (Locate(path, text, origin) is not var (_, source, construction, _) || construction is null)
            return EditResult.Refused("That element's origin does not name anything in this file.");

        if (construction.Parent is not ExpressionElementSyntax element
            || element.Parent is not CollectionExpressionSyntax list)
        {
            return EditResult.Refused(
                "This node is not an element of a [ … ] list, so there is no order to move it within.");
        }

        if (index < 0 || index > list.Elements.Count)
            return EditResult.Refused($"There are {list.Elements.Count} children, so there is no gap {index}.");

        var from = list.Elements.IndexOf(element);
        var to = index > from ? index - 1 : index;
        // The gap before itself and the gap after itself are both "where it already is". Dropping a
        // node back where it started is not an edit, and writing the file to say so would put a
        // no-op in the undo stack.
        if (to == from) return EditResult.Refused("It is already there.");

        return Reorder(source, list, from, to, text, path, origin);
    }

    /// <summary>
    /// Rewrites the stretch of the list the move disturbs, keeping every SLOT where it is.
    /// <para>
    /// The elements between the two positions shift by one and the moved one takes the far end. What
    /// separates them — the comma, the newline, the indentation — is read back from its position and
    /// written to the same position, so the list keeps the shape the author gave it.
    /// </para>
    /// <para>
    /// The imperfection this leaves is worth naming: a comment written ABOVE a child stays with the
    /// position rather than travelling with the child. The alternative — rebuilding the list from its
    /// element texts — loses the comment entirely, which is the worse of the two wrongs.
    /// </para>
    /// </summary>
    private EditResult Reorder(
        SourceText source, CollectionExpressionSyntax list, int from, int to, string text, string path,
        string origin)
    {
        var first = Math.Min(from, to);
        var last = Math.Max(from, to);

        var order = new List<int>();
        if (from < to)
        {
            for (var i = first + 1; i <= last; i++) order.Add(i);
            order.Add(from);
        }
        else
        {
            order.Add(from);
            for (var i = first; i < last; i++) order.Add(i);
        }

        var rewritten = new StringBuilder();
        var moved = 0;
        for (var slot = 0; slot < order.Count; slot++)
        {
            if (order[slot] == from) moved = rewritten.Length;
            rewritten.Append(source.ToString(list.Elements[order[slot]].Span));
            if (slot + 1 < order.Count)
            {
                rewritten.Append(source.ToString(TextSpan.FromBounds(
                    list.Elements[first + slot].Span.End,
                    list.Elements[first + slot + 1].SpanStart)));
            }
        }

        var span = TextSpan.FromBounds(list.Elements[first].SpanStart, list.Elements[last].Span.End);

        // Where the node lands, read off the text this edit produces. Everything after the edit shifts,
        // but the moved node is INSIDE the replaced region, so its new offset is the region's start
        // plus how far into the rewrite it sits.
        var after = source.Replace(span, rewritten.ToString());
        var landed = TextSpan.FromBounds(
            span.Start + moved,
            span.Start + moved + list.Elements[from].Span.Length);

        return Guarded(source, span, rewritten.ToString(), text, path, Where(after, landed, origin));
    }

    /// <summary>An origin string for a span, keeping the SPELLING of the path the caller sent — the
    /// editor matches origins as strings, and a path normalised on the way through would come back as
    /// a node it does not recognise.</summary>
    private static string Where(SourceText source, TextSpan span, string origin)
    {
        // Clamped rather than trusted: this is arithmetic over a text that was just rewritten, and a
        // span past the end would throw where the whole point is to hand back a coordinate.
        var start = source.Lines.GetLinePosition(Math.Clamp(span.Start, 0, source.Length));
        var end = source.Lines.GetLinePosition(Math.Clamp(span.End, 0, source.Length));
        return $"{origin.Split('|')[0]}|{start.Line}:{start.Character}|{end.Line}:{end.Character}";
    }

    /// <summary>
    /// Moves a node out of one container's children and into another's.
    /// <para>
    /// A reorder cannot express this: the node leaves one list and joins a different one, which is a
    /// removal and an insertion. They are written as ONE replacement spanning both — not because the
    /// region between them is interesting, but because one span is one <c>WorkspaceEdit</c> and
    /// therefore one Ctrl+Z, and because two edits into the same document raise an ordering question
    /// that this simply does not have.
    /// </para>
    /// <para>
    /// The moved text is re-indented to its new depth. It is otherwise carried across verbatim — which
    /// is what makes the compile guard the real fence here: a node written against a local of the
    /// method it came from stops compiling in its new home, and the refusal quotes the compiler rather
    /// than inventing a rule.
    /// </para>
    /// </summary>
    public EditResult MoveAcross(string path, string text, string origin, string target, int index)
    {
        if (_compilation is null) throw new InvalidOperationException("initialize first");

        if (!SamePath(origin.Split('|')[0], target.Split('|')[0]))
        {
            return EditResult.Refused(
                "That container is written in another file. Moving a node between files would edit two "
                + "of them at once, which is not done for you.");
        }

        if (Locate(path, text, origin) is not var (tree, source, construction, model) || construction is null)
            return EditResult.Refused("That element's origin does not name anything in this file.");

        if (construction.Parent is not ExpressionElementSyntax element
            || element.Parent is not CollectionExpressionSyntax from)
        {
            return EditResult.Refused(
                "This node is not an element of a [ … ] list, so there is nothing to move it out of.");
        }

        // The same tree, so the two ends of this edit are comparable as nodes rather than as text.
        if (Find(tree, source, target) is not { } container)
            return EditResult.Refused("That container's origin does not name anything in this file.");

        if (model.GetSymbolInfo(container).Symbol is not IMethodSymbol symbol)
            return EditResult.Refused("The compiler cannot resolve that container.");

        var arguments = container switch
        {
            ObjectCreationExpressionSyntax creation => creation.ArgumentList?.Arguments,
            InvocationExpressionSyntax invocation => invocation.ArgumentList.Arguments,
            _ => null,
        };

        if (ChildrenList(symbol, arguments) is not { } into)
        {
            return EditResult.Refused(InsertRefusal(symbol, arguments)
                ?? $"{symbol.Name} does not take a list of children.");
        }

        if (into == from)
            return EditResult.Refused("It is already in that list — that is a reorder, not a move.");

        // Into its own subtree the node would have to contain itself. The syntax says so before any
        // of the text arithmetic below could produce something incoherent.
        if (element.Span.Contains(container.Span))
            return EditResult.Refused("A node cannot be moved inside itself.");

        // The re-indented text is what actually gets WRITTEN, so it is what the landed span has to be
        // measured against. Measuring the original overshoots by exactly the indentation the move
        // removed, and an origin that overshoots resolves to the smallest node containing it — the
        // new parent. The panel would then edit the container the node was just dropped into.
        var snippet = Reindented(
            source, element, source.ToString(element.Span),
            Indentation(source, element.SpanStart), ListIndentation(source, into));
        var removal = Removal(from.Elements, element);
        var (at, addition, offset) = Addition(source, into, index, snippet);

        if (removal.Contains(at) && at != removal.Start)
            return EditResult.Refused("That list is inside the node being moved.");

        // One contiguous region covering both ends, rewritten. The text between them is reproduced
        // exactly — this is a single replacement for the editor's sake, not a rewrite of what it spans.
        var changes = new[] { new TextChange(removal, ""), new TextChange(new TextSpan(at, 0), addition) };
        var after = source.WithChanges(changes);

        var region = TextSpan.FromBounds(Math.Min(removal.Start, at), Math.Max(removal.End, at));
        var shifted = TextSpan.FromBounds(region.Start, region.End + addition.Length - removal.Length);

        return Guarded(
            source, region, after.ToString(shifted), text, path,
            Where(after, new TextSpan(
                (at < removal.Start ? at : at - removal.Length) + offset, snippet.Length), origin));
    }

    /// <summary>The span a child occupies INCLUDING one separator — the comma after it, or the comma
    /// before it when it is last. Shared by remove and by move, which take the same bite.</summary>
    private static TextSpan Removal<T>(SeparatedSyntaxList<T> list, T element) where T : SyntaxNode
    {
        var index = list.IndexOf(element);
        var separators = list.GetSeparators().ToArray();

        if (index + 1 < list.Count)
            return TextSpan.FromBounds(element.SpanStart, list[index + 1].SpanStart);

        // The last of several: back to the comma before it, so none is left orphaned.
        if (index > 0) return TextSpan.FromBounds(separators[index - 1].SpanStart, element.Span.End);

        // The ONLY one, and every list in this repo is written with a trailing comma — so taking the
        // element alone leaves `[ , ]`, which does not parse. Taking the comma too leaves `[ ]`.
        return TextSpan.FromBounds(
            element.SpanStart, separators.Length > 0 ? separators[0].Span.End : element.Span.End);
    }

    /// <summary>
    /// Where a new element goes in a list and the text that puts it there, with the offset of the
    /// element itself inside that text — which is what says where the node LANDED once it is written.
    /// </summary>
    private static (int At, string Text, int Offset) Addition(
        SourceText source, CollectionExpressionSyntax list, int index, string snippet)
    {
        var elements = list.Elements;
        if (elements.Count == 0) return (list.OpenBracketToken.Span.End, snippet, 0);

        var position = Math.Clamp(index, 0, elements.Count);

        // A list written on ONE line stays on one line. `columns: [Flex(), Fixed(120)]` is how a Grid
        // is written and how it reads, and breaking it across lines to add a track would reformat the
        // author's file to make room for a tool.
        if (source.Lines.GetLinePosition(list.SpanStart).Line == source.Lines.GetLinePosition(list.Span.End).Line)
        {
            return position == elements.Count
                ? (elements[^1].Span.End, $", {snippet}", 2)
                : (elements[position].SpanStart, $"{snippet}, ", 0);
        }

        var indent = Indentation(source, elements[Math.Min(position, elements.Count - 1)].SpanStart);
        return position == elements.Count
            ? (elements[^1].Span.End, $",\n{indent}{snippet}", 2 + indent.Length)
            : (elements[position].SpanStart, $"{snippet},\n{indent}", 0);
    }

    /// <summary>The indentation the children of a list are written at — its first element's, or the
    /// line the list opens on when it has none.</summary>
    private static string ListIndentation(SourceText source, CollectionExpressionSyntax list) =>
        Indentation(source, list.Elements.Count == 0 ? list.SpanStart : list.Elements[0].SpanStart);

    /// <summary>
    /// The same text at a new depth. The first line carries no indentation of its own — a span starts
    /// at the token — so only the continuation lines are re-based, and only where they actually begin
    /// with the depth they came from. Anything deeper keeps its own nesting, which is the whole point
    /// of moving the prefix rather than trimming each line.
    /// </summary>
    private static string Reindented(
        SourceText source, SyntaxNode element, string moved, string was, string now)
    {
        if (was == now || was.Length == 0) return moved;

        // A line that BEGINS inside a token spanning a line break is not layout. The leading
        // whitespace of a verbatim or raw string literal is part of what the component renders, and
        // re-basing it changes the text on the screen — invisibly, because the file still compiles
        // and the compile guard has nothing to object to.
        var literals = element.DescendantTokens()
            .Where(token => token.Text.Contains('\n'))
            .Select(token => token.Span)
            .ToArray();

        var original = moved.Split('\n');
        var lines = (string[])original.Clone();
        var offset = element.SpanStart;

        for (var i = 0; i < original.Length; i++)
        {
            var inside = literals.Any(span => span.Start < offset && offset < span.End);
            if (i > 0 && !inside && original[i].StartsWith(was, StringComparison.Ordinal))
                lines[i] = now + original[i][was.Length..];

            // Advanced by the ORIGINAL line, which is what the spans above are measured against.
            offset += original[i].Length + 1;
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Removes a node from its parent's children.
    /// <para>
    /// The element goes and so does ONE separator — the comma after it, or the comma before it when
    /// it is the last. Taking the element alone leaves a stray comma; taking both leaves a hole.
    /// </para>
    /// </summary>
    public EditResult RemoveChild(string path, string text, string origin)
    {
        if (_compilation is null) throw new InvalidOperationException("initialize first");

        if (Locate(path, text, origin) is not var (_, source, construction, _) || construction is null)
            return EditResult.Refused("That element's origin does not name anything in this file.");

        if (construction.Parent is not ExpressionElementSyntax element
            || element.Parent is not CollectionExpressionSyntax list)
        {
            return EditResult.Refused("This node is not an element of a [ … ] list, so there is nothing to remove it from.");
        }

        // Up to the NEXT element when there is one, so the line and its indentation go together;
        // back to the previous separator when this is the last, so no comma is orphaned.
        return Guarded(source, Removal(list.Elements, element), "", text, path);   // the node itself GOES
    }

    /// <summary>
    /// Removes the element at a position of a named list.
    /// <para>
    /// Addressed by INDEX rather than by origin, because the things in a list are not all nodes: a
    /// <c>GridTrack</c> is data, it never renders, so nothing on the canvas carries its span. The
    /// panel points at the container and says which one.
    /// </para>
    /// </summary>
    public EditResult RemoveAt(string path, string text, string origin, string list, int index)
    {
        if (_compilation is null) throw new InvalidOperationException("initialize first");

        if (Locate(path, text, origin) is not var (_, source, construction, model) || construction is null)
            return EditResult.Refused("That element's origin does not name anything in this file.");

        if (model.GetSymbolInfo(construction).Symbol is not IMethodSymbol symbol)
            return EditResult.Refused("The compiler cannot resolve that construction.");

        var arguments = construction switch
        {
            ObjectCreationExpressionSyntax creation => creation.ArgumentList?.Arguments,
            InvocationExpressionSyntax invocation => invocation.ArgumentList.Arguments,
            _ => null,
        };

        if (ListArgument(symbol, arguments, list) is not { } written)
            return EditResult.Refused($"This {symbol.Name} has no `{list}: [ … ]` written on it.");

        if (index < 0 || index >= written.Elements.Count)
            return EditResult.Refused($"`{list}` has {written.Elements.Count} entries, so there is no {index}.");

        var bite = Removal(written.Elements, written.Elements[index]);
        return Guarded(source, bite, "", text, path, Moved(source, bite, "", construction, origin));
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
    public PaletteEntry[] Palette(string? path = null, string? text = null, string? origin = null, string? list = null)
    {
        if (_current is null) throw new InvalidOperationException("initialize first");

        // A list of DATA is offered its own type's ways of being written, not the component surface:
        // `columns: [ … ]` takes GridTracks, and offering it a Button would be a palette that has not
        // read the signature it is filling in.
        if (ElementType(path, text, origin, list) is { } element && !element.IsVisualNode())
            return ValuePalette(element);

        // The app's OWN components first: they are generated into an `AppUI` surface exactly like the
        // framework's, they are what the developer was writing a minute ago, and a palette that only
        // offered the framework would be a palette that cannot build this app's screens.
        var surfaces = AppSurfaces(_current).Select(type => (Type: type, Source: "app")).ToList();
        if (_current.GetTypeByMetadataName("eQuantic.UI.Components.UI") is { } framework)
            surfaces.Add((framework, "framework"));

        var entries = new List<PaletteEntry>();
        foreach (var (surface, source) in surfaces)
        {
            foreach (var method in surface.GetMembers().OfType<IMethodSymbol>())
            {
                if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public) continue;
                if (!method.ReturnType.IsVisualNode()) continue;
                if (Snippet(method) is not { } snippet) continue;

                entries.Add(new PaletteEntry(method.Name, snippet, Summary(method.ReturnType), source));
            }
        }

        // Distinct BEFORE ordering and with the app's entries first, so a name the app defines is the
        // app's own — which is what the generated `global using static` does in the file too.
        return entries
            .DistinctBy(e => e.Name)
            .OrderBy(e => e.Source == "app" ? 0 : 1)
            .ThenBy(e => e.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>The smallest call of a factory that compiles, or null when one cannot be written.</summary>
    private static string? Snippet(IMethodSymbol factory, string? call = null)
    {
        var arguments = new List<string>();
        foreach (var parameter in factory.Parameters)
        {
            if (parameter.HasExplicitDefaultValue || parameter.IsParams) continue;

            var literal = Literal(parameter.Type, factory.ContainingType?.Name ?? factory.Name);
            if (literal is null) return null;
            arguments.Add(literal);
        }

        return $"{call ?? factory.Name}({string.Join(", ", arguments)})";
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
    public EditResult InsertChild(
        string path, string text, string origin, int index, string snippet, string? list = null)
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

        var named = list ?? "children";
        if (ListArgument(symbol, arguments, named) is not { } children)
        {
            return EditResult.Refused(named == "children"
                ? InsertRefusal(symbol, arguments) ?? $"{symbol.Name} does not take a list of children."
                : $"This {symbol.Name} has no `{named}: [ … ]` written on it.");
        }

        // Indented like the element it lands beside, so the new line reads as one the author wrote
        // rather than one a tool dropped in.
        var (at, addition, _) = Addition(source, children, index, snippet);
        return Insert(source, at, addition, text, path, construction, origin);
    }

    /// <summary>What a named list's elements are, when the caller named one and it resolves.</summary>
    private ITypeSymbol? ElementType(string? path, string? text, string? origin, string? list)
    {
        if (path is null || text is null || origin is null || list is null) return null;
        if (Locate(path, text, origin) is not var (_, _, construction, model) || construction is null) return null;
        if (model.GetSymbolInfo(construction).Symbol is not IMethodSymbol symbol) return null;

        var parameter = symbol.Parameters.FirstOrDefault(p => p.Name == list);
        return parameter?.Type switch
        {
            IArrayTypeSymbol array => array.ElementType,
            INamedTypeSymbol { TypeArguments: [var single] } => single,
            _ => null,
        };
    }

    /// <summary>
    /// The ways a VALUE is written: its own static factories, and target-typed <c>new(…)</c>.
    /// <para>
    /// The framework deliberately gives value records no factory of their own — they are data, and
    /// data reads fine as <c>new(…)</c> inside a list whose type is already known. So this reads the
    /// type rather than a surface, which is also why it works for a record the app just defined.
    /// </para>
    /// </summary>
    private static PaletteEntry[] ValuePalette(ITypeSymbol type)
    {
        // A list of strings or of numbers is offered a VALUE, not the type's static surface. Scanning
        // `string` for static members returning string offers Concat, Copy, Empty, Format, Intern and
        // Join — none of which is what anyone adding an item to a list meant.
        if (type.SpecialType != SpecialType.None)
        {
            return Literal(type, type.Name) is { } literal
                ? [new PaletteEntry(type.Name, literal, null, "value")]
                : [];
        }

        var entries = new List<PaletteEntry>();

        foreach (var member in type.GetMembers())
        {
            if (!member.IsStatic || member.DeclaredAccessibility != Accessibility.Public) continue;

            switch (member)
            {
                case IMethodSymbol method when method.MethodKind == MethodKind.Ordinary
                                               && SymbolEqualityComparer.Default.Equals(method.ReturnType, type)
                                               && Snippet(method, $"{type.Name}.{method.Name}") is { } call:
                    entries.Add(new PaletteEntry($"{type.Name}.{method.Name}", call, Summary(method), "value"));
                    break;

                // `GridTrack.Auto` — a whole value with no call at all.
                case IPropertySymbol property when SymbolEqualityComparer.Default.Equals(property.Type, type):
                    entries.Add(new PaletteEntry(
                        $"{type.Name}.{property.Name}", $"{type.Name}.{property.Name}", Summary(property), "value"));
                    break;

                case IFieldSymbol field when SymbolEqualityComparer.Default.Equals(field.Type, type):
                    entries.Add(new PaletteEntry(
                        $"{type.Name}.{field.Name}", $"{type.Name}.{field.Name}", Summary(field), "value"));
                    break;
            }
        }

        foreach (var constructor in (type as INamedTypeSymbol)?.InstanceConstructors ?? [])
        {
            if (constructor.DeclaredAccessibility != Accessibility.Public) continue;
            if (constructor.Parameters.Length == 0) continue;
            if (Snippet(constructor, "new") is not { } call) continue;

            entries.Add(new PaletteEntry($"new {type.Name}(…)", call, Summary(type), "value"));
        }

        return entries.DistinctBy(entry => entry.Snippet).OrderBy(entry => entry.Name, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// The app's own generated factory surfaces — every static <c>AppUI</c> the compilation declares.
    /// <para>
    /// Its namespace is the shortest one the app's components share, so it cannot be looked up by a
    /// fixed metadata name. Only the compilation's OWN assembly is walked: a referenced library's
    /// components reach this app through its own surface, not through a class this palette invented.
    /// </para>
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> AppSurfaces(Compilation compilation)
    {
        var pending = new Queue<INamespaceSymbol>();
        pending.Enqueue(compilation.Assembly.GlobalNamespace);

        while (pending.Count > 0)
        {
            var space = pending.Dequeue();
            foreach (var nested in space.GetNamespaceMembers()) pending.Enqueue(nested);
            foreach (var type in space.GetTypeMembers("AppUI"))
            {
                if (type.IsStatic) yield return type;
            }
        }
    }

    /// <summary>
    /// Whether a component paints its children in LAYERS — a <c>Stack</c>, where spec A3 makes paint
    /// order the child order.
    /// <para>
    /// Asked of the compilation rather than matched on a name in the author's file: the type resolves
    /// through the semantic model or the answer is simply no. It is what tells the canvas that a caret
    /// between two children would mark a place that does not exist, and the panel that "move up"
    /// means "send backward" there.
    /// </para>
    /// </summary>
    private bool IsLayered(ITypeSymbol? type)
    {
        if (type is null || _current is null) return false;
        if (_current.GetTypeByMetadataName("eQuantic.UI.Primitives.Stack") is not { } stack) return false;

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, stack)) return true;
        }
        return false;
    }

    /// <summary>The construction a node's own list belongs to — its parent in the tree as WRITTEN.</summary>
    private static SyntaxNode? EnclosingConstruction(SyntaxNode construction) =>
        construction.Parent is ExpressionElementSyntax { Parent: CollectionExpressionSyntax list }
            ? list.Ancestors().FirstOrDefault(n => n is ObjectCreationExpressionSyntax or InvocationExpressionSyntax)
            : null;

    /// <summary>One line, and not a long one: this is a label in a panel, not the source of record.</summary>
    private static string Shortened(string text)
    {
        var single = string.Join(' ', text.Split('\n').Select(line => line.Trim()));
        return single.Length <= 60 ? single : single[..57] + "…";
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
    private static CollectionExpressionSyntax? ListArgument(
        IMethodSymbol symbol, SeparatedSyntaxList<ArgumentSyntax>? arguments, string name)
    {
        if (arguments is not { } list) return null;

        for (var i = 0; i < symbol.Parameters.Length; i++)
        {
            if (symbol.Parameters[i].Name != name) continue;
            return ArgumentFor(list, symbol.Parameters[i], i)?.Expression as CollectionExpressionSyntax;
        }
        return null;
    }

    private static CollectionExpressionSyntax? ChildrenList(
        IMethodSymbol symbol, SeparatedSyntaxList<ArgumentSyntax>? arguments) =>
        ListArgument(symbol, arguments, "children");

    /// <summary>
    /// Every list this call is written with — not only <c>children</c>.
    /// <para>
    /// A <c>Grid</c>'s <c>columns</c> is the same shape as a <c>children</c>: a collection expression
    /// whose elements have real spans, so everything built for children works on it unchanged. So do
    /// a menu's items and a dialog's actions. Withholding those would have been a fence around the
    /// word "children" rather than around anything real.
    /// </para>
    /// </summary>
    private NodeList[] Lists(IMethodSymbol symbol, SeparatedSyntaxList<ArgumentSyntax>? arguments)
    {
        var found = new List<NodeList>();
        var layered = IsLayered(symbol.MethodKind == MethodKind.Constructor ? symbol.ContainingType : symbol.ReturnType);
        foreach (var parameter in symbol.Parameters)
        {
            if (ListArgument(symbol, arguments, parameter.Name) is not { } list) continue;

            var element = parameter.Type switch
            {
                IArrayTypeSymbol array => array.ElementType,
                INamedTypeSymbol { TypeArguments: [var single] } => single,
                _ => null,
            };
            if (element is null) continue;

            var visual = element.IsVisualNode();
            found.Add(new NodeList(
                parameter.Name,
                element.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                list.Elements.Count,
                visual,
                visual && layered,
                visual ? null : list.Elements.Select(entry => Shortened(entry.ToString())).ToArray()));
        }

        return found.ToArray();
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

    /// <summary>
    /// The settable members of a value written as <c>new T { … }</c>, each an ordinary property.
    /// <para>
    /// A <c>BoxStyle</c> arrives at the panel as one long line of C#, and the things inside it —
    /// padding, background, corner radius — are exactly what an author reaches for. Reported as rows
    /// rather than as text, they get the same editor and the same refusals as any other property.
    /// </para>
    /// </summary>
    private static NodeProperty[]? MembersOf(ObjectCreationExpressionSyntax? creation, SemanticModel model)
    {
        if (creation is null) return null;
        if (model.GetTypeInfo(creation).Type is not INamedTypeSymbol type) return null;
        // Written with `new`, so the braces are how this form is spelled: they can be added.
        var reachable = true;

        var members = new List<NodeProperty>();
        // Nearest declaration wins. A property that is overridden, or shadowed with `new`, is declared
        // at more than one level of the hierarchy and would otherwise be listed once per level.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in Inherited(type).OfType<IPropertySymbol>())
        {
            if (!Writable(member) || IsStructural(member.Type)) continue;
            if (!seen.Add(member.Name)) continue;

            var assignment = creation.Initializer?.Expressions
                .OfType<AssignmentExpressionSyntax>()
                .FirstOrDefault(a => a.Left.ToString() == member.Name);

            members.Add(new NodeProperty(
                member.Name,
                member.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                assignment is null ? "unset" : "initializer",
                assignment?.Right.ToString(),
                reachable,
                null,
                OptionsFor(member.Type, assignment?.Right, model),
                Summary(member)));
        }

        return members.Count == 0 ? null : members.ToArray();
    }

    /// <summary>
    /// Takes a member back OUT of a value's initializer — <c>style.Padding</c>, unset.
    /// <para>
    /// A spreadsheet you can only add rows to is a spreadsheet with a leak. The assignment goes and
    /// exactly one separator with it, the same bite every other removal here takes.
    /// </para>
    /// </summary>
    public EditResult UnsetProperty(string path, string text, string origin, string property)
    {
        if (_compilation is null) throw new InvalidOperationException("initialize first");

        if (property.Split('.') is not [var argumentName, var member])
            return EditResult.Refused($"'{property}' does not name a member of a value.");

        if (Locate(path, text, origin) is not var (_, source, construction, model) || construction is null)
            return EditResult.Refused("That element's origin does not name anything in this file.");

        if (model.GetSymbolInfo(construction).Symbol is not IMethodSymbol symbol)
            return EditResult.Refused("The compiler cannot resolve that construction.");

        var arguments = construction switch
        {
            ObjectCreationExpressionSyntax creation => creation.ArgumentList?.Arguments,
            InvocationExpressionSyntax invocation => invocation.ArgumentList.Arguments,
            _ => null,
        };
        if (arguments is not { } list) return EditResult.Refused("That construction takes no arguments.");

        var index = -1;
        for (var i = 0; i < symbol.Parameters.Length; i++)
        {
            if (symbol.Parameters[i].Name == argumentName) { index = i; break; }
        }
        if (index < 0) return EditResult.Refused($"'{argumentName}' is not a parameter of {symbol.Name}.");

        if (ArgumentFor(list, symbol.Parameters[index], index)?.Expression is not ObjectCreationExpressionSyntax written
            || written.Initializer is null)
        {
            return EditResult.Refused($"'{argumentName}' is not written as a value with members to unset.");
        }

        var assignment = written.Initializer.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault(a => a.Left.ToString() == member);
        if (assignment is null) return EditResult.Refused($"'{member}' is not set here.");

        var bite = Removal(written.Initializer.Expressions, (ExpressionSyntax)assignment);
        return Guarded(source, bite, "", text, path, Moved(source, bite, "", construction, origin));
    }

    /// <summary>Whether a name is a member this tool may write into an object initializer: settable,
    /// and settable from OUTSIDE the type — a private setter is not an offer.</summary>
    private static bool Settable(ITypeSymbol? type, string name) =>
        type is not null && Inherited(type).OfType<IPropertySymbol>().Any(member =>
            member.Name == name && Writable(member));

    /// <summary>
    /// A property this panel may set: public, instance, with a setter that is public too.
    /// <para>
    /// <c>SetMethod is not null</c> is not enough — a <c>private set</c> has one, and offering it
    /// produces a row whose every edit is refused by the compiler rather than by the tool.
    /// </para>
    /// </summary>
    private static bool Writable(IPropertySymbol property) =>
        property.DeclaredAccessibility == Accessibility.Public
        && !property.IsStatic
        && !property.IsIndexer
        && property.SetMethod is { DeclaredAccessibility: Accessibility.Public };

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
    /// <summary>
    /// The closed set a value may take, WIDENED by what is written there.
    /// <para>
    /// Half the framework's tokens are not enums at all. <c>Space</c> is a static class of
    /// <c>const float</c>, so a <c>float gap</c> parameter is typed as a number and a panel reading
    /// only the type offers a number box — for a value that must come off a 4dp scale. The written
    /// expression is what says otherwise: <c>Space.S3</c> names a member of a static class, and every
    /// sibling of the same type is a value that belongs in that slot.
    /// </para>
    /// </summary>
    private static string[]? OptionsFor(ITypeSymbol type, ExpressionSyntax? written, SemanticModel model)
    {
        if (OptionsFor(type) is { } members) return members;

        if (written is not MemberAccessExpressionSyntax access) return null;
        if (model.GetSymbolInfo(access).Symbol is not IFieldSymbol field) return null;
        if (!field.IsStatic || field.ContainingType is not { IsStatic: true } scale) return null;

        // Qualified the way the AUTHOR qualified it. The panel writes what it offers straight into the
        // file, and the scale's simple name is only certainly in scope if that is how it is already
        // written there — `Primitives.Space.S3` would otherwise be answered with an unresolvable
        // `Space.S4`.
        var prefix = access.Expression.ToString();

        var siblings = scale.GetMembers().OfType<IFieldSymbol>()
            .Where(sibling => sibling.IsStatic
                              && sibling.DeclaredAccessibility == Accessibility.Public
                              && SymbolEqualityComparer.Default.Equals(sibling.Type, field.Type))
            .Select(sibling => $"{prefix}.{sibling.Name}")
            .ToArray();

        return siblings.Length > 1 ? siblings : null;
    }

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
