using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace eQuantic.UI.Server.HotReload;

/// <summary>
/// The app's eqc, kept running by its server while it streams rebuilds, and asked to compile on each
/// save (#1276).
/// <para>
/// Every save used to start a new <c>dotnet msbuild -t:CompileEQuanticUI</c>, which evaluated the
/// project and resolved its references again before it started a new eqc that loaded the compilation
/// again: 6.8s of MSBuild before a module was written, on CI's runner (#900). MSBuild decides nothing
/// a save changes, so it runs ONCE for a run of the server, to write the response file the build
/// writes and to say where eqc is, and eqc stays, reading <c>compile</c> on its standard input and
/// answering each in one line (<c>eqc --serve</c>). The project file is not among what a save changes:
/// dotnet watch restarts the app when it changes, and the next run asks MSBuild again.
/// </para>
/// <para>
/// THE SERVER OWNS THE PROCESS. eqc ends when its standard input does, so a server that stops, or is
/// killed, takes it along, and <see cref="Dispose"/> stops it at once. One that dies, or takes longer
/// than a compile may, is stopped and started again by the next save. A compile that fails answers
/// with its error lines, which the page shows, and the next save compiles again from the sources as
/// they are, so the fix after a broken edit reaches the page with no restart.
/// </para>
/// </summary>
internal sealed class RunningCompiler : IModuleCompiler
{
    /// <summary>The prefix of eqc's answer to a compile, the one line of its output this reads.</summary>
    internal const string AnswerPrefix = "@eqc ";

    /// <summary>How long the project's MSBuild may take to say how to start eqc.</summary>
    private static readonly TimeSpan DiscoveryLimit = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The variables dotnet watch gives the app it runs, which a .NET process started by the app
    /// inherits: with them, eqc would load dotnet watch's agent and contend for the app's own channel to
    /// dotnet watch, and an ASP.NET host would load its refresh middleware.
    /// </summary>
    private static readonly string[] WatchVariables =
    [
        "DOTNET_STARTUP_HOOKS", "DOTNET_MODIFIABLE_ASSEMBLIES", "DOTNET_WATCH_HOTRELOAD_NAMEDPIPE_NAME",
        "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT", "ASPNETCORE_AUTO_RELOAD_WS_KEY",
    ];

    private readonly Func<RunningCompiler, ProcessStartInfo?> _discover;
    private readonly object _gate = new();
    // Held across finding and starting eqc: the start's Prepare and the first save meet here, and the
    // second finds the first one's eqc instead of starting another nobody would stop. Dispose takes
    // only the gate, so it can still stop the MSBuild a holder of this waits on.
    private readonly object _readying = new();
    private ProcessStartInfo? _start;
    private Process? _eqc;
    private Process? _discovery;
    private (Process Eqc, TaskCompletionSource<CompileOutcome> Answer)? _pending;
    private bool _disposed;

    /// <summary>The eqc of the project in <paramref name="contentRoot"/>, found by asking MSBuild.</summary>
    public RunningCompiler(string contentRoot)
        : this(compiler => compiler.Discover(contentRoot))
    {
    }

    /// <summary>The compiler started as <paramref name="discover"/> says, which a test sets; null when
    /// it cannot be found.</summary>
    internal RunningCompiler(Func<RunningCompiler, ProcessStartInfo?> discover) => _discover = discover;

    /// <summary>The id of the eqc this started last, which a test reads; null before the first.</summary>
    internal int? ProcessId
    {
        get
        {
            lock (_gate) return _eqc?.Id;
        }
    }

    /// <summary>The eqc this started last, whose own lock a test holds as its exit event does; null before
    /// the first.</summary>
    internal Process? Running
    {
        get
        {
            lock (_gate) return _eqc;
        }
    }

    /// <summary>The id of the MSBuild being asked how to start eqc, which a test reads; null when none is.</summary>
    internal int? AskingId
    {
        get
        {
            lock (_gate) return _discovery?.Id;
        }
    }

    // Set once the project is known not to be built with eQuantic.UI.Sdk: no eqc exists for it, and MSBuild is
    // asked no more (#1757).
    private volatile bool _off;

    /// <inheritdoc/>
    public bool Available => !_off;

