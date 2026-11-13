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
/// started anew from eqc at the same load. The process writes a module, a chunk or a map only when it
/// changed (a map when bun's text for it or a map it composes with did), and only what it wrote is
/// composed. Every entry point is bundled every time, with <c>--splitting</c>: a page bundled alone
/// would carry its own copy of a module it shares, and two copies of a module are two of its state.
/// </para>
/// </summary>
public sealed class ModuleBundler : IDisposable
{
    private const string AnswerPrefix = "@bundle ";

    /// <summary>How long a bundle may take before the worker is taken for stuck: a minute is several
    /// hundred times what the dashboard sample's bundle takes at a load of 200.</summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(1);

    private readonly string _bunPath;
    private readonly string _workingDirectory;

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
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException
                                       or TimeoutException or System.ComponentModel.Win32Exception)
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
        var written = new List<string>();
        foreach (var output in answer["outputs"]!.AsArray())
        {
            var path = Path.GetFullPath(output!["path"]!.GetValue<string>());
            produced.Add(path);
            if (path.EndsWith(".map", StringComparison.Ordinal) && output["written"]!.GetValue<bool>()) written.Add(path);
        }
        foreach (var file in Outputs(outputDir))
            if (!produced.Contains(Path.GetFullPath(file))) File.Delete(file);

        // A module's map leads to the TypeScript eqc wrote (bun), and that TypeScript's map to the C#
        // (eqc): composed here, so a debugger lands on the C#. This was a script over an npm package that
        // bun's auto-install fetched on a developer's first Debug build (#356). Only the maps bun wrote
        // are composed: one it left as it was holds its composition already, and bun writes a map again
        // when a map it composes with changed, since the TypeScript of a C# file whose lines moved can be
        // the same text with another map.
        if (sourceMaps != SourceMapMode.None)
        {
            foreach (var mapFile in written)
            {
                try
                {
                    SourceMapComposer.ComposeFile(mapFile);
                }
                catch (Exception ex) when (ex is FormatException or JsonException or IOException)
                {
                    // A map is a debugging aid, so one that cannot be composed stays as bun wrote it,
                    // leading to the TypeScript, and the build says so.
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
        // Within a deadline: a bun that hangs would hold a served eqc, and every save after it, forever.
        var deadline = DateTime.UtcNow + Deadline;
        while (true)
        {
            var reading = worker.StandardOutput.ReadLineAsync();
            var left = deadline - DateTime.UtcNow;
            bool answered;
            try
            {
                answered = left > TimeSpan.Zero && reading.Wait(left);
            }
            catch (AggregateException ex) when (ex.InnerException is { } failure)
            {
                // A read that failed (a pipe the worker broke as it died) is that failure, which the
                // bundle reports as the worker stopping, not the wrapper a wait puts around it.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(failure);
                throw;
            }
            if (!answered) throw new TimeoutException($"bun did not answer within {Deadline.TotalSeconds:0}s");
            if (reading.Result is not { } line) break;
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
        // Standard error drains on its own, so a warning bun prints never fills its pipe and stalls it,
        // and is thrown away as it comes: a worker kept for a served eqc's life would otherwise hold every
        // line it ever printed. A bundle's errors reach eqc in its answer, never on this stream.
        _drainErrors = Discard(worker.StandardError);
        _worker = worker;
        return worker;
    }

    /// <summary>Reads <paramref name="stream"/> to its end and keeps nothing.</summary>
    private static async Task Discard(StreamReader stream)
    {
        var buffer = new char[4096];
        try
        {
            while (await stream.ReadAsync(buffer.AsMemory()).ConfigureAwait(false) > 0)
            {
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // The worker is gone, and so is what it had to say.
        }
    }

    private void Stop()
    {
        if (_worker is not { } worker) return;
        _worker = null;
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
