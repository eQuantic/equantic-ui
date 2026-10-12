using System.Text.Json;
using System.Text.RegularExpressions;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// What one build of a <see cref="Solution"/> left behind: whether it failed, what it said, and the
/// files it wrote, read back from disk as the bundler and the server would read them.
/// </summary>
internal sealed class Built
{
    private readonly Solution _solution;
    private readonly AppBuild _build;
    private readonly string _outputDirectory;

    public Built(Solution solution, AppBuild build, bool failed, string output, string errors, string outputDirectory)
    {
        _solution = solution;
        _build = build;
        Failed = failed;
        Output = output;
        Errors = errors;
        _outputDirectory = outputDirectory;
    }

    /// <summary>Whether the build produced an error, which is the process's exit code.</summary>
    public bool Failed { get; }

    /// <summary>What the build narrated.</summary>
    public string Output { get; }

    /// <summary>Its diagnostics, one a line, in MSBuild's form.</summary>
    public string Errors { get; }

    /// <summary>The app's compilation, with each referenced project among its references.</summary>
    public Compilation Compilation => _build.Compilation!;

    /// <summary>The compilation the build made of a referenced project, found among the app's
    /// references by the project's name.</summary>
    public Compilation CompilationOf(string project) =>
        Compilation.References.OfType<CompilationReference>()
            .Select(reference => reference.Compilation)
            .Single(compilation => compilation.AssemblyName == project);

    /// <summary>The names of the modules the build wrote, each a type's twin.</summary>
    public IReadOnlyList<string> Modules =>
        Directory.Exists(_build.IntermediateDirectory)
            ? Directory.GetFiles(_build.IntermediateDirectory, "*.ts")
                .Select(file => Path.GetFileNameWithoutExtension(file)!)
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];

    /// <summary>The module written for a type, or null when the build wrote none.</summary>
    public string? Module(string name)
    {
        var path = Path.Combine(_build.IntermediateDirectory, name + ".ts");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>The sibling modules a module imports, by name, as the bundler will ask for them.</summary>
    public IReadOnlyList<string> SiblingImportsOf(string module) =>
        Regex.Matches(Module(module) ?? "", """^import\b[^;]*?\bfrom\s*"\./([^"]+)";""", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>A culture's string catalog as the browser fetches it; empty when the build wrote none.
    /// The neutral one is <c>neutral</c>.</summary>
    public IReadOnlyDictionary<string, string> Catalog(string culture)
    {
        var path = Path.Combine(_outputDirectory, "strings", culture + ".json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!
            : new Dictionary<string, string>();
    }

    /// <summary>The diagnostics that name a file of <paramref name="project"/>.</summary>
    public IEnumerable<string> ErrorsIn(string project) =>
        Errors.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith(_solution.DirectoryOf(project) + Path.DirectorySeparatorChar, StringComparison.Ordinal));
}
