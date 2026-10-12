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

    private readonly string _temporary = Directory.CreateTempSubdirectory("eq-solution-").FullName;
    private readonly string _root;

    /// <summary>A solution in a directory of its own.</summary>
    /// <param name="folder">The name of the folder the projects sit in, inside that directory: a name
    /// that holds what a path may hold, a dollar sign, a bar, a space. None puts them in it.</param>
    public Solution(string? folder = null)
    {
        _root = folder is null ? _temporary : Path.Combine(_temporary, folder);
        Directory.CreateDirectory(_root);
    }
    private readonly List<Project> _projects = [];

    private sealed record Project(string Name, string[] References, string GlobalUsings, bool IsApp, bool IsPackage = false,
        string? NamedAssembly = null)
    {
        /// <summary>The assembly's name, which is the project's unless the project names it apart
        /// (<c>&lt;AssemblyName&gt;</c>).</summary>
        public string AssemblyName => NamedAssembly ?? Name;

        public Compilation? Compilation { get; set; }
        public string? Assembly { get; set; }
    }

    /// <summary>The directory the solution's projects are in, each in a folder of its name.</summary>
    public string Root => _root;

    /// <summary>The assembly a project was compiled to by the last <see cref="Build"/>, the app's
    /// included: what .NET loads to answer what the browser is asked.</summary>
    public string AssemblyOf(string project) => _projects.Single(candidate => candidate.Name == project).Assembly!;

    /// <summary>The directory a project's files are in.</summary>
    public string DirectoryOf(string project) => Path.Combine(_root, project);

    /// <summary>A file of a project, by the path the build names it with: a test writes
    /// <c>Screens/Home.cs</c> on every system, and the build reports <c>Screens\Home.cs</c> on Windows.</summary>
    public string FileOf(string project, string relativePath) =>
        Path.GetFullPath(Path.Combine(_root, project, relativePath));

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

    /// <summary>
    /// An assembly the app references that is NOT a project of its solution: a package. It is
    /// compiled like a library and its assembly is in the reference list, and nothing says where its
    /// source is, so the build reads it as an assembly, as it reads every package.
    /// </summary>
    /// <inheritdoc cref="Library"/>
    public Solution Package(string name, params (string Path, string Text)[] files) =>
        Add(new Project(name, [], "", IsApp: false, IsPackage: true), files);

    /// <summary>The app, which is declared last and referenced by nothing.</summary>
    /// <param name="name">The project's name, which is its directory's and its project file's.</param>
    /// <param name="references">The projects it references, each declared before it.</param>
    /// <param name="globalUsings">Global usings its build generates beside the implicit ones.</param>
    /// <param name="assemblyName">The name its project file gives its assembly, when that is not the
    /// project's own (<c>&lt;AssemblyName&gt;</c>): the project file keeps the project's name, and
    /// the assembly and what the SDK hands the build carry this one.</param>
    /// <param name="files">Its source: each file's path inside the project, and its text.</param>
    public Solution App(string name, string[]? references = null, string globalUsings = "",
        string? assemblyName = null, params (string Path, string Text)[] files) =>
        Add(new Project(name, references ?? [], globalUsings, IsApp: true, NamedAssembly: assemblyName), files);

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
    /// <param name="bun">The bundler, for a solution whose app has an entry point (a file at its
    /// root, or a page) and is to be RUN. Without one the build stops at the modules it wrote.</param>
    /// <param name="whileEqcRuns">Something held from after the projects are compiled until eqc's
    /// build ends: a file the C# compiler could read and eqc then cannot.</param>
    /// <param name="handAssemblyName">False builds the app as an SDK that hands eqc no assembly name
    /// does, which is every build before the review of #827: the build takes the project file's.</param>
    public Built Build(bool handProjects = true, string? bun = null, Func<IDisposable>? whileEqcRuns = null,
        bool handAssemblyName = true)
    {
        foreach (var project in _projects) Compile(project);

        var app = _projects.Single(project => project.IsApp);
        var libraries = _projects.Where(project => !project.IsApp).ToList();
        var obj = Path.Combine(DirectoryOf(app.Name), "obj", "Debug", "net10.0");
        var referencesFile = Path.Combine(obj, "equantic.refs.txt");
        File.WriteAllLines(referencesFile, Framework.Value.Concat(libraries.Select(library => library.Assembly!)));
        var projectsFile = Path.Combine(obj, "equantic.projects.txt");
        // Two lines a project, as the SDK writes it: the assembly, then the project file (#836).
        File.WriteAllLines(projectsFile, libraries.Where(library => !library.IsPackage).SelectMany(library =>
            new[] { library.Assembly!, Path.Combine(DirectoryOf(library.Name), library.Name + ".csproj") }));

        var output = new StringWriter();
        var errors = new StringWriter();
        var outputDirectory = Path.Combine(DirectoryOf(app.Name), "wwwroot", "_equantic");
        var build = new AppBuild(new AppBuildOptions
        {
            SourceDirectories = [DirectoryOf(app.Name)],
            OutputDirectory = outputDirectory,
            ReferencesFile = referencesFile,
            // The SDK hands a list only when it wrote a line: an app that references no project
            // is handed none, and an empty one is a list that does not hold (#835).
            ProjectsFile = handProjects && libraries.Any(library => !library.IsPackage) ? projectsFile : null,
            AssemblyName = handAssemblyName ? app.AssemblyName : null,
            BunPath = bun,
            // One reference per assembly for the whole test process (#481).
            ReferenceReader = TestReferences.Of,
            SourceMaps = SourceMapMode.None,
        }, output, errors);
        using var held = whileEqcRuns?.Invoke();
        // A build that cannot stand on what it was handed stops there, having said why.
        var failed = !build.Load() || build.CompileAndBundle();
        return new Built(this, build, failed, output.ToString(), errors.ToString(), outputDirectory);
    }

    /// <summary>One project compiled as MSBuild compiles it, its assembly written under its
    /// <c>bin</c>, the app's too. A project that does not compile is a test whose C# is wrong, not a
    /// case.</summary>
    private void Compile(Project project)
    {
        var directory = DirectoryOf(project.Name);
        var trees = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.StartsWith(Path.Combine(directory, "bin") + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), ParseDefaults.Options, path: file));
        var references = Framework.Value.Select(path => TestReferences.Of(path))
            .Concat(Reached(project).Select(reference => reference.Compilation!.ToMetadataReference()));
        var compilation = CSharpCompilation.Create(project.AssemblyName, trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .Should().BeEmpty($"{project.Name} is C# that compiles");
        project.Compilation = compilation;

        var assembly = Path.Combine(directory, "bin", "Debug", "net10.0", project.AssemblyName + ".dll");
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
        try { Directory.Delete(_temporary, recursive: true); } catch (IOException) { /* best effort */ }
    }
}
