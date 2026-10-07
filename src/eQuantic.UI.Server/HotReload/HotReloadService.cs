using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server.HotReload;

/// <summary>
/// Phase 3 hot reload (v1, in Development and under dotnet watch): watches the app's C# sources, re-runs the SDK's own
/// eqc target (<c>dotnet msbuild -t:CompileEQuanticUI</c> — the exact pipeline a normal build
/// uses, nothing bespoke), and notifies connected browsers over SSE. The RUNTIME side captures the
/// live page state before reloading and replays it through the ordinary SSR-hydration mechanic —
/// save a file, the UI updates, the state survives. v2 fence: module hot-swap without a reload.
/// </summary>
public sealed class HotReloadService : IDisposable
{
    private readonly string _contentRoot;
    private readonly Func<Process?> _startRebuild;
    private readonly TimeSpan _limit;
    private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();
    private readonly object _gate = new();
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private Timer? _keepAlive;
    private int _building;
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
        }), TimeSpan.FromMinutes(2))
    {
    }

    /// <summary>The service with the rebuild it starts and how long it may take, which a test sets.</summary>
    internal HotReloadService(string contentRoot, Func<Process?> startRebuild, TimeSpan limit)
    {
        _contentRoot = contentRoot;
        _startRebuild = startRebuild;
        _limit = limit;
    }

    public void Start()
    {
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
        // Debounce editor save bursts; the build itself serializes via _building.
        _debounce?.Dispose();
        _debounce = new Timer(_ => Rebuild(), null, 400, Timeout.Infinite);
    }

    internal void Rebuild()
    {
        if (_disposed || Interlocked.Exchange(ref _building, 1) == 1) return;
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
                Broadcast("reload");
            }
            else Console.WriteLine($"[eQuantic.HotReload] eqc rebuild failed:\n{stderr.Result}{stdout.Result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eQuantic.HotReload] rebuild error: {ex.Message}");
        }
        finally
        {
            _rebuild = null;
            Interlocked.Exchange(ref _building, 0);
        }
    }

    private void Broadcast(string message) => Send($"data: {message}\n\n");

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
        await context.Response.WriteAsync(": connected\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);

        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<string>();
        _clients[id] = channel;
        // Registered after Dispose completed every channel it found: this one ends here, or it holds
        // the shutdown until the host gives up on it.
        if (_disposed) channel.Writer.TryComplete();
        try
        {
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
