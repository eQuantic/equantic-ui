using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Resolves component dependencies by analyzing the inheritance hierarchy
/// using Roslyn semantic analysis.
/// </summary>
public class ComponentDependencyResolver
{
    private readonly Dictionary<string, HashSet<string>> _dependencyCache = new();
    private readonly HashSet<string> _analysedAssemblies = new();

    /// <summary>User data types (positional records) discovered during the scan — emitted as named
    /// JS classes, so components that reference them import the generated module.</summary>
    private readonly HashSet<string> _recordTypes = new();

    /// <summary>Static utility classes (`static class X`) discovered during the scan — emitted as their
    /// own module, so a component referencing <c>X.Foo()</c> imports it.</summary>
    private readonly HashSet<string> _staticHelpers = new();
    private readonly HashSet<string> _runtimeProvidedTypes = new();

    /// <summary>The classes the COMPONENT path emits (<see cref="IsComponentLike"/>), each a module of
    /// its own, a nested one included, as the parser writes one for every component it finds.</summary>
    private readonly HashSet<string> _componentLike = new(StringComparer.Ordinal);

    /// <summary>A class that is not a component or a static helper, as the scan saw it: what the
    /// plain-class rule reads of its declaration, and the CLR name its type is found by in the
    /// project's compilation.</summary>
    private readonly record struct ScannedClass(PlainClassModule.Declared Declared, string MetadataName);

    private readonly List<ScannedClass> _classes = new();

    /// <summary>Every type declaration the scan read, by name: the chain of bases a host with no
    /// compilation walks, and the one the parser of such a host reads too (<see cref="Chains"/>).</summary>
    private readonly PlainClassModule.Scan _scan = new();

    /// <summary>The project's compilation, which answers for every type the scan reads by its symbol;
    /// null for a host that has none.</summary>
    private readonly Compilation? _projectCompilation;

    /// <summary>The plain-class modules, settled over every file the scan read; null until asked, and
    /// again after another file is read.</summary>
    private HashSet<string>? _plainClassesSettled;

    /// <param name="projectCompilation">
    /// The project's compilation, the one the compiler's model is built from, so each type the scan reads
    /// is asked of its SYMBOL, as the parser asks it, and not of the names the scan saw: a base from a
    /// referenced library is what it is there, whatever its name says, an interface is never on a chain
    /// of bases, and a partial record marked <c>[ServerOnly]</c> on another declaration is server-only.
    /// It is the resolver's from the start, as every answer the scan gives depends on it. A host that
    /// has none walks the chains by name (<see cref="PlainClassModule"/>).
    /// </param>
    public ComponentDependencyResolver(Compilation? projectCompilation = null)
    {
        _projectCompilation = projectCompilation;
    }

    /// <summary>A declaration's type in the project's compilation, found by its CLR name; null where
    /// the host has no compilation, or the compilation does not know the type.</summary>
    private INamedTypeSymbol? SymbolOf(TypeDeclarationSyntax declaration) =>
        _projectCompilation?.GetTypeByMetadataName(Parser.ComponentParser.ClrIdentity(declaration));

    /// <summary>What the scan saw of the app's declarations, which the parser reads for the chain of a
    /// class when its host has no compilation: the scan reaches across files, a parser's own file does
    /// not.</summary>
    internal PlainClassModule.Scan Chains => _scan;

