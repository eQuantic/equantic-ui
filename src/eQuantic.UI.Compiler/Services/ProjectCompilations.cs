using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The app's compilation over the projects it references, each one a compilation of its own.
/// <para>
/// A referenced project used to reach the build as the assembly MSBuild resolved, and a type read
/// from an assembly has no source: every rule that asks whether a type is declared in source (which
/// is how the compiler tells a type it writes a module for) answered no, so a page that named one
/// built with no diagnostic and failed in the browser (#624). Read through a reference to the
/// project's own COMPILATION, the same type is declared in source, in the project's file. That is
/// how Roslyn itself models a project reference.
/// </para>
/// <para>
/// One compilation per project, never the projects' sources added to the app's: each keeps its own
/// <c>global using</c> directives and its own internal types, so a name one project's directive
/// brings in is not ambiguous in another, and two projects that each declare an internal type of one
/// full name are two types.
/// </para>
/// </summary>
internal static class ProjectCompilations
{
    /// <summary>
    /// The app's compilation, referencing each of <paramref name="projects"/> as a compilation in
    /// place of its assembly.
    /// </summary>
    /// <param name="assemblyName">The app's assembly name.</param>
    /// <param name="sourceFiles">The app's own source.</param>
    /// <param name="assemblyPaths">The reference list MSBuild resolved for the app. It holds what the
    /// app's projects need too, since references flow, so each project is compiled against it.</param>
    /// <param name="addStandardReferences">See <see cref="ProjectCompilationHelper.CreateCompilationFromSources"/>.</param>
    /// <param name="projects">The referenced projects, in any order.</param>
    /// <param name="built">Each project a compilation was built for, with that compilation. A project
    /// with no C# source (another language's) keeps its assembly and is not here.</param>
    public static Compilation Create(string assemblyName, IEnumerable<string> sourceFiles,
        IEnumerable<string> assemblyPaths, bool addStandardReferences,
        IReadOnlyList<ReferencedProject> projects, out IReadOnlyDictionary<ReferencedProject, Compilation> built)
    {
        var sources = projects.ToDictionary(project => project, project => project.SourceFiles());
        var compiled = projects.Where(project => sources[project].Any(IsItsOwnSource)).ToList();

        // The assemblies of the projects compiled here are not loaded: a type read from both its
        // compilation and its assembly is two types of one name, and every use of it ambiguous.
        var metadata = ProjectCompilationHelper.LoadReferences(assemblyPaths, addStandardReferences,
            compiled.Select(project => project.AssemblyFileName).ToHashSet(StringComparer.OrdinalIgnoreCase));

        // In the order the assemblies reference one another, each project referencing the ones it
        // reaches, directly or through another: a type of a third project named in a signature of a
        // second resolves only where the third is referenced too.
        var byAssembly = new Dictionary<string, ReferencedProject>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in compiled) byAssembly.TryAdd(project.AssemblyName, project);

        var compilations = new Dictionary<ReferencedProject, Compilation>();
        var reaches = new Dictionary<ReferencedProject, List<ReferencedProject>>();

        List<ReferencedProject> Reaches(ReferencedProject project, HashSet<ReferencedProject> onPath)
        {
            if (reaches.TryGetValue(project, out var known)) return known;
            var reached = new List<ReferencedProject>();
            // Assemblies cannot reference one another in a circle, so a project met again on its own
            // path is a list written by hand: it is left out where it closes the circle.
            if (!onPath.Add(project)) return reached;
            foreach (var name in project.AssemblyReferences)
            {
                if (!byAssembly.TryGetValue(name, out var dependency) || dependency == project) continue;
                foreach (var further in Reaches(dependency, onPath).Append(dependency))
                    if (!reached.Contains(further)) reached.Add(further);
            }
            onPath.Remove(project);
            return reaches[project] = reached;
        }

        Compilation Build(ReferencedProject project)
        {
            if (compilations.TryGetValue(project, out var known)) return known;
            var references = new List<MetadataReference>(metadata);
            foreach (var dependency in Reaches(project, []))
                references.Add(Build(dependency).ToMetadataReference());
            var trees = Parse(sources[project]);
            if (Friends(project) is { } friends) trees.Add(friends);
            return compilations[project] = CSharpCompilation.Create(
                project.AssemblyName, trees, references, ProjectCompilationHelper.Options);
        }

        var appReferences = new List<MetadataReference>(metadata);
        foreach (var project in compiled) appReferences.Add(Build(project).ToMetadataReference());

        built = compilations;
        return CSharpCompilation.Create(assemblyName, Parse(sourceFiles), appReferences, ProjectCompilationHelper.Options);
    }

    /// <summary>A file the project's own directory holds, as against the global usings its build
    /// generated: a project with none of the first is not written in C#.</summary>
    private static bool IsItsOwnSource(string file) =>
        !file.EndsWith(".GlobalUsings.g.cs", StringComparison.Ordinal);

    /// <summary>The files that exist, parsed side by side and kept in the order given: a referenced
    /// project can hold hundreds of files the browser never reaches, and each is parsed all the same.</summary>
    private static List<SyntaxTree> Parse(IEnumerable<string> files) =>
        files.Where(File.Exists)
            .AsParallel().AsOrdered()
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), ParseDefaults.Options, path: file))
            .ToList();

    /// <summary>The project's friend declarations as source, since the assembly that carried them is
    /// not loaded; null when it declares none.</summary>
    private static SyntaxTree? Friends(ReferencedProject project)
    {
        if (project.FriendAssemblies.Count == 0) return null;
        var text = string.Concat(project.FriendAssemblies.Select(friend =>
            $"[assembly: System.Runtime.CompilerServices.InternalsVisibleTo({SymbolDisplay.FormatLiteral(friend, quote: true)})]\n"));
        return CSharpSyntaxTree.ParseText(text, ParseDefaults.Options,
            path: Path.Combine(project.Directory, "obj", "eQuantic", project.AssemblyName + ".Friends.g.cs"));
    }
}
