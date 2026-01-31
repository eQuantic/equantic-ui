using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Provides semantic models for component files.
/// Can use either a full project compilation (with all external types) or minimal compilation (isolated file).
/// </summary>
public class SemanticModelProvider
{
    private readonly List<MetadataReference> _references;
    private Compilation? _projectCompilation;

    public SemanticModelProvider()
    {
        _references = new List<MetadataReference>();
        LoadStandardReferences();
    }

    /// <summary>
    /// Sets the full project compilation to resolve external types.
    /// When set, GetSemanticModel will use this compilation instead of creating a minimal one.
    /// </summary>
    public void SetProjectCompilation(Compilation projectCompilation)
    {
        _projectCompilation = projectCompilation;
    }

    /// <summary>
    /// Clears the project compilation, reverting to minimal compilation mode.
    /// </summary>
    public void ClearProjectCompilation()
    {
        _projectCompilation = null;
    }

    private void LoadStandardReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly, // System.Private.CoreLib
            typeof(Console).Assembly, // System.Console
            typeof(System.Linq.Enumerable).Assembly, // System.Linq
            typeof(List<>).Assembly, // System.Collections
            Assembly.Load("System.Runtime"),
            Assembly.Load("eQuantic.UI.Core") // eQuantic Core
        };

        foreach (var assembly in assemblies)
        {
            try
            {
                if (!string.IsNullOrEmpty(assembly.Location))
                {
                    _references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
            }
            catch
            {
                // Ignore assemblies that can't be loaded
            }
        }
    }

    public SemanticModel GetSemanticModel(string sourceCode)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        return GetSemanticModel(tree);
    }

    /// <summary>
    /// Gets a semantic model for the given syntax tree.
    /// If a project compilation is set, uses that (includes all project types).
    /// Otherwise, creates a minimal compilation (isolated file only).
    /// </summary>
    public SemanticModel GetSemanticModel(SyntaxTree tree)
    {
        // If we have a project compilation, try to find the tree in it
        if (_projectCompilation != null)
        {
            // Find the matching syntax tree in the project compilation
            var projectTree = _projectCompilation.SyntaxTrees
                .FirstOrDefault(t => AreSameFile(t.FilePath, tree.FilePath));

            if (projectTree != null)
            {
                // Return semantic model from the full project compilation
                // This includes all types from the entire project!
                return _projectCompilation.GetSemanticModel(projectTree);
            }

            // If tree not found in project compilation, but we have one,
            // we can add this tree to the project compilation
            var updatedCompilation = _projectCompilation.AddSyntaxTrees(tree);
            return updatedCompilation.GetSemanticModel(tree);
        }

        // Fallback: Create minimal compilation (old behavior)
        var compilation = CSharpCompilation.Create(
            "eQuantic.UI.DynamicAssembly",
            new[] { tree },
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation.GetSemanticModel(tree);
    }

    /// <summary>
    /// Compares two file paths to see if they reference the same file.
    /// Handles relative vs absolute paths, different separators, etc.
    /// </summary>
    private bool AreSameFile(string? path1, string? path2)
    {
        if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2))
            return false;

        try
        {
            var fullPath1 = Path.GetFullPath(path1);
            var fullPath2 = Path.GetFullPath(path2);
            return string.Equals(fullPath1, fullPath2, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If Path.GetFullPath fails, fall back to string comparison
            return string.Equals(path1, path2, StringComparison.OrdinalIgnoreCase);
        }
    }
}
