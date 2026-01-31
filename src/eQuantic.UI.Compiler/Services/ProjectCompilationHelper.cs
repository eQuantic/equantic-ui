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
    public static Compilation CreateCompilationFromSources(
        IEnumerable<string> sourceFiles,
        IEnumerable<string> assemblyPaths,
        string assemblyName = "DynamicAssembly")
    {
        // Parse all source files
        var syntaxTrees = sourceFiles
            .Where(File.Exists)
            .Select(file => CSharpSyntaxTree.ParseText(
                File.ReadAllText(file),
                path: file))
            .ToList();

        // Load all assembly references
        var references = new List<MetadataReference>();

        // Add standard references
        references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(List<>).Assembly.Location));

        try
        {
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location));
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location));
        }
        catch
        {
            // Ignore if assemblies can't be loaded
        }

        // Add project-specific references
        foreach (var assemblyPath in assemblyPaths)
        {
            if (File.Exists(assemblyPath))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(assemblyPath));
                }
                catch
                {
                    // Ignore assemblies that can't be loaded
                }
            }
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
