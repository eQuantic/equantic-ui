using System.Text.Json;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Which project references which, as NuGet's restore wrote it into the app's assets file (#887).
/// <para>
/// An assembly cannot say it. A constant read from another project is copied into the code that
/// reads it, and a reference assembly keeps no method body and no private member, so the assembly of
/// a library that reads only a constant of a project, or uses one of its types only inside a method,
/// names no reference to that project. Taken from the assemblies, that library's compilation was
/// built without the project, and every name of it the library's code read failed to bind (EQ2006).
/// Restore walks the project files' own references, and every SDK build has its file.
/// </para>
/// </summary>
internal sealed class ProjectGraph
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _references;

    private ProjectGraph(IReadOnlyDictionary<string, IReadOnlyList<string>> references, StringComparer paths)
    {
        _references = references;
        Paths = paths;
    }

    /// <summary>
    /// How two spellings of a project file's path compare on the file system the app is on. The assets
    /// file and the build each write a full path, and one file system takes two paths that differ only
    /// by case for one file where another takes them for two: asked by case everywhere, two projects of
    /// a Linux solution whose paths differ only so were one node of the graph. Every map keyed by a
    /// project file uses this one.
    /// </summary>
    public StringComparer Paths { get; }

    /// <summary>The comparer of the file system <paramref name="file"/> is on, asked as MSBuild asks:
    /// one that ignores case answers for the file under another casing.</summary>
    private static StringComparer PathsWhere(string file)
    {
        var upper = file.ToUpperInvariant();
        var lower = file.ToLowerInvariant();
        return upper != lower && File.Exists(upper) && File.Exists(lower)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    /// <summary>Whether the graph holds <paramref name="projectFile"/>.</summary>
    public bool Holds(string projectFile) => _references.ContainsKey(Path.GetFullPath(projectFile));

    /// <summary>The project files <paramref name="projectFile"/> references directly.</summary>
    public IReadOnlyList<string> ReferencesOf(string projectFile) =>
        _references.TryGetValue(Path.GetFullPath(projectFile), out var references) ? references : [];

    /// <summary>
    /// The graph the assets file at <paramref name="assetsFile"/> holds. Its libraries of the
    /// <c>project</c> type are the projects the app reaches, each named by its project file's path
    /// from the app's own; each target framework lists, for each library, the libraries it depends
    /// on, and a dependency that is a project is a reference. A file that is not there, or does not
    /// hold that shape, throws, and the build stops on it (EQ0001): read as no reference at all, the
    /// graph would build every library without the projects it reads.
    /// </summary>
    public static ProjectGraph Read(string assetsFile)
    {
        if (!File.Exists(assetsFile))
            throw new FileNotFoundException($"the restore graph is not there: '{assetsFile}'", assetsFile);
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(assetsFile));
            var root = document.RootElement;
            var appProject = root.GetProperty("project").GetProperty("restore").GetProperty("projectPath").GetString()
                ?? throw new InvalidDataException("it names no project");
            var appDirectory = Path.GetDirectoryName(Path.GetFullPath(appProject))!;

            // Library keys are "name/version", and a dependency names a library by its name alone.
            var projects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var library in root.GetProperty("libraries").EnumerateObject())
            {
                if (!library.Value.TryGetProperty("type", out var type) || type.GetString() != "project") continue;
                var path = library.Value.TryGetProperty("msbuildProject", out var project) ? project : library.Value.GetProperty("path");
                projects[NameOf(library.Name)] = Path.GetFullPath(Path.Combine(appDirectory, path.GetString()!));
            }

            var paths = PathsWhere(Path.GetFullPath(assetsFile));
            var references = new Dictionary<string, List<string>>(paths);
            foreach (var target in root.GetProperty("targets").EnumerateObject())
            foreach (var library in target.Value.EnumerateObject())
            {
                if (!projects.TryGetValue(NameOf(library.Name), out var from)) continue;
                if (!references.TryGetValue(from, out var list)) references[from] = list = [];
                if (!library.Value.TryGetProperty("dependencies", out var dependencies)) continue;
                foreach (var dependency in dependencies.EnumerateObject())
                    if (projects.TryGetValue(dependency.Name, out var to) && !list.Contains(to, paths)) list.Add(to);
            }

            return new ProjectGraph(
                references.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, paths), paths);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException
            or InvalidDataException or ArgumentException)
        {
            throw new InvalidDataException(
                $"the restore graph at '{assetsFile}' does not hold the shape NuGet writes: {exception.Message}", exception);
        }

        static string NameOf(string key) => key.LastIndexOf('/') is var slash and > 0 ? key[..slash] : key;
    }
}
