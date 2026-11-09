using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server.HotReload;

/// <summary>
/// Phase 3 hot reload (v1, in Development and under dotnet watch): watches the app's C# sources, re-runs the SDK's own
/// eqc target (<c>dotnet msbuild -t:CompileEQuanticUI</c> — the exact pipeline a normal build
/// uses, nothing bespoke), and notifies connected browsers over SSE. The RUNTIME side captures the
/// live page state before reloading and replays it through the ordinary SSR-hydration mechanic —
/// save a file, the UI updates, the state survives. v2 fence: module hot-swap without a reload.
/// <para>
/// THE STREAM IS THE PAGE'S ONE SOURCE OF RELOADS, and says three things (#1275). <c>server</c>, first
/// on every connection, names this run of the server, so a page that reconnects to another one (a
/// restart, dotnet watch's answer to an edit it cannot apply) reloads. <c>rebuilt</c> says the modules
/// are new and whether the server's markup holds the edit too (<see cref="ServerEdits"/>): <c>hydrate</c>,
/// or <c>mount</c> when the page has to draw over markup from before it. <c>failed</c> carries the
/// errors of a rebuild that did not compile. dotnet watch's own refresh script, which reloaded each page
/// a second time with the old modules, stays out of the page while this stream runs: the shell tells it
/// the page is taken.
/// </para>
/// </summary>
public sealed class HotReloadService : IDisposable
{
    /// <summary>The most error lines a failed rebuild sends the page, which shows them all.</summary>
    internal const int ErrorsShown = 20;

    private readonly string _contentRoot;
    private readonly Func<Process?> _startRebuild;
    private readonly TimeSpan _limit;
    private readonly ServerEdits _edits;
    private readonly string _run = Guid.NewGuid().ToString("N");
    private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();
    private readonly object _gate = new();
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private Timer? _keepAlive;
    private int _building;
    private int _pending;
    private Burst? _burst;
    private Process? _rebuild;
    private volatile bool _disposed;