    public CompileOutcome Compile(TimeSpan limit)
    {
        var clock = Stopwatch.StartNew();
        if (_off) return CompileOutcome.Off;
        if (Ready() is not { } eqc)
            return _off ? CompileOutcome.Off
                : CompileOutcome.NotRun("eqc could not be started: the project's MSBuild did not say where it is");

        TaskCompletionSource<CompileOutcome> answer;
        lock (_gate)
        {
            if (_disposed) return CompileOutcome.NotRun("the app stopped");
            answer = new TaskCompletionSource<CompileOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            // The answer belongs to THIS eqc: one stopped before it may still say it exited.
            _pending = (eqc, answer);
        }
        try
        {
            eqc.StandardInput.WriteLine("compile");
            eqc.StandardInput.Flush();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Stop(eqc);
            return CompileOutcome.NotRun($"eqc stopped before it compiled: {ex.Message}");
        }

        var left = limit - clock.Elapsed;
        if (left > TimeSpan.Zero && answer.Task.Wait(left)) return answer.Task.Result;
        // Stopped, not abandoned: a compile left running writes beside the next one.
        Stop(eqc);
        return CompileOutcome.NotRun($"eqc did not answer in {limit.TotalSeconds:F0}s, and was stopped");
    }

    /// <summary>
    /// Asks MSBuild how to start eqc and starts it, so the first save of a run pays neither: the
    /// server calls this as it starts, beside the app's own start.
    /// </summary>
    public void Prepare() => Ready();

    /// <summary>The running eqc, started when there is none: the first save of a run, or the one after
    /// it died or was stopped.</summary>
    private Process? Ready()
    {
        lock (_readying) return ReadyOnce();
    }

    private Process? ReadyOnce()
    {
        Process? current;
        lock (_gate)
        {
            if (_disposed) return null;
            current = _eqc;
        }
        // Asked and released outside the gate. The process's exit event runs inside the process's own lock and
        // reads the compile in flight through the gate, and both asking whether it exited, when nothing has
        // seen the exit yet, and releasing it take that lock. Under the gate, the save waited on the event
        // and the event on the save, and the hot reload hung for good (#1491): releasing was moved out first
        // (#1496), and asking still hung a save that was the first to see eqc dead.
        if (current is not null)
        {
            if (StillRunning(current)) return current;
            lock (_gate)
            {
                // Dispose took it out and releases it itself. Otherwise it is still the one running: only
                // this, which one save at a time runs, and Dispose change it.
                if (_disposed) return null;
                _eqc = null;
            }
            current.Dispose();
        }

        // Found outside the gate, since asking MSBuild takes seconds and Dispose must be able to stop it.
        if (_off) return null;
        var start = _start ??= _discover(this);
        if (start is null) return null;

        lock (_gate)
        {
            if (_disposed) return null;
            var eqc = new Process { StartInfo = start, EnableRaisingEvents = true };
            eqc.OutputDataReceived += (_, line) => Heard(eqc, line.Data);
            // Read so its pipe never fills: the errors also arrive in the answer.
            eqc.ErrorDataReceived += (_, _) => { };
            // Raised inside the process's own lock, and it takes the gate: so no member of a process is
            // ever called while the gate is held, or the two wait on each other (#1491).
            eqc.Exited += (_, _) => AnswerOf(eqc)?.TrySetResult(CompileOutcome.NotRun("eqc exited before it answered"));
            if (!eqc.Start()) return null;
            eqc.BeginOutputReadLine();
            eqc.BeginErrorReadLine();
            _eqc = eqc;
            return eqc;
        }
    }

