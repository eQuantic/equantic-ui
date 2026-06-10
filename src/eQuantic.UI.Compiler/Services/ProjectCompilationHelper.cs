using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using System.Reflection;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Helper for obtaining the full Roslyn compilation of a C# project.
/// Used to enable resolution of external types in component compilation.
/// </summary>
public static class ProjectCompilationHelper
{
    /// <summary>
    /// Gets the full Roslyn compilation for a .csproj file.
    /// This includes all source files and references in the project.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file</param>
    /// <returns>The full compilation with all project types available</returns>
    public static async Task<Compilation?> GetProjectCompilationAsync(string projectPath)
    {
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException($"Project file not found: {projectPath}");
        }

        try
        {
            using var workspace = MSBuildWorkspace.Create();

            // Register workspace failure handler (non-obsolete way)
            #pragma warning disable CS0618 // Type or member is obsolete
            workspace.WorkspaceFailed += (sender, e) =>
            {
                // Log or ignore workspace failures
                // These are often benign (missing references, etc.)
            };
            #pragma warning restore CS0618

            var project = await workspace.OpenProjectAsync(projectPath);
            var compilation = await project.GetCompilationAsync();

            return compilation;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load project compilation from {projectPath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates a compilation from source files and assemblies.
    /// Useful when MSBuildWorkspace is not available (e.g., in MSBuild tasks).
    /// </summary>
    /// <param name="sourceFiles">Paths to .cs files</param>
    /// <param name="assemblyPaths">Paths to referenced assemblies (.dll)</param>
    /// <param name="assemblyName">Name for the compilation</param>
    /// <returns>A compilation with the specified sources and references</returns>
    /// <param name="addStandardReferences">
    /// When true (default), the running BCL impl assemblies (CoreLib, Linq, Collections, …) are injected.
    /// Pass false when <paramref name="assemblyPaths"/> already holds the complete MSBuild-resolved
    /// reference set: mixing runtime impl assemblies with targeting-pack ref assemblies duplicates type
    /// identities and only muddies symbol resolution.
    /// </param>
    public static Compilation CreateCompilationFromSources(
        IEnumerable<string> sourceFiles,
        IEnumerable<string> assemblyPaths,
        string assemblyName = "DynamicAssembly",
        bool addStandardReferences = true)
    {
        // Parse all source files
        var syntaxTrees = sourceFiles
            .Where(File.Exists)
            .Select(file => CSharpSyntaxTree.ParseText(
                File.ReadAllText(file),
                path: file))
            .ToList();

        // Load all assembly references, deduping by file name so the same assembly supplied through both
        // the standard set and the explicit list isn't added twice.
        var references = new List<MetadataReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddReference(string path)
        {
            if (!File.Exists(path) || !seen.Add(Path.GetFileName(path)))
                return;
            try
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
            catch
            {
                // Ignore assemblies that can't be loaded
            }
        }

        if (addStandardReferences)
        {
            AddReference(typeof(object).Assembly.Location);
            AddReference(typeof(Console).Assembly.Location);
            AddReference(typeof(Enumerable).Assembly.Location);
            AddReference(typeof(List<>).Assembly.Location);

            try
            {
                AddReference(Assembly.Load("System.Runtime").Location);
                AddReference(Assembly.Load("System.Collections").Location);
            }
            catch
            {
                // Ignore if assemblies can't be loaded
            }
        }

        // Add project-specific references
        foreach (var assemblyPath in assemblyPaths)
        {
            AddReference(assemblyPath);
        }

        // Create compilation
        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation;
    }

    /// <summary>
    /// The SDK-generated implicit-usings file(s) for a project (<c>obj/**/*.GlobalUsings.g.cs</c>).
    /// <see cref="GetProjectSourceFiles"/> deliberately skips <c>obj/</c> — generated code isn't
    /// transpiled — but this one generated artifact must still reach the semantic model, because it is
    /// what lets BCL types referenced unqualified resolve (e.g. <c>Dictionary&lt;RecordKey, V&gt;</c>
    /// under <c>&lt;ImplicitUsings&gt;</c>, with no explicit <c>using System.Collections.Generic;</c>).
    /// Consuming the real file — rather than hardcoding a namespace list — honors the project's actual
    /// <c>ImplicitUsings</c> setting and any custom <c>&lt;Using&gt;</c> items, so the compiler's
    /// semantic view matches the real <c>dotnet build</c>. The SDK generates it during the build before
    /// the eqc step runs, so it is present on disk by the time the compilation is assembled.
    /// </summary>
    public static IEnumerable<string> GetGeneratedGlobalUsingsFiles(string projectDirectory)
    {
        var objDir = Path.Combine(projectDirectory, "obj");
        return Directory.Exists(objDir)
            ? Directory.GetFiles(objDir, "*.GlobalUsings.g.cs", SearchOption.AllDirectories)
            : Enumerable.Empty<string>();
    }

    /// <summary>
    /// Gets all .cs source files from a project directory.
    /// Excludes obj/ and bin/ directories.
    /// </summary>
    public static IEnumerable<string> GetProjectSourceFiles(string projectDirectory)
    {
        if (!Directory.Exists(projectDirectory))
        {
            yield break;
        }

        var csFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories);

        foreach (var file in csFiles)
        {
            // Skip obj and bin directories
            var relativePath = Path.GetRelativePath(projectDirectory, file);
            if (relativePath.StartsWith("obj" + Path.DirectorySeparatorChar) ||
                relativePath.StartsWith("bin" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            yield return file;
        }
    }
}
