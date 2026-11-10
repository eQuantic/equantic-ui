using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// One build of an app's browser code: the project's compilation, a module for each type the
/// browser reaches, the string catalogs and the bundle. It is what <c>eqc</c> runs, and it lives in
/// the library so a test runs the same thing over a project it wrote to a temporary directory. While
/// it was the CLI's top-level statements nothing could call it, and what it decides (which files
/// are compiled, which name a module claims, what is an entry point) was proven only by building a
/// sample.
/// </summary>
public sealed class AppBuild
{
    private readonly AppBuildOptions _options;
    private readonly TextWriter _output;
    private readonly TextWriter _errors;
    private readonly ComponentCompiler _compiler;
    private readonly string _primarySourceDir;
    private readonly string _intermediateDir;

    /// <summary>What the projects the app references declare; null for an app that references none,
    /// which is built exactly as it was before there was such a layer.</summary>
    private ReferencedModules? _referenced;

    /// <summary>The compilations of the projects the app references, read for what every source of the
    /// build is held to (<see cref="InlineHandlers"/>); empty for an app that references none.</summary>
    private IReadOnlyList<Compilation> _referencedCompilations = [];

    // What this build loaded, which a later build of the same process starts from (LoadFrom).
    private IReadOnlyList<string>? _sourceFiles;
    private IReadOnlyDictionary<ReferencedProject, Compilation>? _built;
    private string? _inputs;

    // What each file's compile gave this build, by full path, which the next build of a served eqc takes
    // as it is for a file the save changed nothing of (#1281), and the ones this build was handed so.
    private readonly Dictionary<string, FileCompile> _fileCompiles = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, FileCompile> _reusable = new Dictionary<string, FileCompile>();

    /// <param name="options">What the build is handed.</param>
    /// <param name="output">Where the build narrates; the console when null.</param>
    /// <param name="errors">Where diagnostics go, in MSBuild's canonical form; the console's error
    /// stream when null.</param>
    public AppBuild(AppBuildOptions options, TextWriter? output = null, TextWriter? errors = null)
    {
        _options = options;
        _output = output ?? Console.Out;
        _errors = errors ?? Console.Error;
        _primarySourceDir = options.SourceDirectories[0];
        _intermediateDir = Path.Combine(_primarySourceDir, "obj", "eQuantic", "ts");
        _compiler = new ComponentCompiler
        {
            SourceMaps = options.SourceMaps,
            SourceRoot = Path.GetFullPath(_primarySourceDir),
            SourceMapDirectory = Path.GetFullPath(_intermediateDir),
        };
    }

    /// <summary>Where each type's TypeScript module is written before it is bundled.</summary>
    public string IntermediateDirectory => _intermediateDir;

    /// <summary>The app's compilation, once <see cref="Load"/> has built it: the projects it
    /// references are among its references as compilations of their own.</summary>
    public Compilation? Compilation { get; private set; }