    /// <summary>Whether <paramref name="eqc"/> is still running; false once a Dispose that ran beside the
    /// asking has released it, which Windows can answer as an invalid handle (#1375).</summary>
    private static bool StillRunning(Process eqc)
    {
        try
        {
            return !eqc.HasExited;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    /// <summary>The answer the compile in flight on <paramref name="eqc"/> waits for, if one does.</summary>
    private TaskCompletionSource<CompileOutcome>? AnswerOf(Process eqc)
    {
        lock (_gate) return _pending is { } pending && ReferenceEquals(pending.Eqc, eqc) ? pending.Answer : null;
    }

    /// <summary>A line eqc wrote: its answer to the compile in flight, or something to pass over.</summary>
    private void Heard(Process eqc, string? line)
    {
        if (line is null || !line.StartsWith(AnswerPrefix, StringComparison.Ordinal)) return;
        try
        {
            using var answer = JsonDocument.Parse(line[AnswerPrefix.Length..]);
            var compiled = answer.RootElement.GetProperty("compiled").GetBoolean();
            var errors = answer.RootElement.GetProperty("errors").EnumerateArray()
                .Select(error => error.GetString() ?? string.Empty).ToArray();
            AnswerOf(eqc)?.TrySetResult(new CompileOutcome(compiled, errors));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            AnswerOf(eqc)?.TrySetResult(CompileOutcome.NotRun($"eqc answered in a form this server does not read: {line}"));
        }
    }

    /// <summary>
    /// How this project's eqc is started, from its own MSBuild: the target that writes eqc's response
    /// file, and the properties that say where eqc and that file are. The same evaluation the build ran,
    /// so the arguments are the build's, configuration and references included.
    /// </summary>
    private ProcessStartInfo? Discover(string contentRoot) => Asked(
        Started("dotnet", contentRoot,
            ["msbuild", "-t:_EQuanticCompilerArguments", "-getProperty:EqcCliPath",
             "-getProperty:_EqcArgumentsPath", "-getProperty:MSBuildProjectDirectory", "-nologo", "-v:q"]),
        contentRoot, DiscoveryLimit, () => BuiltWithTheSdk(contentRoot));

    /// <summary>What <paramref name="info"/>, an MSBuild asked how to start eqc, answered within
    /// <paramref name="limit"/>, which a test sets; null when it did not say. When it refuses, <paramref name="isApp"/>,
    /// when given, says whether the project is built with eQuantic.UI.Sdk at all.</summary>
    internal ProcessStartInfo? Asked(ProcessStartInfo info, string contentRoot, TimeSpan limit, Func<bool>? isApp = null)
    {
        if (Run(info, limit, "say how to start eqc") is not { } answer) return null;
        try
        {
            if (answer.ExitCode != 0)
            {
                // A host that serves eQuantic.UI pages from a library it references, without being an
                // eQuantic.UI app itself, has no eqc and none of the SDK's targets: MSBuild's MSB4057 and
                // "Build failed" opened its log, and every save asked again (#1757). Said once, in these words.
                if (isApp is not null && !isApp())
                {
                    _off = true;
                    Console.WriteLine("[eQuantic.HotReload] hot reload is off for this host: its project is not built with "
                        + "eQuantic.UI.Sdk, so there is no eqc to rebuild the pages it serves");
                    return null;
                }
                Console.WriteLine($"[eQuantic.HotReload] MSBuild could not say how to start eqc:\n{answer.Output}{answer.Errors}");
                return null;
            }
            var json = answer.Output;
            var properties = JsonDocument.Parse(json[json.IndexOf('{')..]).RootElement.GetProperty("Properties");
            var eqc = properties.GetProperty("EqcCliPath").GetString();
            var arguments = properties.GetProperty("_EqcArgumentsPath").GetString();
            var project = properties.GetProperty("MSBuildProjectDirectory").GetString() ?? contentRoot;
            if (string.IsNullOrEmpty(eqc) || string.IsNullOrEmpty(arguments)) return null;
            var file = Path.GetFullPath(arguments, project);
            var start = Started("dotnet", project, [eqc, "@" + file, "--serve"]);
            start.RedirectStandardInput = true;
            return start;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            Console.WriteLine($"[eQuantic.HotReload] MSBuild's answer did not say how to start eqc: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Whether the project in <paramref name="contentRoot"/> is built with eQuantic.UI.Sdk: an evaluation, no
    /// target, reading the eqc the SDK names for every project it builds. Asked only when MSBuild refused the
    /// SDK's target, so an app pays nothing for it. An answer that does not come is read as yes, and the
    /// refusal is reported as MSBuild gave it.
    /// </summary>
    private bool BuiltWithTheSdk(string contentRoot)
    {
        // Two properties, so MSBuild answers in JSON and the property is read itself: one alone is printed bare,
        // where a warning on the same stream would read as a path.
        if (Run(Started("dotnet", contentRoot,
                ["msbuild", "-getProperty:EqcCliPath", "-getProperty:MSBuildProjectDirectory", "-nologo", "-v:q"]),
                DiscoveryLimit, "say whether this project is built with eQuantic.UI.Sdk") is not { ExitCode: 0 } answer)
            return true;
        try
        {
            var json = answer.Output;
            var properties = JsonDocument.Parse(json[json.IndexOf('{')..]).RootElement.GetProperty("Properties");
            return !string.IsNullOrEmpty(properties.GetProperty("EqcCliPath").GetString());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return true;
        }
    }

    /// <summary>What an MSBuild said, and how it ended.</summary>
    private sealed record MsBuildAnswer(int ExitCode, string Output, string Errors);

    /// <summary>
    /// Runs <paramref name="info"/>, an MSBuild, within <paramref name="limit"/>, where Dispose can stop it; null
    /// when it did not answer, its output stayed held, or the app stopped, each said in its own words.
    /// </summary>
    private MsBuildAnswer? Run(ProcessStartInfo info, TimeSpan limit, string asked)
    {
        using var msbuild = new Process { StartInfo = info };
        lock (_gate)
        {
            if (_disposed) return null;
            msbuild.Start();
            _discovery = msbuild;
        }
        try
        {
            var output = msbuild.StandardOutput.ReadToEndAsync();
            var errors = msbuild.StandardError.ReadToEndAsync();
            var clock = Stopwatch.StartNew();
            if (!msbuild.WaitForExit(limit))
            {
                Stop(msbuild);
                Console.WriteLine($"[eQuantic.HotReload] MSBuild did not {asked} in {limit.TotalMinutes:F0} minutes, and was stopped");
                return null;
            }
            // What it wrote, within what is left of its time: a stream ends when the last process holding
            // it closes it, not when MSBuild exits, and an unbounded read held the save that asked, and
            // every save after it (#997).
            var left = limit - clock.Elapsed;
            if (!Task.WhenAll(output, errors).Wait(left > TimeSpan.Zero ? left : TimeSpan.Zero))
            {
                Console.WriteLine("[eQuantic.HotReload] MSBuild exited, and something it started still holds its output");
                return null;
            }
            // A stop kills it: that is no failure to report.
            lock (_gate)
            {
                if (_disposed) return null;
            }
            return new MsBuildAnswer(msbuild.ExitCode, output.Result, errors.Result);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"[eQuantic.HotReload] MSBuild could not {asked}: {ex.Message}");
            return null;
        }
        finally
        {
            // Taken back under the gate before the using releases it: a Dispose stopping it holds the gate
            // until it has stopped.
            lock (_gate) _discovery = null;
        }
    }

    /// <summary>A program started with a list of arguments and no shell, without dotnet watch's
    /// variables and with no build server to outlive it, its output read by this server.</summary>
    internal static ProcessStartInfo Started(string program, string directory, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(program)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        foreach (var variable in WatchVariables) info.Environment.Remove(variable);
        // Nothing left running behind it: a worker node, an MSBuild server or a compiler server that
        // outlived the MSBuild would keep its streams open, which is how the Web suite hung until #971
        // measured it, and here would hold the save that asked (#997).
        info.Environment.Remove("MSBUILDENSURESTDOUTFORTASKPROCESSES");
        info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        info.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        info.Environment["UseSharedCompilation"] = "false";
        return info;
    }

    /// <summary>Stops eqc and any MSBuild still saying where it is. A compile in flight answers that the
    /// app stopped.</summary>
    public void Dispose()
    {
        Process? eqc;
        lock (_gate)
        {
            _disposed = true;
            eqc = _eqc;
            _eqc = null;
            // Stopped under the gate: the MSBuild belongs to the asking that started it, which releases it
            // only after taking it back under this gate, so it cannot be released while this waits for it.
            // Read here and stopped outside, it was released between the look and the wait, which Windows
            // refuses with an invalid handle, and the host's disposal threw it (#1375). Nothing it waits
            // on takes the gate: its output is read whole by the asking, and it raises no exit event.
            if (_discovery is { } discovery) Stop(discovery);
        }
        if (eqc is not null) AnswerOf(eqc)?.TrySetResult(CompileOutcome.NotRun("the app stopped"));
        if (eqc is not null)
        {
            Stop(eqc);
            eqc.Dispose();
        }
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
        }
        catch (InvalidOperationException)
        {
            // It exited, or was released, between the look and the kill.
        }
    }
}
