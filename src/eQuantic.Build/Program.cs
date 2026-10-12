// CLI entry point for eQuantic.UI Compiler: it reads its arguments and runs the build the compiler
// library holds (AppBuild), so a test runs the same build the SDK does.
using eQuantic.UI.Compiler.Services;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: eqc <source-dir> <output-dir> [--bun <path>] [--source-maps full|external|none] [--watch]");
    return 1;
}

// TrimEntries: the SDK passes the source dirs as "$(MSBuildProjectDirectory);$(_StandardComponentsDir)",
// and _StandardComponentsDir is defined as multi-line XML, so its value carries leading whitespace/
// newline. Without trimming, Directory.Exists fails on the standard-components path → the built-in
// components (Grid/Box/…) are never transpiled, and pages referencing them break with "X is not defined".
var sourceDirs = args[0].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
var outputDir = args[1].Trim();
var bunPath = args.ToList().Contains("--bun") ? args[args.ToList().IndexOf("--bun") + 1] : null;
var isWatchMode = args.Any(a => a == "--watch");
// --refs <file>: a newline-delimited list of the assemblies MSBuild resolved for this project
// (@(ReferencePathWithRefAssemblies)). Feeding these to the semantic model makes eqc's type resolution
// identical to the real csc build — eQuantic.UI.Core/Components, NuGet and project references all resolve,
// independent of TargetFramework or configuration. Without it, base types like HtmlElement.Children
// (IList<IComponent>) stay unresolved and member calls degrade to naive camel-casing (.Add → .add).
var refsFile = args.ToList().Contains("--refs") ? args[args.ToList().IndexOf("--refs") + 1] : null;
// --ref-sources <file>: newline-delimited DIRECTORIES whose .cs join the compilation as
// SEMANTIC-ONLY reference sources (never transpiled). This is what lets eqc inline external
// constants at the use site — an icon pack's `static readonly IconGlyph` needs its INITIALIZER,
// which metadata (via --refs) does not carry; the pack's tools/source supplies it here.
var refSourcesFile = args.ToList().Contains("--ref-sources") ? args[args.ToList().IndexOf("--ref-sources") + 1] : null;
// --generated <dir>: the SOURCE-GENERATOR output directory for the configuration being built. The
// SDK knows which one that is and eqc cannot: a project built in both configurations has a
// generated tree under obj/Debug AND obj/Release, and sweeping obj would take every generated type
// twice — which resolves to neither, while the C# build stays perfectly happy.
var generatedDir = args.ToList().Contains("--generated") ? args[args.ToList().IndexOf("--generated") + 1].Trim() : null;
// --source-maps full|external|none (#352): what each module's map carries. `full` links a map with
// the C# inside it, `external` writes one without the C# and without the link, `none` writes none.
// The SDK passes full in Debug and none everywhere else; a direct run keeps full.
var sourceMapArg = args.ToList().Contains("--source-maps") ? args[args.ToList().IndexOf("--source-maps") + 1].Trim() : "full";
var sourceMaps = sourceMapArg.ToLowerInvariant() switch
{
    "full" => eQuantic.UI.Compiler.SourceMapMode.Full,
    "external" => eQuantic.UI.Compiler.SourceMapMode.External,
    "none" => eQuantic.UI.Compiler.SourceMapMode.None,
    _ => throw new ArgumentException($"--source-maps takes full, external or none, not '{sourceMapArg}'."),
};

var build = new AppBuild(new AppBuildOptions
{
    SourceDirectories = sourceDirs,
    OutputDirectory = outputDir,
    BunPath = bunPath,
    ReferencesFile = refsFile,
    ReferenceSourcesFile = refSourcesFile,
    GeneratedDirectory = generatedDir,
    SourceMaps = sourceMaps,
});

// The project's compilation and the scan of its declarations. A build it cannot stand on has said
// why (an empty reference list is EQ0002) and stops here.
if (!build.Load()) return 1;

if (isWatchMode)
{
    Console.WriteLine($"👀 eQuantic.UI: Watching {sourceDirs.Length} directories...");
}
else
{
    Console.WriteLine($"🔨 eQuantic.UI: Compiling components from {sourceDirs.Length} directories");
}

Console.WriteLine($"   Intermediate: {build.IntermediateDirectory}");
Console.WriteLine($"   Output:       {outputDir}");

// Initial compilation
var initialBuildHadErrors = build.CompileAndBundle();

if (isWatchMode)
{
    var debouncer = new Debouncer(TimeSpan.FromMilliseconds(100));
    // Watch all source directories
    var watchers = new List<FileSystemWatcher>();
    foreach (var dir in sourceDirs)
    {
        var watcher = new FileSystemWatcher(dir, "*.cs");
        watcher.IncludeSubdirectories = true;
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;

        FileSystemEventHandler onChanged = (sender, e) =>
        {
            if (e.FullPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                e.FullPath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                return;

            debouncer.Debounce(() => {
                Console.WriteLine($"🔄 Change detected in {Path.GetFileName(e.FullPath)}. Recompiling...");
                build.CompileAndBundle();
            });
        };

        watcher.Changed += onChanged;
        watcher.Created += onChanged;
        watcher.Deleted += onChanged;
        watcher.Renamed += (s, e) => onChanged(s, e);
        watcher.EnableRaisingEvents = true;
        watchers.Add(watcher);
    }

    await Task.Delay(-1);
}

// Non-zero exit fails the MSBuild `Exec` step when transpilation hit unsupported constructs.
return initialBuildHadErrors ? 1 : 0;

class Debouncer
{
    private readonly TimeSpan _delay;
    private CancellationTokenSource? _cts;

    public Debouncer(TimeSpan delay)
    {
        _delay = delay;
    }

    public void Debounce(Action action)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Delay(_delay, token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                action();
            }
        });
    }
}