    /// <summary>
    /// Builds the project's compilation and the scan of its declarations, which every module of the
    /// build is compiled against. False when the build must stop here, which it has said why.
    /// </summary>
    public bool Load()
    {
        // Create full project compilation for better type resolution
        // This enables the compiler to resolve types defined in external files
        Compilation? projectCompilation = null;
        try
        {
            // Write to a log file for debugging since MSBuild might not show console output
            var logPath = Path.Combine(_intermediateDir, "compilation.log");
            Directory.CreateDirectory(_intermediateDir);
            File.WriteAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] Starting compilation setup\n");

            _output.WriteLine("🔍 Attempting to load project compilation...");

            var allSourceFiles = CollectSourceFiles(_output);

            if (allSourceFiles.Count > 0)
            {
                var assemblyPaths = new List<string>();

                // Preferred path: the exact reference set MSBuild resolved for this project, handed to us via
                // --refs. This is what csc itself compiles against, so the semantic model resolves every type
                // the real build does (eQuantic.UI.*, NuGet, project references) with no TFM/config guessing.
                var usingExplicitRefs = false;
                var refsFile = _options.ReferencesFile;
                if (!string.IsNullOrEmpty(refsFile) && File.Exists(refsFile))
                {
                    foreach (var line in File.ReadAllLines(refsFile))
                    {
                        var path = line.Trim();
                        if (path.Length > 0 && File.Exists(path))
                            assemblyPaths.Add(path);
                    }
                    usingExplicitRefs = assemblyPaths.Count > 0;
                    _output.WriteLine($"   Using {assemblyPaths.Count} MSBuild-resolved references from {Path.GetFileName(refsFile)}");

                    // An EMPTY --refs file is not a reason to fall back — it is a reason to stop. The bin-tree
                    // fallback below exists for a human running eqc by hand with no MSBuild at all; reaching it
                    // from a real build means the semantic model is gone, and the compiler's degraded path
                    // passes named arguments through in SYNTACTIC order. That renders a component with the
                    // wrong values and reports nothing, which is the single worst outcome this compiler has.
                    // Fail the build and say what to do about it.
                    if (!usingExplicitRefs)
                    {
                        _errors.WriteLine(
                            $"{refsFile}(1,1): error EQ0002: the MSBuild reference list is empty, so the " +
                            "semantic model would be built from an incomplete compilation and named arguments " +
                            "could be emitted in the wrong order. Rebuild the project (dotnet build) to " +
                            "regenerate it; if this persists, the CompileEQuanticUI target ran without " +
                            "FindReferenceAssembliesForReferences.");
                        return false;
                    }
                }

                if (!usingExplicitRefs)
                {
                    // Fallback for direct CLI use without --refs: the running BCL plus any eQuantic/System/Roslyn
                    // assemblies emitted to the project's bin tree. Scan every TFM/config dir (deduping by file
                    // name) instead of hardcoding one — the old net8.0 hardcode silently dropped all references.
                    assemblyPaths.Add(typeof(object).Assembly.Location);   // System.Private.CoreLib
                    assemblyPaths.Add(typeof(Enumerable).Assembly.Location); // System.Linq
                    assemblyPaths.Add(typeof(List<>).Assembly.Location);   // System.Collections

                    var binRoot = Path.Combine(_primarySourceDir, "bin");
                    if (Directory.Exists(binRoot))
                    {
                        var seen = new HashSet<string>(assemblyPaths.Select(Path.GetFileName)!, StringComparer.OrdinalIgnoreCase);
                        foreach (var dll in Directory.GetFiles(binRoot, "*.dll", SearchOption.AllDirectories))
                        {
                            var fileName = Path.GetFileName(dll);
                            var isCandidate =
                                fileName.StartsWith("eQuantic.UI", StringComparison.OrdinalIgnoreCase) ||
                                fileName.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase) ||
                                fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase);
                            if (isCandidate && seen.Add(fileName))
                                assemblyPaths.Add(dll);
                        }
                        _output.WriteLine($"   Found {assemblyPaths.Count - 3} assemblies in bin tree (fallback)");
                    }
                }

                _output.WriteLine($"   Creating compilation for {allSourceFiles.Count} files with {assemblyPaths.Count} references...");

                // The name the project evaluates to, which the SDK hands over. Inferred from the
                // project file's name, an app that sets <AssemblyName> was compiled here under a name
                // no other assembly knows it by, and a referenced project's InternalsVisibleTo opened
                // nothing to it (found in review of #827). The file's name is MSBuild's own default,
                // and what a build with no SDK behind it has.
                var csprojFiles = Directory.GetFiles(_primarySourceDir, "*.csproj", SearchOption.TopDirectoryOnly);
                var assemblyName = !string.IsNullOrWhiteSpace(_options.AssemblyName)
                    ? _options.AssemblyName.Trim()
                    : csprojFiles.Length > 0 ? Path.GetFileNameWithoutExtension(csprojFiles[0]) : "DynamicAssembly";

                // When we have the complete MSBuild reference set, use it verbatim. Injecting the runtime BCL
                // impl assemblies on top of the targeting-pack ref assemblies would duplicate identities and
                // muddy resolution, so only add the standard fallback set when --refs was not supplied.
                var addStandardReferences = !usingExplicitRefs;

