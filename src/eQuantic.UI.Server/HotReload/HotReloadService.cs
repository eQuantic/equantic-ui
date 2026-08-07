using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server.HotReload;

/// <summary>
/// Phase 3 hot reload (v1, DEVELOPMENT only): watches the app's C# sources, re-runs the SDK's own
/// eqc target (<c>dotnet msbuild -t:CompileEQuanticUI</c> — the exact pipeline a normal build
/// uses, nothing bespoke), and notifies connected browsers over SSE. The RUNTIME side captures the
/// live page state before reloading and replays it through the ordinary SSR-hydration mechanic —
/// save a file, the UI updates, the state survives. v2 fence: module hot-swap without a reload.
/// </summary>
public sealed class HotReloadService : IDisposable
{
    private readonly string _contentRoot;
    private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private Timer? _keepAlive;
    private int _building;

    public HotReloadService(string contentRoot) => _contentRoot = contentRoot;

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

    private void Rebuild()
    {
        if (Interlocked.Exchange(ref _building, 1) == 1) return;
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "msbuild -t:CompileEQuanticUI -v:q -nologo",
                WorkingDirectory = _contentRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null) return;
            // The pipes are read BEFORE waiting: a build that says more than the pipe buffer holds
            // (~64KB — any real error list) used to block writing while we blocked waiting, and the
            // whole thing sat there until the 2-minute timeout.
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            var started = Stopwatch.StartNew();
            process.WaitForExit(120_000);
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

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
        _keepAlive?.Dispose();
    }
}
