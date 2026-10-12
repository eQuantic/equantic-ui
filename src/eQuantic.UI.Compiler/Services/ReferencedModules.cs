using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// What the projects an app references declare, in a layer of its own beside the app's scan
/// (<see cref="ComponentDependencyResolver"/>): which project a file belongs to, and whether a name
/// one of its files declares becomes a module, judged by that project's own compilation and the rules
/// the app's own types are judged by.
/// <para>
/// A layer apart because modules share one flat namespace and the app's scan answers by NAME: a
/// project the browser never reaches may declare a type of any name, and that name must not become
/// importable anywhere a file merely mentions it. A name of this layer is asked for together with the
/// files a symbol said declare it (<see cref="ReferencedNames"/>), so the question is always about
/// one declaration.
/// </para>
/// <para>
/// Each project is scanned when a name of it is first asked for. A Domain or a Persistence project
/// no page reaches is parsed, and nothing more.
/// </para>
/// </summary>
internal sealed class ReferencedModules
{
    private readonly IReadOnlyList<ReferencedProject> _projects;
    private readonly IReadOnlyDictionary<ReferencedProject, Compilation> _compilations;
    private readonly Dictionary<ReferencedProject, ComponentDependencyResolver> _scans = new();

    /// <param name="compilations">Each referenced project the build compiles, with its compilation.</param>
    public ReferencedModules(IReadOnlyDictionary<ReferencedProject, Compilation> compilations)
    {
        _compilations = compilations;
        // The deepest directory first: a project inside another's directory holds its own files.
        _projects = compilations.Keys.OrderByDescending(project => project.Directory.Length).ToList();
    }

    /// <summary>The referenced project whose source <paramref name="file"/> is, or null for a file of
    /// the app's own, of the SDK's libraries, or of nothing this build reads.</summary>
    public ReferencedProject? ProjectOf(string file)
    {
        if (string.IsNullOrEmpty(file)) return null;
        foreach (var project in _projects)
            if (project.Holds(file))
                return project;
        return null;
    }

    /// <summary>Whether <paramref name="name"/>, declared in one of <paramref name="declaredIn"/>,
    /// becomes a module of the bundle when it is reached.</summary>
    public bool IsModule(string name, IEnumerable<string> declaredIn) =>
        Scans(declaredIn).Any(scan => scan.IsModule(name));

    /// <summary>Whether <paramref name="name"/> is the twin of a nested type the build writes no
    /// module for, as <see cref="ComponentDependencyResolver.IsKeptOut"/> answers for the app's own.</summary>
    public bool IsKeptOut(string name, IEnumerable<string> declaredIn) =>
        Scans(declaredIn).Any(scan => scan.IsKeptOut(name));

    private IEnumerable<ComponentDependencyResolver> Scans(IEnumerable<string> files) =>
        files.Select(ProjectOf).OfType<ReferencedProject>().Distinct().Select(ScanOf);

    private ComponentDependencyResolver ScanOf(ReferencedProject project) =>
        _scans.TryGetValue(project, out var scan)
            ? scan
            : _scans[project] = ComponentDependencyResolver.From(_compilations[project]);
}