                // The projects the app references are compiled from their own source, each a
                // compilation the app references in place of the project's assembly, so a type of
                // one is a type declared in source for every rule this compiler has (#624).
                IReadOnlyDictionary<ReferencedProject, Compilation>? built = null;
                try
                {
                    var projects = ReferencedProjects();
                    if (projects.Count > 0)
                        projectCompilation = ProjectCompilations.Create(assemblyName, allSourceFiles, assemblyPaths,
                            addStandardReferences, projects, Graph(), _options.ReferenceReader, out built);
                }
                catch (Exception ex)
                {
                    // Never the fallback below: without the project's compilation the build goes on
                    // by name heuristics, and a page that used a type of a referenced project would
                    // be written wrong with no diagnostic, which is the defect this exists to end.
                    // The list itself is read in here for that reason.
                    _errors.WriteLine($"{_options.ProjectsFile}(1,1): error EQ0001: Compilation crash: " +
                        $"the projects the app references could not be compiled from their source: {ex.Message}");
                    return false;
                }

                // An app that references no project is compiled exactly as it was before it could.
                projectCompilation ??= ProjectCompilationHelper.CompilationOf(
                    allSourceFiles,
                    assemblyPaths,
                    assemblyName: assemblyName,
                    addStandardReferences,
                    read: _options.ReferenceReader);

                if (built is { Count: > 0 })
                    _output.WriteLine($"   Compiling {built.Count} referenced project(s) from source: " +
                        string.Join(", ", built.Keys.Select(project => project.AssemblyName)));
                Adopt(projectCompilation, built);
                // What a later build of the same process starts from (LoadFrom).
                _sourceFiles = allSourceFiles.Where(File.Exists).ToList();
                _inputs = Inputs();
                _output.WriteLine($"📚 Loaded project compilation: {assemblyName} ({allSourceFiles.Count} files, {assemblyPaths.Count} refs)");