    /// <summary>
    /// Which of the scanned classes are plain-class modules: the predicate's answer over the CHAIN of
    /// base classes, by symbol in the project's compilation, and through the scan of every file read
    /// where there is none (<see cref="PlainClassModule"/>). Each declaration is judged on its own, so
    /// two classes that share a simple name, in two namespaces, are two answers.
    /// </summary>
    private HashSet<string> PlainClasses()
    {
        if (_plainClassesSettled is { } settled) return settled;
        var modules = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scanned in _classes)
            if (PlainClassModule.Is(scanned.Declared, _projectCompilation?.GetTypeByMetadataName(scanned.MetadataName), _scan))
                modules.Add(scanned.Declared.Module);
        _plainClassesSettled = modules;
        return modules;
    }

    /// <summary>
    /// Scans source code directories to build dependency map
    /// </summary>
    /// <summary>The generated-sources directory for the configuration being built; null lets the
    /// scan find it. Set from the SDK, for the reason <see cref="ProjectCompilationHelper"/> gives:
    /// a project built Debug AND Release otherwise contributes every generated type twice.</summary>
    public string? GeneratedDirectory { get; set; }

    public void ScanSourceDirectories(IEnumerable<string> directories)
    {
        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory)) continue;

            var csFiles = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            foreach (var file in csFiles)
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                    file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    continue;

                AnalyzeFile(file);
            }

            // GENERATED sources become modules like any other (eqc transpiles them), so the scan
            // has to know their types too: this map is what decides whether a referenced name is
            // imported as `./AppUI`. Without it the page names a binding nothing imports and the
            // bundle leaves it undefined — a build that succeeds and a page that throws.
            foreach (var file in ProjectCompilationHelper.GetCompilerGeneratedFiles(directory, GeneratedDirectory))
                AnalyzeFile(file);
        }
    }

    /// <summary>
    /// Analyzes a C# file to extract component inheritance relationships
    /// </summary>
    private void AnalyzeFile(string filePath)
    {
        try
        {
            var code = File.ReadAllText(filePath);
            Analyze(CSharpSyntaxTree.ParseText(code, ParseDefaults.Options, path: filePath).GetRoot());
        }
        catch (Exception ex)
        {
            // Silently skip files that can't be analyzed
            Console.Error.WriteLine($"Warning: Could not analyze {Path.GetFileName(filePath)}: {ex.Message}");
        }
    }

    /// <summary>
    /// The scan of a compilation already in memory, for a compile that was handed no per-app scan
    /// (<c>CompileSource</c>, the playground): the same rules over the compilation's own trees. A
    /// record's module imports the app types its body names only when this answers they became
    /// modules, and without a scan it answered for none, so a struct member's zero
    /// (<c>new Cell()</c>) reached the module with nothing importing it.
    /// </summary>
    public static ComponentDependencyResolver From(Compilation compilation)
    {
        var resolver = new ComponentDependencyResolver(compilation);
        foreach (var tree in compilation.SyntaxTrees) resolver.Analyze(tree.GetRoot());
        return resolver;
    }

    /// <summary>What one source file declares, by the rules every scan applies.</summary>
    private void Analyze(SyntaxNode root)
    {
        // Discover user value types (records/structs) — emitted as named JS classes (so references
        // import them). A server-only one has none, whichever of its declarations says so.
        foreach (var valueType in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (valueType is (RecordDeclarationSyntax or StructDeclarationSyntax)
                && CodeGen.RecordTypeEmitter.CanEmit(valueType, SymbolOf(valueType)))
                _recordTypes.Add(valueType.TwinTypeName());
        }

        _scan.Add(root);
        _plainClassesSettled = null;

        // Find all class declarations
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;

            // [RuntimeProvided] types already exist in @equantic/runtime. The resolver is the
            // no-project-semantic-model fallback used to decide whether a referenced name is a
            // per-app module, so registering one here would manufacture a dangling ./Type import.
            if (classDecl.AttributeLists.SelectMany(list => list.Attributes)
                .Any(attribute => attribute.IsNamed("RuntimeProvided")))
            {
                if (classDecl.Parent is not ClassDeclarationSyntax)
                    _runtimeProvidedTypes.Add(className);
                continue;
            }

            // Static utility classes are emitted as their own module — register so referencers
            // import. A NESTED one is a module of its own too, named by its owner (`Section$Copy`,
            // #584), where its owner crosses.
            if (classDecl.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)))
            {
                if (PlainClassModule.OwnersCross(classDecl) && !PlainClassModule.IsServerOnlyDeclaration(classDecl)
                    && !(SymbolOf(classDecl) is { } helper && PlainClassModule.OwnerKeptOut(helper)))
                    _staticHelpers.Add(classDecl.TwinTypeName());
            }

            // A component is a module of its own, as the parser writes one for every component it
            // finds, a nested one included. A static class never is one, whatever its methods are
            // called: a nested `static class Copy` with a `Build` helper is its owner's scope.
            else if (!classDecl.Modifiers.Any(SyntaxKind.StaticKeyword) && IsComponentLike(classDecl))
            {
                _componentLike.Add(classDecl.TwinTypeName());
            }

            // A PLAIN class is a module too — a referencing module has to import it, or the page
            // dies with "Bucket is not defined". Whether it is one is the parser's rule, read from
            // the same predicate (#423), and settled once every file is scanned: its chain of bases
            // can be declared in another file.
            else
            {
                _classes.Add(new ScannedClass(PlainClassModule.Declared.Of(classDecl), Parser.ComponentParser.ClrIdentity(classDecl)));
            }

            // Get base type
            var baseType = classDecl.BaseList?.Types.FirstOrDefault();
            if (baseType != null)
            {
                var baseTypeName = baseType.Type.ToString();

                // Clean generic types
                if (baseTypeName.Contains('<'))
                {
                    baseTypeName = baseTypeName.Substring(0, baseTypeName.IndexOf('<'));
                }

                // Track ALL inheritance relationships for UI components
                // We'll filter later - this allows discovering the full dependency graph
                if (!string.IsNullOrEmpty(baseTypeName))
                {
                    if (!_dependencyCache.ContainsKey(className))
                    {
                        _dependencyCache[className] = new HashSet<string>();
                    }

                    _dependencyCache[className].Add(baseTypeName);
                }
            }
        }
    }

    /// <summary>
    /// Gets all transitive dependencies for a component type
    /// </summary>
    public HashSet<string> GetDependencies(string componentType)
    {
        var dependencies = new HashSet<string>();
        GetDependenciesRecursive(componentType, dependencies);
        return dependencies;
    }

    private void GetDependenciesRecursive(string componentType, HashSet<string> accumulated)
    {
        if (_dependencyCache.TryGetValue(componentType, out var directDeps))
        {
            foreach (var dep in directDeps)
            {
                if (accumulated.Add(dep)) // Only recurse if not already visited
                {
                    GetDependenciesRecursive(dep, accumulated);
                }
            }
        }
    }

    /// <summary>
    /// Resolves all dependencies for a collection of component types
    /// </summary>
    public HashSet<string> ResolveDependencies(IEnumerable<string> componentTypes)
    {
        var allDependencies = new HashSet<string>();

        foreach (var type in componentTypes)
        {
            var deps = GetDependencies(type);
            foreach (var dep in deps)
            {
                allDependencies.Add(dep);
            }
        }

        return allDependencies;
    }

    private bool IsUIComponent(string typeName)
    {
        return typeName switch
        {
            "HtmlElement" => true,
            "StatefulComponent" => true,
            "StatelessComponent" => true,
            "Component" => true,
            _ when typeName.EndsWith("Component") => true,
            _ when _dependencyCache.ContainsKey(typeName) => true,
            _ => false
        };
    }

    /// <summary>
    /// Gets all registered component types
    /// </summary>
    public IEnumerable<string> GetAllComponents()
    {
        return _dependencyCache.Keys;
    }

    /// <summary>Names of user data types (records) emitted as named JS classes.</summary>
    public IReadOnlySet<string> GetAllRecords() => _recordTypes;

    /// <summary>Names of static utility classes emitted as their own modules.</summary>
    public IReadOnlySet<string> GetAllStaticHelpers() => _staticHelpers;

    /// <summary>Top-level types whose implementation is supplied by <c>@equantic/runtime</c>.
    /// Collected syntactically for the no-semantic-model fallback.</summary>
    public IReadOnlySet<string> GetRuntimeProvidedTypes() => _runtimeProvidedTypes;

    /// <summary>Plain classes the app declares — each its own module, each importable.</summary>
    public IReadOnlySet<string> GetAllPlainClasses() => PlainClasses();

    /// <summary>
    /// Whether the scan knows <paramref name="name"/> became a module of its own: a record or struct,
    /// a static helper, a component or a plain class. Every emitter imports an APP type only when this
    /// answers yes, which is what keeps an import from naming a module nobody wrote.
    /// <para>
    /// Modules share ONE flat namespace, named by the type's simple name, so a name is a module when
    /// ANY declaration of it is one, and nothing a declaration of that name is refused for takes it
    /// back: a class the plain-class rule refuses (a nested one, an exception, one over a server-only
    /// base) said nothing about a component of the same name in another file, and its name vetoed
    /// that component's import, which the browser met as a ReferenceError. The component graph is no
    /// answer either: it holds every class with a base, and the parser writes no module for an
    /// attribute or an exception (#423).
    /// </para>
    /// </summary>
    public bool IsModule(string name) =>
        _recordTypes.Contains(name)
        || _staticHelpers.Contains(name)
        || _componentLike.Contains(name)
        || PlainClasses().Contains(name);

    /// <summary>
    /// Whether the class is (or extends) something the COMPONENT path emits. Syntactic on purpose:
    /// the resolver runs before semantics, and a component's own module is registered elsewhere.
    /// </summary>
    private static bool IsComponentLike(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.Members.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .Any(m => m.Identifier.Text is "Build" or "Render" or "CreateState"))
        {
            return true;
        }

        var baseName = classDecl.BaseList?.Types.FirstOrDefault()?.Type.ToString();
        if (baseName is null) return false;
        if (baseName.Contains('<')) baseName = baseName[..baseName.IndexOf('<')];
        if (baseName.Contains('.')) baseName = baseName[(baseName.LastIndexOf('.') + 1)..];
        return baseName is "StatefulComponent" or "StatelessComponent" or "ComponentState"
            or "HtmlElement" or "UiComponent" or "Flex" or "Container" or "Stack";
    }

    /// <summary>
    /// Debug: Print dependency tree
    /// </summary>
    public void PrintDependencyTree()
    {
        Console.WriteLine("Component Dependency Tree:");
        foreach (var kvp in _dependencyCache.OrderBy(x => x.Key))
        {
            Console.WriteLine($"  {kvp.Key} → {string.Join(", ", kvp.Value)}");
        }
    }
}
