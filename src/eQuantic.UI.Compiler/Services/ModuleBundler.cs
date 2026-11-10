using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// The step after eqc writes its TypeScript: bun bundles the entry modules into the JavaScript the
/// browser loads, and each module's map is composed with the map its TypeScript carries, so a
/// debugger lands on the C#. One owner, so the build and the test that proves a thrown error's frame
/// leads back to its C# line (#293) run the same bundling, flags and all.
/// <para>
/// ONE BUN A BUNDLER, KEPT BETWEEN BUNDLES (#1280). The bundler starts a bun process the first time it
/// bundles and hands it every later bundle, so a served eqc (#1276) pays for starting bun once rather
/// than on every save, which cost more than bun's own work: measured on the dashboard sample, bun
/// bundled its 22 entry points in 30 to 44 ms inside a process it kept, and took 117 to 215 ms when
/// started anew from eqc at the same load. The process writes a module, a chunk or a map only when its
/// text changed, and a map is composed again only when bun's text for it or a map it composes with
/// changed. Every entry point is bundled every time, with <c>--splitting</c>: a page bundled alone
/// would carry its own copy of a module it shares, and two copies of a module are two of its state.
/// </para>
/// </summary>
public sealed class ModuleBundler : IDisposable
{
    private const string AnswerPrefix = "@bundle ";

    private readonly string _bunPath;
    private readonly string _workingDirectory;

    /// <summary>What each map was composed from the last time it was composed: the map bun wrote, and
    /// each map of a source it composes with, by its length and the time it was written.</summary>
    private readonly Dictionary<string, string> _composedFrom = new(StringComparer.Ordinal);

    private Process? _worker;
    private Task? _drainErrors;

    /// <summary>A bundler that runs <paramref name="bunPath"/> in <paramref name="workingDirectory"/>.</summary>
    /// <remarks>bun names each module by its path from the folder it runs in, and mixes those names into
    /// the hash a shared chunk is named by. So that folder is named here and never inherited: a bundle is
    /// the same bytes wherever its caller runs, and the SDK runs eqc in the folder of its response file,
    /// not the project's (#836).</remarks>
    public ModuleBundler(string bunPath, string workingDirectory)
    {
        _bunPath = bunPath;
        _workingDirectory = workingDirectory;
    }

    /// <summary>
    /// Bundles <paramref name="entryPoints"/>, TypeScript files under <paramref name="intermediateDir"/>,
    /// into <paramref name="outputDir"/>, and composes the maps <paramref name="sourceMaps"/> asks for.
    /// Answers null, or bun's error output when it fails. A map that cannot be composed stays as bun
    /// wrote it, and <paramref name="warn"/> hears why.
    /// </summary>
    public string? Bundle(IReadOnlyCollection<string> entryPoints, string outputDir, string intermediateDir,
        SourceMapMode sourceMaps, Action<string>? warn = null)
    {
        // The root pins the output-path base to the intermediate TS dir (where every entry .ts lives),
        // so bun writes entries FLAT as "<Page>.js" in outDir. Without it bun infers the root from the
        // common ancestor of the absolute entry paths (the repo/cwd) and nests entries under that
        // relative path (e.g. wwwroot/_equantic/samples/.../ts/Dashboard.js), which the boot — loading
        // the flat "/_equantic/<Page>.js" — then 404s on.
        var request = new JsonObject
        {
            ["entrypoints"] = new JsonArray(entryPoints.Select(entry => (JsonNode)Path.GetFullPath(entry)).ToArray()),
            ["root"] = Path.GetFullPath(intermediateDir),
            ["outdir"] = Path.GetFullPath(outputDir),
            ["sourcemap"] = sourceMaps switch
            {
                SourceMapMode.Full => "linked",
                SourceMapMode.External => "external",
                _ => "none",
            },
        };

        JsonObject answer;
        try
        {
            answer = Ask(request.ToJsonString());
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException)
        {
            // A worker that died answers nothing: the next bundle starts another, and this one says why.
            Stop();
            return $"bun's bundle worker stopped: {ex.Message}";
        }

        if (answer["ok"]?.GetValue<bool>() != true) return answer["error"]?.GetValue<string>() ?? "bun failed";

        // A module, a chunk or a map is this bundle's when bun produced it, written or left as it was.
        // Any other is removed now, after bun has written, never before, so the folder holds every
        // module while a compile runs: a removed or renamed component, a shared chunk bun renamed, the
        // maps a Release build does not write (the output folder is shared by every configuration, so a
        // Debug build's maps would ride out with a Release publish). This is the folder's one prune, and
        // it goes by what the bundle produced, never by a file's time, since an unchanged file keeps its
        // own (#1279).
        var produced = new HashSet<string>(StringComparer.Ordinal);
        var maps = new List<(string Path, bool Written, string[] Sources)>();
        foreach (var output in answer["outputs"]!.AsArray())
        {
            var path = Path.GetFullPath(output!["path"]!.GetValue<string>());
            produced.Add(path);
            if (!path.EndsWith(".map", StringComparison.Ordinal)) continue;
            var sources = output["sources"]?.AsArray().Select(source => source!.GetValue<string>()).ToArray() ?? [];
            maps.Add((path, output["written"]!.GetValue<bool>(), sources));
        }
        foreach (var file in Outputs(outputDir))
            if (!produced.Contains(Path.GetFullPath(file))) File.Delete(file);
        foreach (var path in _composedFrom.Keys.Where(path => !produced.Contains(path)).ToList())
            _composedFrom.Remove(path);

        // A module's map leads to the TypeScript eqc wrote (bun), and that TypeScript's map to the C#
        // (eqc): composed here, so a debugger lands on the C#. This was a script over an npm package that
        // bun's auto-install fetched on a developer's first Debug build (#356). A map bun left as it was
        // is composed again only when a map it composes with changed: the TypeScript of a C# file whose
        // lines moved can be the same text with another map.
        if (sourceMaps != SourceMapMode.None)
        {
            foreach (var (mapFile, written, sources) in maps)
            {
                var from = Fingerprint(mapFile, sources);
                if (!written && _composedFrom.TryGetValue(mapFile, out var last) && last == from) continue;
                try
                {
                    SourceMapComposer.ComposeFile(mapFile);
                    _composedFrom[mapFile] = from;
                }
                catch (Exception ex) when (ex is FormatException or JsonException or IOException)
                {
                    // A map is a debugging aid, so one that cannot be composed stays as bun wrote it,
                    // leading to the TypeScript, and the build says so.
                    _composedFrom.Remove(mapFile);
                    warn?.Invoke($"⚠️ {Path.GetFileName(mapFile)} leads to the TypeScript only: {ex.Message}");
                }
            }
        }
        return null;
    }