                // Log success
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] SUCCESS: Loaded {assemblyName} with {allSourceFiles.Count} files\n");
            }
            else
            {
                _output.WriteLine("⚠️  No source files found for project compilation");
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] WARNING: No source files found\n");
            }
        }
        catch (Exception ex)
        {
            // If project compilation fails, continue with minimal compilation
            _output.WriteLine($"⚠️  Project compilation failed: {ex.Message}");
            _output.WriteLine($"   Stack: {ex.StackTrace}");
            _output.WriteLine("   Using minimal compilation per file");

            var logPath = Path.Combine(_intermediateDir, "compilation.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}\n{ex.StackTrace}\n");
        }

        ResolveDependencies(projectCompilation);
        return true;
    }

    /// <summary>
    /// The build's compilation, and what the compiler reads beside it: the projects the app references,
    /// compiled from their source, and one catalogue id per resource class across all of them.
    /// </summary>
    private void Adopt(Compilation compilation, IReadOnlyDictionary<ReferencedProject, Compilation>? built)
    {
        if (built is { Count: > 0 })
        {
            _referenced = new ReferencedModules(built, _primarySourceDir);
            _referencedCompilations = built.Values.ToList();
            _compiler.SetReferencedModules(_referenced);
            // One catalogue id per resource class for the whole build: two projects may
            // each have a Strings, and a file sees only its own project's.
            _compiler.SetResourceIds(new ResourceCatalogIds(compilation));
        }
        _built = built;
        Compilation = compilation;
        _compiler.SetProjectCompilation(compilation);
    }

    private void ResolveDependencies(Compilation? projectCompilation)
    {
        // Initialize dependency resolver by scanning component directories. It is handed the compilation the
        // compiler's model is built from: it asks it for each type's symbol, as the parser does, so who imports
        // a module and who writes it read one answer (#423). Without it the resolver judges a library's base,
        // and an interface, by its name.
        var dependencyResolver = new ComponentDependencyResolver(projectCompilation);
        var componentDirectories = new List<string>(_options.SourceDirectories);

        // Also scan standard component library locations relative to build tool
        var buildDir = AppContext.BaseDirectory;
        var standardComponentsPath = Path.GetFullPath(Path.Combine(buildDir, "..", "..", "..", "..", "eQuantic.UI.Web.Components"));
        if (Directory.Exists(standardComponentsPath))
        {
            componentDirectories.Add(standardComponentsPath);
        }

        dependencyResolver.GeneratedDirectory = _options.GeneratedDirectory;
        dependencyResolver.ScanSourceDirectories(componentDirectories);
        _compiler.SetDependencyResolver(dependencyResolver);
    }

    /// <summary>
    /// Loads the project's compilation from the one <paramref name="previous"/> loaded, in the same
    /// process, and answers false when it cannot, for the caller to <see cref="Load"/> instead (#1278).
    /// <para>
    /// A save changes the text of a file, and loading every reference and every file again cost 3.4s
    /// of each save's compile. The previous compilation keeps its references, already read and bound,
    /// and each file whose text changed has its tree replaced IN PLACE, so the compilation holds its
    /// trees in the order a cold load would. Anything else a cold load would read differently loads
    /// cold: a file added or removed, a changed input the SDK handed (the references, the projects, the
    /// reference sources, the restore graph), or a changed file of a project the app references.
    /// </para>
    /// </summary>
    public bool LoadFrom(AppBuild previous)
    {
        // A file an editor's save removes or holds for a moment, between the look and the read, is a
        // cold load's to read, never a failure of the process that serves the compiles.
        try
        {
            return LoadWarm(previous);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool LoadWarm(AppBuild previous)
    {
        if (previous.Compilation is not { } warm || previous._sourceFiles is not { } before) return false;
        if (previous._inputs != Inputs()) return false;
        var files = CollectSourceFiles(TextWriter.Null).Where(File.Exists).ToList();
        if (!files.SequenceEqual(before, StringComparer.Ordinal)) return false;
        if (previous._built is { } built && built.Any(pair => !IsAsWritten(pair.Value, pair.Key)))
            return false;

        // A file the compilation holds twice is one a tree replaced by path would leave half stale.
        var byPath = warm.SyntaxTrees.Where(tree => tree.FilePath.Length > 0)
            .GroupBy(tree => tree.FilePath, StringComparer.Ordinal).ToList();
        if (byPath.Any(group => group.Count() > 1)) return false;
        var trees = byPath.ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var compilation = warm;
        var replaced = new List<(string File, SyntaxTree Before, SyntaxTree After)>();
        foreach (var file in files)
        {
            if (!trees.TryGetValue(file, out var tree)) return false;
            var text = File.ReadAllText(file);
            if (tree.GetText().ToString() == text) continue;
            var replacement = CSharpSyntaxTree.ParseText(text, ParseDefaults.Options, path: file);
            compilation = compilation.ReplaceSyntaxTree(tree, replacement);
            replaced.Add((file, tree, replacement));
        }

        _output.WriteLine($"📚 Kept the project compilation: {replaced.Count} file(s) changed since the last compile");
        Adopt(compilation, previous._built);
        _sourceFiles = files;
        _inputs = previous._inputs;
        ResolveDependencies(compilation);
        _reusable = Reusable(previous._fileCompiles, replaced);
        return true;
    }

    /// <summary>
    /// The files whose compile the last build gave this one may take as it is (#1281): when the save
    /// changed bodies alone, every file that compiled, is not one the save changed, and read none of them.
    /// After a save that changed anything else, none: a declaration can change what another file's code
    /// binds to, which nothing the last build recorded can see.
    /// </summary>
    private IReadOnlyDictionary<string, FileCompile> Reusable(IReadOnlyDictionary<string, FileCompile> previous,
        IReadOnlyList<(string File, SyntaxTree Before, SyntaxTree After)> replaced)
    {
        var none = new Dictionary<string, FileCompile>();
        if (previous.Count == 0) return none;
        if (replaced.FirstOrDefault(edit => !BodyOnlyEdit.Is(edit.Before, edit.After)) is { File: { } declared })
        {
            _output.WriteLine($"   A declaration changed in {Describe(declared)}: every file is compiled");
            return none;
        }
        var changed = replaced.Select(edit => Path.GetFullPath(edit.File)).ToHashSet(StringComparer.Ordinal);
        return previous.Values
            .Where(compiled => compiled.Succeeded && !changed.Contains(compiled.File)
                && !compiled.Read.Overlaps(changed))
            .ToDictionary(compiled => compiled.File, StringComparer.Ordinal);
    }

    /// <summary>Whether a referenced project's compilation holds its files as they are on disk: every
    /// tree but its friend declarations is one of its files, with the same text, and no file is
    /// missing. A tree whose file was deleted is not among the files, and must not be passed over.</summary>
    private static bool IsAsWritten(Compilation compilation, ReferencedProject project)
    {
        var files = project.SourceFiles().Where(File.Exists).ToHashSet(StringComparer.Ordinal);
        var friends = ProjectCompilations.FriendsPath(project);
        var trees = compilation.SyntaxTrees.Where(tree => tree.FilePath != friends).ToList();
        return trees.Count == files.Count
            && trees.All(tree => files.Contains(tree.FilePath) && tree.GetText().ToString() == File.ReadAllText(tree.FilePath));
    }

    /// <summary>The inputs the SDK hands a build besides the sources, by what they are on disk: a
    /// build whose inputs changed loads cold.</summary>
    private string Inputs()
    {
        // The assemblies the reference list names as well as the list: one built again under the same
        // path leaves the list as it was, and the kept compilation would read its metadata as it was.
        var files = new List<string?>
        {
            _options.ReferencesFile, _options.ProjectsFile, _options.ReferenceSourcesFile, _options.ProjectAssetsFile,
        };
        if (!string.IsNullOrEmpty(_options.ReferencesFile) && File.Exists(_options.ReferencesFile))
            files.AddRange(File.ReadAllLines(_options.ReferencesFile).Select(line => line.Trim()).Where(line => line.Length > 0));
        return string.Join('|', files.Select(file => string.IsNullOrEmpty(file) || !File.Exists(file)
            ? $"{file}:none"
            : $"{file}:{File.GetLastWriteTimeUtc(file).Ticks}:{new FileInfo(file).Length}"));
    }

    /// <summary>
    /// Every file the project's compilation is built from: the project's own sources, the global usings
    /// and the source generators' output its build wrote, and the reference-only sources. Each build
    /// reads them again, so a file a save added or removed is seen.
    /// </summary>
    private List<string> CollectSourceFiles(TextWriter narration)
    {
        // Collect source files only from the PRIMARY source directory (user's project)
        // Skip standard components directory as those are already compiled
        var allSourceFiles = new List<string>();

        if (Directory.Exists(_primarySourceDir))
        {
            narration.WriteLine($"   Scanning project directory: {_primarySourceDir}");
            var files = ProjectCompilationHelper.GetProjectSourceFiles(_primarySourceDir).ToList();
            narration.WriteLine($"   Found {files.Count} source files in project");
            allSourceFiles.AddRange(files);

            // Feed the SDK-generated global usings (obj/*.GlobalUsings.g.cs) to the compilation so the
            // semantic model resolves BCL types used unqualified — e.g. Dictionary<RecordKey, V> under
            // <ImplicitUsings> — exactly as the real build does, without hardcoding a namespace list.
            var globalUsings = ProjectCompilationHelper.GetGeneratedGlobalUsingsFiles(_primarySourceDir).ToList();
            if (globalUsings.Count > 0)
            {
                narration.WriteLine($"   Including {globalUsings.Count} generated global-usings file(s)");
                allSourceFiles.AddRange(globalUsings);
            }

            // SOURCE-GENERATOR output: part of the program csc compiled, so a page calling into it
            // must resolve here too — see GetCompilerGeneratedFiles.
            var generated = ProjectCompilationHelper
                .GetCompilerGeneratedFiles(_primarySourceDir, _options.GeneratedDirectory).ToList();
            if (generated.Count > 0)
            {
                narration.WriteLine($"   Including {generated.Count} compiler-generated file(s)");
                allSourceFiles.AddRange(generated);
            }
        }

        // Reference-only sources (icon packs etc.): their .cs enter the compilation so the semantic model
        // can reach constant initializers for inlining, but they are NOT in the source directories so
        // nothing here is transpiled to a module.
        var refSourcesFile = _options.ReferenceSourcesFile;
        if (!string.IsNullOrEmpty(refSourcesFile) && File.Exists(refSourcesFile))
        {
            var refSourceCount = 0;
            foreach (var dir in File.ReadAllLines(refSourcesFile).Select(l => l.Trim()).Where(l => l.Length > 0))
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in ProjectCompilationHelper.GetProjectSourceFiles(dir))
                {
                    allSourceFiles.Add(file);
                    refSourceCount++;
                }
            }
            if (refSourceCount > 0)
                narration.WriteLine($"   Including {refSourceCount} reference-source file(s) for constant inlining");
        }
        return allSourceFiles;
    }

    /// <summary>
    /// The projects the build compiles from source, of those the SDK listed. One whose directory is
    /// already read another way is left out, whatever the list says: the app's own, a library the
    /// build transpiles whole, or a pack whose sources joined the app's compilation. Its types would
    /// be declared twice, and a name declared twice binds to neither.
    /// </summary>
    /// <summary>Which project references which, read from the assets file the SDK hands with the
    /// projects. Projects handed with no graph cannot be ordered, and the build stops on it.</summary>
    private ProjectGraph Graph() =>
        ProjectGraph.Read(_options.ProjectAssetsFile is { Length: > 0 } assets ? assets
            : throw new InvalidDataException("the projects were handed with no restore graph (--project-assets), which says which project references which"));

    private IReadOnlyList<ReferencedProject> ReferencedProjects()
    {
        var alreadyRead = _options.SourceDirectories.Select(Normalize).ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(_options.ReferenceSourcesFile) && File.Exists(_options.ReferenceSourcesFile))
            foreach (var directory in File.ReadAllLines(_options.ReferenceSourcesFile).Select(line => line.Trim()))
                if (directory.Length > 0) alreadyRead.Add(Normalize(directory));

        return ReferencedProject.ReadList(_options.ProjectsFile)
            .Where(project => !alreadyRead.Contains(project.Directory))
            .ToList();

        static string Normalize(string directory) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
    }

    /// <summary>A file read back for a message: by its place in the app where it is the app's,
    /// and by its place in its own project, named, where it is a referenced project's.</summary>
    private string Describe(string file) =>
        _referenced?.ProjectOf(file) is { } project
            ? $"{project.AssemblyName}: {Path.GetRelativePath(project.Directory, file)}"
            : Path.GetRelativePath(_primarySourceDir, file);

    // Format a diagnostic in MSBuild's canonical form so `Exec` recognises it and fails/warns the build:
    //   path(line,col): error EQ2001: message
    private static string FormatDiagnostic(CompilationError d, string severity)
    {
        var code = string.IsNullOrEmpty(d.Code) ? "EQ0000" : d.Code;
        var path = string.IsNullOrEmpty(d.SourcePath) ? "eQuantic.UI" : d.SourcePath;
        var line = d.Line > 0 ? d.Line : 1;
        var col = d.Column > 0 ? d.Column : 1;
        return $"{path}({line},{col}): {severity} {code}: {d.Message}";
    }

    /// <summary>
    /// How long the last <see cref="CompileAndBundle"/> spent in bun, so a caller timing the build can
    /// tell bundling from compiling: eqc writes it for the edit-to-view measurement (#900).
    /// </summary>
    public TimeSpan BundleElapsed { get; private set; }

    /// <summary>
    /// The files the last <see cref="CompileAndBundle"/> compiled, in order. A served build that kept the
    /// compilation compiles only the files a save can have changed and takes the others as the build
    /// before it compiled them (#1281); every other build compiles every file.
    /// </summary>
    public IReadOnlyList<string> CompiledFiles { get; private set; } = [];

    /// <summary>
    /// Compiles every module, writes the string catalogs and bundles. True when it produced an error,
    /// so the process can exit non-zero and fail the build.
    /// </summary>
    public bool CompileAndBundle()
    {
        BundleElapsed = TimeSpan.Zero;
        var sourceDirs = _options.SourceDirectories;
        var outputDir = _options.OutputDirectory;
        var bunPath = _options.BunPath;
        var hasBun = !string.IsNullOrEmpty(bunPath) && File.Exists(bunPath);
        try
        {
            var hasErrors = false;

            // The escape hatch's inline handlers, refused in every source of the build when the app
            // turned on the Content-Security-Policy (#1269): a strict policy refuses them in the
            // browser, wherever their markup was written.
            if (Compilation is { } app)
            {
                foreach (var refused in InlineHandlers.Refused(app, new[] { app }.Concat(_referencedCompilations)))
                {
                    hasErrors = true;
                    _errors.WriteLine(FormatDiagnostic(refused, "error"));
                }
            }

            // Which names are already taken — see EmittedTwins for why a name collision is an error.
            var written = new EmittedTwins();
            var entryPoints = new List<string>();

            if (!Directory.Exists(_intermediateDir)) Directory.CreateDirectory(_intermediateDir);
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            // GENERATED sources are compiled alongside the app's own: a generator that writes something
            // a page CALLS (a factory surface built from the app's components) has to become a module,
            // or the page names a binding the bundle never defines. They are paired with the project
            // dir so their entry-point rule is the project's own; nothing here is an entry point unless
            // it is a page. Files a generator wrote for other purposes (ASP.NET's public Program, say)
            // pass through the parser and come back empty, exactly as a non-component always has.
            //
            // The list comes from the SAME function the semantic model is built from, and each unit
            // says whether it is generated. That is the whole of #244: this used to be a raw recursive
            // GetFiles filtered by the SHAPE of the path — obj/ and bin/ skipped "unless the path
            // contains /generated/" — so a Release build transpiled obj/Debug's factory surface too.
            var compileUnits = ProjectCompilationHelper
                .GetCompilationUnits(sourceDirs, _options.GeneratedDirectory)
                .GroupBy(unit => unit.Directory);

            // What the written modules import of the projects the app references, still to be
            // compiled: each type's name with a file that declares it.
            var reached = new Queue<ReferencedType>();

            // One module from one compiled type: its warnings, its name's claim, its file and map,
            // and whether the bundle starts from it. `claimedAs` is what makes two claims of a name
            // ONE type, and `describe` reads a file back for the message of a clash.
            void Write(CompilationResult result, string file, bool isEntryCandidate, string claimedAs,
                Func<string, string> describe)
            {
                // Warnings surface whether or not compilation succeeded (e.g. an unconverted
                // construct emitted verbatim). Errors are reported below and fail the build.
                foreach (var warning in result.Warnings)
                {
                    _errors.WriteLine(FormatDiagnostic(warning, "warning"));
                }

                if (!result.Success)
                {
                    hasErrors = true;
                    foreach (var error in result.Errors)
                    {
                        _errors.WriteLine(FormatDiagnostic(error, "error"));
                    }
                    return;
                }

                var tsPath = Path.Combine(_intermediateDir, $"{result.ComponentName}.ts");

                // A twin is named for its TYPE, not its namespace, so two types with one
                // name write the same file and the second wins. Nothing said so: C# is
                // happy — the namespaces differ — and the page died in the browser with
                // the OTHER type's fields, which is the worst way to learn.
                // Namespace-qualified, because that is what makes two claims ONE type.
                // The FILE is still keyed by the bare name — see EmittedTwins for why both
                // halves are needed, and why either alone gets a real case wrong.
                var claim = written.Claim(result.ComponentName, claimedAs, file, result.TypeScript,
                    describe, out var message);
                if (claim == TwinClaim.Collision)
                {
                    _errors.WriteLine($"{file}(1,1): error EQ1005: {message}");
                    hasErrors = true;
                    return;
                }
                // One type across declarations eqc cannot merge. The build goes on with
                // the module already written; the members left out are said out loud,
                // because their absence shows up only in the browser.
                if (claim == TwinClaim.Divided)
                {
                    _errors.WriteLine($"{file}(1,1): warning EQ1006: {message}");
                    return;
                }
                // The same module already on disk: writing it again would rewrite its map
                // with one pointing at a different C# file.
                if (claim == TwinClaim.Repeat) return;

                // Written only when its text changed (#1279): a save that changes one screen writes that
                // screen's module and leaves the others, and their times, as they were.
                var hasMap = !string.IsNullOrEmpty(result.SourceMap);
                OutputFile.Write(tsPath, hasMap
                    ? result.TypeScript + $"\n//# sourceMappingURL={result.ComponentName}.ts.map"
                    : result.TypeScript);
                if (hasMap) OutputFile.Write(tsPath + ".map", result.SourceMap!);

                // A PAGE is always an entry point, wherever its file sits: the server loads
                // it by name, so a page that is only transpiled is a 404 the build never
                // mentioned. The positional rule stays as well — a project laid out the
                // conventional way keeps every module it had before.
                if (result.IsPage || isEntryCandidate)
                {
                    entryPoints.Add(tsPath);
                }

                foreach (var type in result.ReferencedTypes) reached.Enqueue(type);
            }

            var compiledFiles = new List<string>();
            var units = 0;
            foreach (var group in compileUnits)
            {
                var dir = group.Key;

                foreach (var (_, file, isGenerated) in group)
                {
                    // Try to compile - parser will return empty if not a component
                    // This removes restrictions on naming, inheritance patterns, aliases, etc.
                    // A file the save changed nothing of is taken as the build before compiled it, and
                    // hands the catalogs what its compile handed them (#1281).
                    units++;
                    var path = Path.GetFullPath(file);
                    if (_reusable.TryGetValue(path, out var fileCompile))
                    {
                        _compiler.Replay(fileCompile);
                    }
                    else
                    {
                        fileCompile = _compiler.CompileRecorded(file);
                        compiledFiles.Add(file);
                    }
                    _fileCompiles[path] = fileCompile;
                    var results = fileCompile.Results;

                    var relativePath = Path.GetRelativePath(dir, file);
                    var isEntryCandidate = !isGenerated && dir == sourceDirs[0]
                        && (relativePath.StartsWith("Pages")
                            || !relativePath.Contains(Path.DirectorySeparatorChar));

                    foreach (var result in results)
                    {
                        var identity = string.IsNullOrEmpty(result.Namespace)
                            ? result.ComponentName
                            : $"{result.Namespace}.{result.ComponentName}";
                        Write(result, file, isEntryCandidate, identity, path => Path.GetRelativePath(dir, path));
                    }
                }
            }

            CompiledFiles = compiledFiles;
            if (_reusable.Count > 0)
                _output.WriteLine($"   Compiled {compiledFiles.Count} of {units} file(s): the others read nothing the save changed");

            // A type of a referenced project is a module when a written module imports it, and only
            // then (#624): the record a page builds, the static class whose member it reads, the
            // component it places, and whatever those name in turn, in whichever project. So the
            // build follows the imports until none is unmet, compiling a type where it is reached
            // and nothing beside it. A Domain or a Persistence project costs what the browser code
            // uses of it, and a class in it that reads a file is never compiled, so it says nothing.
            var compiled = new HashSet<ReferencedType>();
            while (_referenced is not null && reached.TryDequeue(out var type))
            {
                if (_referenced.ProjectOf(type.File) is not { } project || !compiled.Add(type)) continue;
                foreach (var result in _compiler.CompileType(type))
                {
                    // Claimed as what it is, with its project's assembly: a type of the app and one
                    // of a referenced project that share a full name are two types, and so are two
                    // of one simple name in two namespaces of one file. One name for two types is
                    // the collision, never one type declared twice.
                    Write(result, type.File, isEntryCandidate: false, $"{project.AssemblyName}:{type.Identity}", Describe);
                }
            }

            if (hasErrors) return true;

            StringCatalogs.Write(_compiler, outputDir, _primarySourceDir,
                file => _referenced?.ProjectOf(file) is not null, _output, _errors);

            if (!hasBun && entryPoints.Count > 0)
            {
                // The embedded Bun is a hard requirement — bundling is what produces the page JS. A
                // missing binary must fail the build loudly, never "succeed" without output.
                _errors.WriteLine("❌ Embedded Bun not found — the eQuantic.UI build requires the " +
                    "bundled Bun (restore the platform Runtime package, e.g. eQuantic.UI.Runtime.Osx64).");
                return true;
            }

            if (hasBun && entryPoints.Count > 0)
            {
                // Bundling and the map composition are the library's (ModuleBundler), so the test that
                // proves a thrown error's frame leads back to its C# line runs exactly this. bun runs
                // in the project's folder, where the build has always run it, so a chunk keeps its
                // name wherever eqc itself runs.
                var bundling = System.Diagnostics.Stopwatch.StartNew();
                var bunError = ModuleBundler.Bundle(bunPath!, _primarySourceDir, entryPoints, outputDir,
                    _intermediateDir, _options.SourceMaps, _errors.WriteLine);
                BundleElapsed = bundling.Elapsed;
                if (bunError is not null)
                {
                    _errors.WriteLine("❌ Bun compilation failed:");
                    _errors.WriteLine(bunError);
                    return true;
                }
            }

            _output.WriteLine($"✅ Built at {DateTime.Now:HH:mm:ss}");
            return false;
        }
        catch (Exception ex)
        {
            _errors.WriteLine($"eQuantic.UI(1,1): error EQ0001: Compilation crash: {ex.Message}");
            return true;
        }
    }
}
