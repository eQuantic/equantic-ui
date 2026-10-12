using eQuantic.UI.Compiler.Services;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.Tests.ReferencedProjects;

/// <summary>
/// An app and the projects it references, written to a temporary directory and built AS THE SDK BUILDS
/// THEM: each referenced project is compiled to an assembly first, as MSBuild compiles it, and eqc's
/// own build (<see cref="AppBuild"/>) is then handed what the SDK hands it, the reference list with
/// those assemblies in it and the list of projects with the directory each one's source is in.
/// <para>
/// So a test here runs the loop the CLI runs, over real files: which projects are read, which of
/// their types become modules, what each module imports and what the build refuses. A snippet
/// compiled on its own (<c>CompileSource</c>) has one compilation and cannot ask any of it (#624).
/// </para>
/// <para>
/// The app's files sit in a folder that is not <c>Pages</c> and none is a <c>[Page]</c>, so the build
/// has no entry point and needs no bundler: what is asserted is the module eqc WROTE for each type.
/// </para>
/// </summary>
internal sealed class Solution : IDisposable
{
    /// <summary>The usings an SDK-style project gets, written where its build writes them.</summary>
    private const string ImplicitUsings =
        """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Threading.Tasks;

        """;

    /// <summary>The framework, the vocabulary and the component library, read once per process (#481).</summary>
    private static readonly Lazy<string[]> Framework = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Append(typeof(eQuantic.UI.Primitives.VisualNode).Assembly.Location)
            .Append(typeof(eQuantic.UI.Components.UI).Assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .ToArray());

    private readonly string _root = Directory.CreateTempSubdirectory("eq-solution-").FullName;
    private readonly List<Project> _projects = [];

    private sealed record Project(string Name, string[] References, string GlobalUsings, bool IsApp)
    {
        public Compilation? Compilation { get; set; }
        public string? Assembly { get; set; }
    }

    /// <summary>The directory a project's files are in.</summary>
    public string DirectoryOf(string project) => Path.Combine(_root, project);

    /// <summary>A file of a project, as the developer wrote it.</summary>
    public string FileOf(string project, string relativePath) => Path.Combine(_root, project, relativePath);

    /// <summary>
    /// A class library the app references, directly or through another.
    /// </summary>
    /// <param name="name">The project's name, which is its assembly's and its directory's.</param>
    /// <param name="references">The projects it references, each declared before it.</param>
    /// <param name="globalUsings">Global usings its build generates beside the implicit ones (a
    /// <c>&lt;Using&gt;</c> item of its project file).</param>
    /// <param name="files">Its source: each file's path inside the project, and its text.</param>
    public Solution Library(string name, string[]? references = null, string globalUsings = "",
        params (string Path, string Text)[] files) =>
        Add(new Project(name, references ?? [], globalUsings, IsApp: false), files);

    /// <summary>The app, which is declared last and referenced by nothing.</summary>
    /// <inheritdoc cref="Library"/>
    public Solution App(string name, string[]? references = null, string globalUsings = "",
        params (string Path, string Text)[] files) =>
        Add(new Project(name, references ?? [], globalUsings, IsApp: true), files);

    private Solution Add(Project project, (string Path, string Text)[] files)
    {
        var directory = DirectoryOf(project.Name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, project.Name + ".csproj"), "<Project />\n");
        foreach (var (path, text) in files) Write(project.Name, path, text);
        // Where the build of a project writes its generated usings, which eqc reads from there.
        Write(project.Name, Path.Combine("obj", "Debug", "net10.0", project.Name + ".GlobalUsings.g.cs"),
            ImplicitUsings + project.GlobalUsings);
        _projects.Add(project);
        return this;
    }

    /// <summary>Writes, or writes over, one file of a project.</summary>
    public Solution Write(string project, string relativePath, string text)
    {
        var path = FileOf(project, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return this;
    }

    /// <summary>
    /// Compiles every referenced project to its assembly, checks the whole solution is C# that
    /// compiles, and runs eqc's build over the app.
    /// </summary>
    /// <param name="handProjects">False builds the app as an SDK that hands eqc no list of projects
    /// does, which is every build before #624: the referenced projects reach it as assemblies alone.</param>
    public Built Build(bool handProjects = true)
    {
        foreach (var project in _projects) Compile(project);

        var app = _projects.Single(project => project.IsApp);
        var libraries = _projects.Where(project => !project.IsApp).ToList();
        var obj = Path.Combine(DirectoryOf(app.Name), "obj", "Debug", "net10.0");
        var referencesFile = Path.Combine(obj, "equantic.refs.txt");
        File.WriteAllLines(referencesFile, Framework.Value.Concat(libraries.Select(library => library.Assembly!)));
        var projectsFile = Path.Combine(obj, "equantic.projects.txt");
        File.WriteAllLines(projectsFile, libraries.Select(library =>
            $"{library.Assembly}|{Path.Combine(DirectoryOf(library.Name), library.Name + ".csproj")}"));

        var output = new StringWriter();
        var errors = new StringWriter();
        var outputDirectory = Path.Combine(DirectoryOf(app.Name), "wwwroot", "_equantic");
        var build = new AppBuild(new AppBuildOptions
        {
            SourceDirectories = [DirectoryOf(app.Name)],
            OutputDirectory = outputDirectory,
            ReferencesFile = referencesFile,
            ProjectsFile = handProjects ? projectsFile : null,
            // One reference per assembly for the whole test process (#481).
            ReferenceReader = TestReferences.Of,
            SourceMaps = SourceMapMode.None,
        }, output, errors);
        build.Load().Should().BeTrue($"the build stands on its references:\n{errors}");
        var failed = build.CompileAndBundle();
        return new Built(this, build, failed, output.ToString(), errors.ToString(), outputDirectory);
    }

    /// <summary>One project compiled as MSBuild compiles it, its assembly written under its
    /// <c>bin</c>. A project that does not compile is a test whose C# is wrong, not a case.</summary>
    private void Compile(Project project)
    {
        var directory = DirectoryOf(project.Name);
        var trees = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.StartsWith(Path.Combine(directory, "bin") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), ParseDefaults.Options, path: file));
        var references = Framework.Value.Select(path => TestReferences.Of(path))
            .Concat(Reached(project).Select(reference => reference.Compilation!.ToMetadataReference()));
        var compilation = CSharpCompilation.Create(project.Name, trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .Should().BeEmpty($"{project.Name} is C# that compiles");
        project.Compilation = compilation;
        if (project.IsApp) return;

        var assembly = Path.Combine(directory, "bin", "Debug", "net10.0", project.Name + ".dll");
        Directory.CreateDirectory(Path.GetDirectoryName(assembly)!);
        using (var image = File.Create(assembly))
            compilation.Emit(image).Success.Should().BeTrue($"{project.Name} emits its assembly");
        project.Assembly = assembly;
    }

    /// <summary>The projects one reaches, directly or through another, as MSBuild lets references flow.</summary>
    private IEnumerable<Project> Reached(Project project)
    {
        var reached = new List<Project>();
        void Follow(Project from)
        {
            foreach (var name in from.References)
            {
                var reference = _projects.Single(candidate => candidate.Name == name);
                if (reached.Contains(reference)) continue;
                reached.Add(reference);
                Follow(reference);
            }
        }
        Follow(project);
        return reached;
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
    }
}
