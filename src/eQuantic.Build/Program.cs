// CLI entry point for eQuantic.UI Compiler: it reads its arguments and runs the build the compiler
// library holds (AppBuild), so a test runs the same build the SDK does.
using eQuantic.UI.Compiler.Services;

// The Server's bundle of the runtime holds modules eqc transpiled from the SDK's own C#, each with its
// map beside it: this composes the bundle's map with theirs, so the overlay names an SDK frame by its
// C# (#1108). Named from the source tree and carrying no text, since the map ships in the Server.
if (args is ["compose-map", var bundleMap, "--names-from", var namesFrom])
{
    try
    {
        SourceMapComposer.ComposeFile(bundleMap, namesFrom);
        return 0;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException
        or System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"eqc: {bundleMap} could not be composed: {ex.Message}");
        return 1;
    }
}

// The SDK hands its arguments in a file, `@<file>`, one a line, so no shell reads a path or a name
// on its way here (#836). A run by hand may still write them out.
IReadOnlyList<string> arguments;
AppBuildOptions options;
try
{
    arguments = AppBuildOptions.ReadArguments(args);
    options = AppBuildOptions.FromArguments(arguments);
}
catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"eqc: {ex.Message}");
    Console.Error.WriteLine("Usage: eqc <source-dir>[;<source-dir>...] <output-dir> [--refs <file>] [--projects <file> --project-assets <file>] [--assembly-name <name>] [--ref-sources <file>] [--generated <dir>] [--bun <path>] [--source-maps full|external|none] [--watch]");
    Console.Error.WriteLine("       eqc @<file>, the same arguments one a line");
    Console.Error.WriteLine("       eqc compose-map <map> --names-from <dir>");
    return 1;
}
var isWatchMode = options.Watch;
var sourceDirs = options.SourceDirectories;
var outputDir = options.OutputDirectory;

var build = new AppBuild(options);

// The project's compilation and the scan of its declarations. A build it cannot stand on has said
// why (an empty reference list is EQ0002) and stops here.
if (!build.Load()) return 1;
var loadedAt = DateTimeOffset.UtcNow;

if (isWatchMode)
{
    Console.WriteLine($"👀 eQuantic.UI: Watching {sourceDirs.Count} directories...");
}
else
{
    Console.WriteLine($"🔨 eQuantic.UI: Compiling components from {sourceDirs.Count} directories");
}

Console.WriteLine($"   Intermediate: {build.IntermediateDirectory}");
Console.WriteLine($"   Output:       {outputDir}");

// Initial compilation
var initialBuildHadErrors = build.CompileAndBundle();

// The edit-to-view measurement (#900) names a file in EQC_TIMINGS, and each compile then appends one
// line to it: when this process started, when the compilation was loaded and when the build ended, in
// Unix milliseconds, and how long bun took inside it. That is what tells eqc's time and bun's apart
// from MSBuild's around them and from the browser's after them. Nothing is written otherwise.
if (Environment.GetEnvironmentVariable("EQC_TIMINGS") is { Length: > 0 } timings)
{
    var startedAt = new DateTimeOffset(System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime());
    var line = System.Text.Json.JsonSerializer.Serialize(new
    {
        started = startedAt.ToUnixTimeMilliseconds(),
        loaded = loadedAt.ToUnixTimeMilliseconds(),
        ended = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        bunMs = (long)build.BundleElapsed.TotalMilliseconds,
    });
    try
    {
        File.AppendAllText(timings, line + "\n");
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        // A measurement nobody can read back is not a reason to fail a build that compiled.
        Console.Error.WriteLine($"eqc: EQC_TIMINGS could not be written: {ex.Message}");
    }
}

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
