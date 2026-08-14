using System.Diagnostics;
using System.Text.RegularExpressions;
using eQuantic.UI.Compiler;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
    private string _projectDir = "";

    /// <summary>Emitted JS per dependency FILE, keyed by path and invalidated by write time — a page's
    /// neighbours do not change while you are typing in the page.</summary>
    private readonly Dictionary<string, (DateTime Stamp, List<CompilationResult> Modules)> _dependencies = new(StringComparer.OrdinalIgnoreCase);

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

        _compilation = ProjectCompilationHelper.CreateCompilationFromSources(
            sources, references, assemblyName: assemblyName, addStandardReferences: false);
        _compiler.SetProjectCompilation(_compilation);

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
        var compilation = Swap(_compilation, tree);

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
        var file = Directory.EnumerateFiles(_projectDir, moduleName + ".cs", SearchOption.AllDirectories)
            .FirstOrDefault(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                              && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        if (file is null) return [];

        var stamp = File.GetLastWriteTimeUtc(file);
        if (_dependencies.TryGetValue(file, out var cached) && cached.Stamp == stamp) return cached.Modules;

        var modules = _compiler.CompileSource(File.ReadAllText(file), file)
            .Where(r => r.Success && r.TypeScript.Length > 0)
            .ToList();

        _dependencies[file] = (stamp, modules);
        return modules;
    }
}
