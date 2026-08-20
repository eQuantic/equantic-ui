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

    /// <summary>
    /// Whether a host handed over the project's real compilation. With it, models are
    /// AUTHORITATIVE (unbindable calls are build errors); without it, the minimal compilation
    /// resolves almost nothing by construction, and name heuristics stay legal.
    /// </summary>
    public bool HasProjectCompilation => _projectCompilation != null;

    /// <summary>
    /// Parse options for a tree that must JOIN the model this provider serves. Roslyn refuses a
    /// compilation whose trees mix language versions, so when a project compilation exists, eqc's
    /// own tree adopts ITS version (the SDK builds the whole compilation at Preview already —
    /// see <see cref="ParseDefaults"/> — so nothing is lost there); standalone, Preview stands.
    /// </summary>
    public CSharpParseOptions JoinOptions =>
        _projectCompilation?.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions
            ?? ParseDefaults.Options;

    private void LoadStandardReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly, // System.Private.CoreLib
            typeof(Console).Assembly, // System.Console
            typeof(Enumerable).Assembly, // System.Linq
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
        var tree = CSharpSyntaxTree.ParseText(sourceCode, ParseDefaults.Options);
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
                // Found existing tree in project. We must REPLACE it with our new tree
                // because the ComponentDefinition holds nodes from the 'tree' instance.
                // If we used projectTree's model, we'd get "Syntax node not within syntax tree".
                var updated = _projectCompilation.ReplaceSyntaxTree(projectTree, tree);
                return updated.GetSemanticModel(tree);
            }

            // If tree not found in project compilation, add it
            var updatedCompilation = _projectCompilation.AddSyntaxTrees(tree);
            return updatedCompilation.GetSemanticModel(tree);
        }

        // Fallback: Create minimal compilation (old behavior). Nullable enabled — see
        // ProjectCompilationHelper: disabled annotations turn `Action?` into Nullable<Action> and
        // silently break overload binding.
        var compilation = CSharpCompilation.Create(
            "eQuantic.UI.DynamicAssembly",
            new[] { tree },
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        return compilation.GetSemanticModel(tree);
    }

    /// <summary>
    /// What the C# LANGUAGE says about this file — the CS#### errors, before eqc translates a
    /// single node. eqc already holds the compilation it needs to answer this; it simply never
    /// asked, so a file with a syntax error was parsed leniently by Roslyn and transpiled anyway,
    /// emitting a component built from a half-understood tree.
    ///
    /// This REPORTS, it does not gate: in an SDK build csc is the authority on C# and runs right
    /// after, and eqc's own compilation can legitimately be poorer than csc's (source generators
    /// contribute types eqc never sees), so failing the build here would reject code that
    /// compiles. A host with no csc of its own — the playground — is the one that must gate,
    /// because there nothing else ever will.
    /// </summary>
    public IReadOnlyList<Diagnostic> GetLanguageDiagnostics(SyntaxTree tree)
    {
        var compilation = _projectCompilation is null
            ? CSharpCompilation.Create(
                "eQuantic.UI.DynamicAssembly",
                new[] { tree },
                _references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable))
            : _projectCompilation.SyntaxTrees.FirstOrDefault(t => AreSameFile(t.FilePath, tree.FilePath)) is { } existing
                ? _projectCompilation.ReplaceSyntaxTree(existing, tree)
                : _projectCompilation.AddSyntaxTrees(tree);

        return compilation.GetSemanticModel(tree)
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
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