    public HotReloadService(string contentRoot)
        : this(contentRoot, () => Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "msbuild -t:CompileEQuanticUI -v:q -nologo",
            WorkingDirectory = contentRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }), TimeSpan.FromMinutes(2), ServerEdits.OfThisProcess)
    {
    }

    /// <summary>The service with the rebuild it starts, how long it may take and the edits its server
    /// takes, which a test sets.</summary>
    internal HotReloadService(string contentRoot, Func<Process?> startRebuild, TimeSpan limit, ServerEdits edits)
    {
        _contentRoot = contentRoot;
        _startRebuild = startRebuild;
        _limit = limit;
        _edits = edits;
    }

    /// <summary>
    /// The saves one rebuild answers, from the first: how many edits the server had taken then, and
    /// when. The edit of that save reaches the server after it, so an edit counted past that number is
    /// one the rebuild's reload will find in the server's markup.
    /// </summary>
    private sealed record Burst(long EditsSeen, long At);

    /// <summary>
    /// How long the sources stay quiet before a rebuild starts: long enough to take an editor's save,
    /// which can be several writes and a rename, as one rebuild, and short against the second an edit
    /// has to reach the screen in. It was a fixed 400ms, measured as 44% of that bar (#900, #1274).
    /// </summary>
    internal static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(50);

    public void Start()
    {
        // One timer for the service's life, restarted by each event: the watcher raises them on pool
        // threads, and replacing the timer from two of them at once left the first one running.
        _debounce = new Timer(_ => Rebuild(), null, Timeout.Infinite, Timeout.Infinite);
        _watcher = new FileSystemWatcher(_contentRoot, "*.cs")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
        };
        _watcher.Changed += OnSourceChanged;
        _watcher.Created += OnSourceChanged;
        _watcher.Renamed += OnSourceChanged;
        _watcher.EnableRaisingEvents = true;

        // A parked SSE request that never says anything gets idle-dropped by Kestrel and by every
        // proxy in between — a couple of quiet minutes and hot reload was silently over for that
        // tab. A comment frame every 20s is invisible to the client and keeps the pipe warm.
        _keepAlive = new Timer(_ => Send(": ping\n\n"), null, 20_000, 20_000);
    }

    private void OnSourceChanged(object sender, FileSystemEventArgs e)
    {
        var path = e.FullPath.Replace('\\', '/');
        if (path.Contains("/obj/") || path.Contains("/bin/")) return;
        // Each event of a save restarts the wait, so a burst of writes is one rebuild.
        if (_disposed) return;
        // The burst's first event, taken before the edit of its save can reach the server.
        if (Volatile.Read(ref _burst) is null)
            Interlocked.CompareExchange(ref _burst, new Burst(_edits.Taken, Stopwatch.GetTimestamp()), null);
        try
        {
            _debounce?.Change(Quiet, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // An event still in flight when the app stopped: there is nothing left to rebuild for.
        }
    }

    /// <summary>
    /// Rebuilds, and once more when a save came while it ran: once, however many came, since a rebuild
    /// compiles the whole project. A save that found a rebuild running used to be dropped, and the
    /// browser stayed on the edit before it until the next save (#1262).
    /// </summary>
    internal void Rebuild()
    {
        // Said before the flag is taken, so a rebuild already running sees it when it ends.
        Volatile.Write(ref _pending, 1);
        while (!_disposed && Interlocked.Exchange(ref _building, 1) == 0)
        {
            try
            {
                while (!_disposed && Interlocked.Exchange(ref _pending, 0) == 1)
                    RebuildOnce();
            }
            finally
            {
                Interlocked.Exchange(ref _building, 0);
            }
            // A save between the last look and the release found the flag held and left: its rebuild
            // is this loop's to run.
            if (Volatile.Read(ref _pending) == 0) return;
        }
    }

    private void RebuildOnce()
    {
        // The saves this rebuild answers; one after this line opens the next burst, rebuilt after it.
        var burst = Interlocked.Exchange(ref _burst, null) ?? new Burst(_edits.Taken, Stopwatch.GetTimestamp());
        try
        {
            // Started under the gate Dispose takes: a shutdown that comes while the process is starting
            // waits for it to exist, and then finds it to stop.
            Process? launched;
            lock (_gate)
            {
                if (_disposed) return;
                launched = _startRebuild();
                _rebuild = launched;
            }
            using var process = launched;
            if (process is null) return;
            // Said when it starts as well as when it ends: the person sees the save was taken, and the
            // edit-to-view measurement (#900) reads the wait after a save off this line.
            Console.WriteLine("[eQuantic.HotReload] a source changed, rebuilding");
            // The pipes are read BEFORE waiting: a build that says more than the pipe buffer holds
            // (~64KB — any real error list) used to block writing while we blocked waiting, and the
            // whole thing sat there until the 2-minute timeout.
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            var started = Stopwatch.StartNew();
            if (!process.WaitForExit(_limit))
            {
                // Stopped, not abandoned: releasing the wrapper ends nothing, and a build left running
                // writes beside the next one, where shutdown can no longer reach it.
                Stop(process);
                process.WaitForExit(5_000);
                Console.WriteLine($"[eQuantic.HotReload] eqc rebuild stopped after {_limit.TotalSeconds:F0}s");
                return;
            }
            if (_disposed) return;
            if (process.ExitCode == 0)
            {
                Console.WriteLine($"[eQuantic.HotReload] rebuilt in {started.Elapsed.TotalSeconds:F1}s — reloading browsers");
                // Announced once the server holds the edit, when every save brings it one, so the
                // page reloads ONCE and hydrates markup that agrees with its modules.
                var held = _edits.ArrivedAfter(burst.EditsSeen, burst.At);
                if (_disposed) return;
                Send(Frame("rebuilt", held ? "hydrate" : "mount"));
            }
            else
            {
                var output = stdout.Result + stderr.Result;
                Console.WriteLine($"[eQuantic.HotReload] eqc rebuild failed:\n{output}");
                Send(Frame("failed", JsonSerializer.Serialize(ErrorsOf(output))));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eQuantic.HotReload] rebuild error: {ex.Message}");
        }
        finally
        {
            _rebuild = null;
        }
    }

    /// <summary>One event of the stream: its name and a line of data.</summary>
    private static string Frame(string name, string data) => $"event: {name}\ndata: {data}\n\n";

    /// <summary>
    /// What the page shows of a rebuild that failed: MSBuild's error lines, each once, or the end of
    /// what it said when it wrote none in that form.
    /// </summary>
    internal static string[] ErrorsOf(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var errors = lines.Where(line => line.Contains(": error ", StringComparison.Ordinal)).Distinct().ToArray();
        return (errors.Length > 0 ? errors : lines.TakeLast(ErrorsShown)).Take(ErrorsShown).ToArray();
    }

    private void Send(string frame)
    {
        // Kestrel forbids synchronous response writes from arbitrary threads — hand each parked
        // client its frame through a channel; the request's own async loop does the writing.
        foreach (var (_, channel) in _clients)
            channel.Writer.TryWrite(frame);
    }

    /// <summary>The SSE endpoint body: parks the request, writing each broadcast ASYNCHRONOUSLY
    /// on its own loop until the client disconnects.</summary>
    public async Task HandleClient(HttpContext context)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        // Registered BEFORE the first frame is written: a rebuild that ends in between queues its
        // event behind the greeting instead of reaching a page that was not yet listed.
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<string>();
        _clients[id] = channel;
        // Registered after Dispose completed every channel it found: this one ends here, or it holds
        // the shutdown until the host gives up on it.
        if (_disposed) channel.Writer.TryComplete();
        try
        {
            // Which run of the server this is, and a second between attempts to reach the next one: a
            // restart is seconds of build, and the page reloads as soon as the new run answers.
            await context.Response.WriteAsync("retry: 1000\n" + Frame("server", _run), context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await foreach (var frame in channel.Reader.ReadAllAsync(context.RequestAborted))
            {
                await context.Response.WriteAsync(frame, context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            // client went away
        }
        finally
        {
            _clients.TryRemove(id, out _);
        }
    }

    /// <summary>Stops watching, and stops a rebuild still running: it belongs to this app, and the
    /// build that follows a restart writes the same files. It returns once that rebuild has stopped,
    /// since the host awaits this and nothing after it, and a rebuild still starting is waited for and
    /// stopped too. Every parked stream ends as well, or each open tab holds the app's graceful
    /// shutdown until the host gives up on it.</summary>
    public void Dispose()
    {
        Process? rebuild;
        lock (_gate)
        {
            _disposed = true;
            rebuild = _rebuild;
        }
        _watcher?.Dispose();
        _debounce?.Dispose();
        _keepAlive?.Dispose();
        if (rebuild is not null) Stop(rebuild);
        // The rebuild ends once its process exits, which the kill makes quick.
        SpinWait.SpinUntil(() => Volatile.Read(ref _building) == 0, TimeSpan.FromSeconds(10));
        foreach (var (_, channel) in _clients)
            channel.Writer.TryComplete();
    }

    private static void Stop(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It exited, or was released, between the read and the kill.
        }
    }
}
