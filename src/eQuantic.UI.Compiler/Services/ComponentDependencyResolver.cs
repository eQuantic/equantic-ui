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
    private readonly HashSet<string> _plainClasses = new();

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
            var tree = CSharpSyntaxTree.ParseText(code, path: filePath);
            var root = tree.GetRoot();

            // Discover user value types (records/structs) — emitted as named JS classes (so references
            // import them).
            foreach (var valueType in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (CodeGen.RecordTypeEmitter.CanEmit(valueType))
                    _recordTypes.Add(valueType.Identifier.Text);
            }

            // Find all class declarations
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classes)
            {
                var className = classDecl.Identifier.Text;

                // Static utility classes are emitted as their own module — register so referencers
                // import. NESTED static classes embed in their owner's module (private scope, every
                // section has its own `Copy`) and must never register as importable.
                if (classDecl.Parent is not Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax
                    && classDecl.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)))
                {
                    _staticHelpers.Add(className);
                }

                // A PLAIN class is a module too — a referencing module has to import it, or the
                // page dies with "Bucket is not defined". Components and state classes are resolved
                // by their own paths; a nested class embeds in its owner.
                else if (classDecl.Parent is not Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax
                         && classDecl.Members.Count > 0
                         && !IsComponentLike(classDecl))
                {
                    _plainClasses.Add(className);
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
        catch (Exception ex)
        {
            // Silently skip files that can't be analyzed
            Console.Error.WriteLine($"Warning: Could not analyze {Path.GetFileName(filePath)}: {ex.Message}");
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

    /// <summary>Plain classes the app declares — each its own module, each importable.</summary>
    public IReadOnlySet<string> GetAllPlainClasses() => _plainClasses;

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
