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

    /// <summary>
    /// Scans source code directories to build dependency map
    /// </summary>
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

            // Find all class declarations
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classes)
            {
                var className = classDecl.Identifier.Text;

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
