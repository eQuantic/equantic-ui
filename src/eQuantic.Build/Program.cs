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
    Console.Error.WriteLine("Usage: eqc <source-dir>[;<source-dir>...] <output-dir> [--refs <file>] [--projects <file> --project-assets <file>] [--assembly-name <name>] [--ref-sources <file>] [--generated <dir>] [--bun <path>] [--source-maps full|external|none] [--serve]");
    Console.Error.WriteLine("       eqc @<file>, the same arguments one a line");
    Console.Error.WriteLine("       eqc compose-map <map> --names-from <dir>");
    return 1;
}

if (options.Serve) return Serve(options);

var processStarted = new DateTimeOffset(System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime());
var build = new AppBuild(options);

// The project's compilation and the scan of its declarations. A build it cannot stand on has said
// why (an empty reference list is EQ0002) and stops here.
if (!build.Load()) return 1;
var loadedAt = DateTimeOffset.UtcNow;

Console.WriteLine($"🔨 eQuantic.UI: Compiling components from {options.SourceDirectories.Count} directories");
Console.WriteLine($"   Intermediate: {build.IntermediateDirectory}");
Console.WriteLine($"   Output:       {options.OutputDirectory}");

var hadErrors = build.CompileAndBundle();
WriteTimings(processStarted, loadedAt, build);

// Non-zero exit fails the MSBuild step when transpilation hit unsupported constructs.
return hadErrors ? 1 : 0;

// SERVED (#1276): the server of an app in development starts eqc once, with the response file the
// build wrote, and writes `compile` on its standard input for each save, where it used to start a new
// MSBuild and a new eqc for every one. Each compile starts from the compilation the one before it
// loaded, with the files the save changed replaced in it, and loads it cold when anything else a load
// reads changed (#1278). It answers in ONE line of standard output, `@eqc ` and a JSON object
// that says whether it compiled and holds its error lines, whatever it wrote before that line. The
// end of the input is the server gone, and the process goes with it: nothing outlives its owner.
static int Serve(AppBuildOptions options)
{
    Console.WriteLine($"👀 eQuantic.UI: serving compiles of {options.SourceDirectories.Count} directories, one for each `compile` read");
    // Loaded before the first request, so the first save pays for loading neither the compiler nor the
    // project: each compile starts from the compilation the one before it loaded, and replaces only the
    // files a save changed (#1278).
    AppBuild? last = new AppBuild(options, TextWriter.Null, TextWriter.Null);
    if (!last.Load()) last = null;
    string? request;
    while ((request = Console.In.ReadLine()) is not null)
    {
        if (request.Trim() != "compile") continue;
        var started = DateTimeOffset.UtcNow;
        var errors = new StringWriter();
        var build = new AppBuild(options, Console.Out, errors);
        var compiled = (last is not null && build.LoadFrom(last)) || build.Load();
        var loaded = DateTimeOffset.UtcNow;
        if (compiled) last = build;
        if (compiled) compiled = !build.CompileAndBundle();
        WriteTimings(started, loaded, build);
        var said = errors.ToString();
        Console.Error.Write(said);
        var answer = System.Text.Json.JsonSerializer.Serialize(new
        {
            compiled,
            errors = said.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        });
        Console.Out.WriteLine("@eqc " + answer);
        Console.Out.Flush();
    }
    return 0;
}

// The edit-to-view measurement (#900) names a file in EQC_TIMINGS, and each compile then appends one
// line to it: when the compile started (the process's start for a single build, the request for a
// served one), when the compilation was loaded and when the build ended, in Unix milliseconds, and
// how long bun took inside it. That is what tells eqc's time and bun's apart from what runs around
// them and from the browser's after them. Nothing is written otherwise.
static void WriteTimings(DateTimeOffset started, DateTimeOffset loaded, AppBuild build)
{
    if (Environment.GetEnvironmentVariable("EQC_TIMINGS") is not { Length: > 0 } timings) return;
    var line = System.Text.Json.JsonSerializer.Serialize(new
    {
        started = started.ToUnixTimeMilliseconds(),
        loaded = loaded.ToUnixTimeMilliseconds(),
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