    /// <summary>Stops bun.</summary>
    public void Dispose() => Stop();

    /// <summary>Hands <paramref name="request"/> to bun, starting it first if none is running, and reads
    /// its answer: the one line that starts with <see cref="AnswerPrefix"/>.</summary>
    private JsonObject Ask(string request)
    {
        var worker = _worker is { HasExited: false } running ? running : Start();
        worker.StandardInput.WriteLine(request);
        worker.StandardInput.Flush();
        while (worker.StandardOutput.ReadLine() is { } line)
        {
            if (line.StartsWith(AnswerPrefix, StringComparison.Ordinal))
                return JsonNode.Parse(line[AnswerPrefix.Length..])!.AsObject();
        }
        throw new IOException($"bun exited with code {(worker.WaitForExit(1000) ? worker.ExitCode : -1)} before it answered");
    }

    private Process Start()
    {
        Stop();
        // Each argument is handed whole, as a list: a command line of quoted paths lost every double
        // quote a folder's name held.
        var startInfo = new ProcessStartInfo
        {
            FileName = _bunPath,
            WorkingDirectory = _workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add(WorkerScript.Value);
        var worker = Process.Start(startInfo) ?? throw new InvalidOperationException("bun did not start");
        // Standard error drains on its own, so a warning bun prints never fills its pipe and stalls it.
        _drainErrors = worker.StandardError.ReadToEndAsync();
        _worker = worker;
        return worker;
    }

    private void Stop()
    {
        if (_worker is not { } worker) return;
        _worker = null;
        _composedFrom.Clear();
        try
        {
            // The end of its input is the worker's end, as it is a served eqc's.
            if (!worker.HasExited) worker.StandardInput.Close();
            if (!worker.WaitForExit(2000)) worker.Kill(entireProcessTree: true);
            _drainErrors?.Wait(1000);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            // Already gone.
        }
        finally
        {
            worker.Dispose();
        }
    }

    /// <summary>What <paramref name="mapFile"/> is composed from: bun's map and each map its sources
    /// carry beside them, by length and time, since eqc writes a TypeScript's map only when its text
    /// changed (#1279).</summary>
    private static string Fingerprint(string mapFile, string[] sources)
    {
        var directory = Path.GetDirectoryName(mapFile)!;
        return string.Join("|", sources.Prepend(Path.GetFileName(mapFile)).Select((source, index) =>
        {
            var file = index == 0 ? mapFile : Path.GetFullPath(Path.Combine(directory, source)) + ".map";
            var info = new FileInfo(file);
            return info.Exists ? $"{file}:{info.Length}:{info.LastWriteTimeUtc.Ticks}" : $"{file}:-";
        }));
    }

    /// <summary>
    /// Every module, chunk and map under <paramref name="outputDir"/>: what bun writes, and the runtime
    /// aside, which the SDK copies there and is not the bundle's.
    /// </summary>
    private static IEnumerable<string> Outputs(string outputDir) =>
        Directory.Exists(outputDir)
            ? Directory.GetFiles(outputDir, "*.js", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(outputDir, "*.js.map", SearchOption.AllDirectories))
                .Where(file => !Path.GetFileName(file).StartsWith("runtime.js", StringComparison.Ordinal))
            : [];

    /// <summary>The worker's JavaScript, which eqc carries as a resource and hands bun on its command line,
    /// so nothing is written to run it.</summary>
    private static readonly Lazy<string> WorkerScript = new(() =>
    {
        using var stream = typeof(ModuleBundler).Assembly.GetManifestResourceStream("eQuantic.UI.Compiler.BundleWorker.js")
                           ?? throw new InvalidOperationException("eqc carries no bundle worker");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
}
