// The time an edit takes from the save to the screen, on the dashboard sample and on the Photon
// desktop sample (#900).
//
// The roadmap's bar for production-ready is an edit on screen in under a second. This measures it
// the way a developer meets it: the sample runs under `dotnet watch`, a component's `.cs` file is
// changed on disk, and the clock stops when the change is DRAWN, by a browser on the web and by the
// window on the desktop. The edit is repeated: the first one after the app starts is the cold one,
// the others are warm. Each part of the path is timed on its own, from what the parts report:
//
//   web      wait    the save to the server starting its rebuild (it says so on its console)
//            msbuild the rebuild, less the eqc process inside it
//            eqc     eqc's process, less bun (eqc writes both to EQC_TIMINGS)
//            bun     the bundle, timed by eqc
//            replay  the document the rebuild reloaded, from its start to the frame that draws the text
//            early   apart from the sum: the first frame that showed the text, from dotnet watch's own
//                    refresh of the page with the server patched and the old modules still running
//   desktop  delta   the save to the runtime applying the edit (dotnet watch compiles and applies it)
//            frame   the edit applied, to the frame that draws it (Photon says so on its console)
//
// A budget file, when given, holds the warm median of the parts it names, in milliseconds, and the
// run fails over any of them. A target that meets the bar is held to the bar. One that does not is
// held to a CEILING, the number it measures on CI with that runner's spread, which only ever comes
// down: the run says when a median falls under half its ceiling, and the work that made it faster
// lowers it, until it is the bar. The bar itself is reported on every run.
//
// usage: dotnet run scripts/measure-edit-to-view.cs -- web|desktop [--runs <n>] [--report <file>] [--budget <file>]
//   web needs Google Chrome (EQ_CHROME names another one), desktop a session that can open a window.
#:property PublishAot=false
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

const double BarMs = 1000;

var target = args.FirstOrDefault();
if (target is not ("web" or "desktop"))
{
    Console.Error.WriteLine("usage: measure-edit-to-view.cs web|desktop [--runs <n>] [--report <file>] [--budget <file>]");
    return 2;
}
var runs = 6;
string? reportPath = null, budgetPath = null;
for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--runs" when i + 1 < args.Length: runs = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
        case "--report" when i + 1 < args.Length: reportPath = Path.GetFullPath(args[++i]); break;
        case "--budget" when i + 1 < args.Length: budgetPath = Path.GetFullPath(args[++i]); break;
        default:
            Console.Error.WriteLine($"measure-edit-to-view: unknown argument {args[i]}");
            return 2;
    }
}
if (runs < 2)
{
    Console.Error.WriteLine("measure-edit-to-view: two runs at least, the cold one and a warm one");
    return 2;
}

var root = Environment.CurrentDirectory;
while (!File.Exists(Path.Combine(root, "eQuantic.UI.sln")))
    root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("run this from inside the repository");

// What each sample edits: a literal its screen draws, in a file a developer would edit.
var (project, source, literal, route) = target == "web"
    ? ("samples/DefaultUIDashboard", "Screens/DeclarativeScreen.cs", "\"Declarative all the way down — factories, no new.\"", "/declarative")
    : ("samples/PhotonDesktop", "Studio/StudioShell.cs", "\"eQuantic.UI · Photon tokens\"", (string?)null);
var file = Path.Combine(root, project, source);
// Put back byte for byte at the end, whatever the file's encoding and line endings were.
var originalBytes = File.ReadAllBytes(file);
var original = File.ReadAllText(file);
if (!original.Contains(literal, StringComparison.Ordinal))
{
    Console.Error.WriteLine($"measure-edit-to-view: {source} no longer holds {literal}, so there is nothing to edit");
    return 2;
}

var scratch = Directory.CreateTempSubdirectory("edit-to-view-").FullName;
var timings = Path.Combine(scratch, "eqc-timings.jsonl");
var lines = new ConcurrentQueue<(double At, string Text)>();
var port = target == "web" ? FreePort() : 0;
var measured = new List<Dictionary<string, double>>();
var failures = new List<string>();
Process? watch = null;
Process? chrome = null;
Cdp? page = null;

try
{
    watch = StartWatch();
    if (target == "web")
    {
        await WaitForAnswer($"http://127.0.0.1:{port}{route}", TimeSpan.FromMinutes(10));
        (chrome, page) = await OpenBrowser();
        await page.Send("Page.enable");
        await page.Send("Page.addScriptToEvaluateOnNewDocument", new { source = Observer });
        await page.Send("Page.navigate", new { url = $"http://127.0.0.1:{port}{route}" });
        await Until(async () => await page.Evaluate("document.readyState === 'complete' && !!window.__EQ_CONFIG") is JsonValue v
            && v.GetValue<bool>(), TimeSpan.FromMinutes(2), "the page did not load");
        // The page opens its stream to the server once it has started; a save before that is a save
        // nobody is told about.
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
    else
    {
        // dotnet watch waits for changes once it has launched the app, and the window has drawn its first
        // frame a few seconds after that.
        await Until(() => Task.FromResult(lines.Any(l => l.Text.Contains("dotnet watch", StringComparison.Ordinal)
            && l.Text.Contains("Waiting for changes", StringComparison.Ordinal))), TimeSpan.FromMinutes(10), "the app did not start");
        await Task.Delay(TimeSpan.FromSeconds(5));
    }

    for (var run = 1; run <= runs; run++)
    {
        var marker = $"Edit to view {run}";
        if (page is not null) await page.Evaluate($"sessionStorage.setItem('eq-edit-to-view:marker', {JsonSerializer.Serialize(marker)})");
        var saved = Now();
        File.WriteAllText(file, original.Replace(literal, JsonSerializer.Serialize(marker), StringComparison.Ordinal));
        var parts = target == "web" ? await MeasureWeb(marker, saved) : await MeasureDesktop(saved);
        parts["cold"] = run == 1 ? 1 : 0;
        measured.Add(parts);
        Console.WriteLine($"{target} run {run}{(run == 1 ? " (cold)" : "")}: {Describe(parts)}");
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}
catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or HttpRequestException or WebSocketException)
{
    failures.Add(ex.Message);
}
finally
{
    if (page is not null) await page.DisposeAsync();
    Stop(chrome);
    Stop(watch);
    File.WriteAllBytes(file, originalBytes);
    try { Directory.Delete(scratch, recursive: true); }
    catch (IOException) { /* a temporary folder a browser still held */ }
}

// The report: the cold run apart, the warm ones as a distribution.
var warm = measured.Where(r => r["cold"] == 0).ToList();
var partNames = target == "web"
    ? new[] { "total", "wait", "msbuild", "eqc", "bun", "replay", "early", "flips", "documents" }
    : new[] { "total", "delta", "frame" };
var summary = new JsonObject();
foreach (var name in partNames)
{
    var values = warm.Where(r => r.ContainsKey(name)).Select(r => r[name]).Order().ToArray();
    if (values.Length == 0) continue;
    summary[name] = new JsonObject
    {
        ["min"] = Math.Round(values[0]),
        ["median"] = Math.Round(Median(values)),
        ["max"] = Math.Round(values[^1]),
    };
}
var report = new JsonObject
{
    ["target"] = target,
    ["barMs"] = BarMs,
    ["machine"] = $"{Environment.OSVersion}, {Environment.ProcessorCount} cores",
    ["cold"] = measured.FirstOrDefault(r => r["cold"] == 1) is { } cold ? Round(cold) : null,
    ["warm"] = summary,
    ["runs"] = new JsonArray(measured.Select(r => (JsonNode)Round(r)).ToArray()),
    ["failures"] = new JsonArray(failures.Select(f => (JsonNode)f).ToArray()),
};
if (reportPath is not null)
{
    File.WriteAllText(reportPath, report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
    File.WriteAllLines(Path.ChangeExtension(reportPath, ".log"), lines.Select(l => $"{l.At:F0} {l.Text}"));
}
WriteSummary();

if (measured.Count < runs) failures.Add($"{measured.Count} of {runs} edits were measured");
if (budgetPath is not null && JsonNode.Parse(File.ReadAllText(budgetPath))?[target] is JsonObject budget)
{
    foreach (var (name, limit) in budget)
    {
        if (summary[name]?["median"] is not JsonValue median) failures.Add($"the budget names {name}, which was not measured");
        else if (median.GetValue<double>() > limit!.GetValue<double>())
            failures.Add($"{target} {name}: the warm median is {median.GetValue<double>():F0}ms, over the budget of {limit.GetValue<double>():F0}ms");
        // A ceiling above the bar only ever comes down: a change that made this part faster says by how
        // much, so the number it earned is written into the budget in the same pull request.
        else if (limit.GetValue<double>() > BarMs && median.GetValue<double>() < limit.GetValue<double>() / 2)
            Console.WriteLine($"::notice::{target} {name}: the warm median is {median.GetValue<double>():F0}ms, under half its ceiling of {limit.GetValue<double>():F0}ms: lower the ceiling in the budget");
    }
}
foreach (var failure in failures) Console.WriteLine($"::error::{failure}");
return failures.Count == 0 ? 0 : 1;

// ---- the web ---------------------------------------------------------------------------------

async Task<Dictionary<string, double>> MeasureWeb(string marker, double saved)
{
    // The edit is on screen once the browser runs the code that draws it: a document the server's
    // rebuild reloaded, drawing the text. A draw before that would come from a reload of something
    // else (dotnet watch's own refresh did that, with the patched server's markup and the old modules,
    // until #1275), and it is reported apart, as "early".
    JsonNode? state = null;
    JsonNode? settled = null;
    await Until(async () =>
    {
        state = await page!.Evaluate("sessionStorage.getItem('eq-edit-to-view')") is JsonValue text
            ? JsonNode.Parse(text.GetValue<string>()) : null;
        if (state is null) return false;
        var rebuilds = state["documents"]!.AsArray()
            .Where(d => d!["rebuild"]!.GetValue<bool>() && d["origin"]!.GetValue<double>() > saved)
            .Select(d => d!["origin"]!.GetValue<double>()).ToHashSet();
        settled = state["draws"]!.AsArray().LastOrDefault(d => d!["marker"]!.GetValue<string>() == marker
            && rebuilds.Contains(d["document"]!.GetValue<double>()));
        return settled is not null;
    }, TimeSpan.FromMinutes(5), $"the browser did not draw \"{marker}\" in a document the rebuild reloaded");
    var drawnAt = settled!["at"]!.GetValue<double>();
    var document = settled["document"]!.GetValue<double>();

    var parts = new Dictionary<string, double> { ["total"] = drawnAt - saved };
    var draws = state!["draws"]!.AsArray().Where(d => d!["marker"]!.GetValue<string>() == marker).ToList();
    parts["early"] = draws.Min(d => d!["at"]!.GetValue<double>()) - saved;
    // How many times the text went away again before it stayed, and how many documents the edit took.
    parts["flips"] = state["timeline"]!.AsArray().Count(t => t!["marker"]!.GetValue<string>() == marker
        && !t["present"]!.GetValue<bool>());
    parts["documents"] = state["documents"]!.AsArray().Count(d => d!["origin"]!.GetValue<double>() > saved);

    // ONE reload per save, the rebuild's, into markup that holds the edit, which the page hydrates
    // without a warning (#1275). Each of those is a behaviour, not a number to budget, so a run that
    // breaks one fails whatever its time.
    var reloaded = state["documents"]!.AsArray().First(d => d!["origin"]!.GetValue<double>() == document)!;
    if (parts["documents"] != 1)
        failures.Add($"\"{marker}\": the save reloaded the page {parts["documents"]:F0} times, where once is the rebuild's");
    if (reloaded["hydrated"]?.GetValue<bool>() != true)
        failures.Add($"\"{marker}\": the rebuild's document drew over the server's markup instead of hydrating it, so the server did not hold the edit");
    if (reloaded["warned"]?.GetValue<bool>() == true)
        failures.Add($"\"{marker}\": the rebuild's document warned while it hydrated, so the server's markup and the modules disagree");

    // Every boundary below is read off a clock that was there: eqc's own, and the browser's, whose new
    // document starts the moment the runtime asks for the reload (before the old one is even hidden,
    // which is why the old one's leaving is no boundary). Only the rebuild's start is a line dotnet
    // watch passes on late, and it cancels out of the sum.
    var started = LineAfter(saved, "[eQuantic.HotReload] a source changed, rebuilding");
    var eqc = File.Exists(timings)
        ? File.ReadAllLines(timings).Select(l => JsonNode.Parse(l)!)
            .LastOrDefault(t => t["started"]!.GetValue<double>() > saved && t["ended"]!.GetValue<double>() < drawnAt)
        : null;
    if (started is { } from && eqc is not null)
    {
        var eqcStarted = eqc["started"]!.GetValue<double>();
        var eqcEnded = eqc["ended"]!.GetValue<double>();
        var bunMs = eqc["bunMs"]!.GetValue<double>();
        parts["wait"] = from - saved;
        // MSBuild before eqc and after it, up to the page starting the reload the rebuild asked for: the
        // stream's message and the state's capture ride in the second half, both measured in milliseconds.
        parts["msbuild"] = (eqcStarted - from) + (document - eqcEnded);
        parts["eqc"] = eqcEnded - eqcStarted - bunMs;
        parts["bun"] = bunMs;
    }
    parts["replay"] = drawnAt - document;
    return parts;
}

async Task<(Process, Cdp)> OpenBrowser()
{
    var executable = Environment.GetEnvironmentVariable("EQ_CHROME")
        ?? (OperatingSystem.IsMacOS() ? "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" : "google-chrome");
    var profile = Path.Combine(scratch, "chrome");
    var start = new ProcessStartInfo(executable) { RedirectStandardError = true, RedirectStandardOutput = true };
    foreach (var argument in new[] { "--headless=new", "--remote-debugging-port=0", $"--user-data-dir={profile}",
        "--no-first-run", "--no-default-browser-check", "--window-size=1440,900", "about:blank" })
        start.ArgumentList.Add(argument);
    var process = Process.Start(start) ?? throw new InvalidOperationException($"{executable} did not start");
    process.ErrorDataReceived += (_, _) => { };
    process.OutputDataReceived += (_, _) => { };
    process.BeginErrorReadLine();
    process.BeginOutputReadLine();
    // Chrome writes the port it took to its profile once DevTools listens.
    var portFile = Path.Combine(profile, "DevToolsActivePort");
    await Until(() => Task.FromResult(File.Exists(portFile) && File.ReadAllLines(portFile).Length > 0),
        TimeSpan.FromSeconds(30), "Chrome did not open DevTools");
    var devtools = int.Parse(File.ReadAllLines(portFile)[0], CultureInfo.InvariantCulture);
    using var http = new HttpClient();
    var targets = JsonNode.Parse(await http.GetStringAsync($"http://127.0.0.1:{devtools}/json/list"))!.AsArray();
    var socket = targets.First(t => t!["type"]!.GetValue<string>() == "page")!["webSocketDebuggerUrl"]!.GetValue<string>();
    return (process, await Cdp.Connect(new Uri(socket)));
}

// ---- the desktop -----------------------------------------------------------------------------

async Task<Dictionary<string, double>> MeasureDesktop(double saved)
{
    var drew = new Regex(@"\[eQuantic\.HotReload\] drew the edit (\d+)ms after it was applied");
    (double At, string? Text) line = default;
    await Until(() =>
    {
        line = lines.FirstOrDefault(l => l.At > saved && drew.IsMatch(l.Text));
        return Task.FromResult(line.Text is not null);
    }, TimeSpan.FromMinutes(5), "the window did not draw the edit");
    var frame = double.Parse(drew.Match(line.Text!).Groups[1].Value, CultureInfo.InvariantCulture);
    return new Dictionary<string, double>
    {
        ["total"] = line.At - saved,
        ["delta"] = line.At - frame - saved,
        ["frame"] = frame,
    };
}

// ---- the parts both share --------------------------------------------------------------------

Process StartWatch()
{
    var start = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = root,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    foreach (var argument in new[] { "watch", "--project", project, "--non-interactive", "run", "--no-launch-profile" })
        start.ArgumentList.Add(argument);
    start.Environment["EQC_TIMINGS"] = timings;
    start.Environment["DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER"] = "1";
    start.Environment["DOTNET_WATCH_SUPPRESS_EMOJIS"] = "1";
    if (target == "web")
    {
        // As a developer runs it: Development, on a port of its own.
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
    }
    var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet watch did not start");
    process.OutputDataReceived += (_, e) => { if (e.Data is { } text) lines.Enqueue((Now(), text)); };
    process.ErrorDataReceived += (_, e) => { if (e.Data is { } text) lines.Enqueue((Now(), text)); };
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;
}

double? LineAfter(double after, string text) =>
    lines.Where(l => l.At >= after && l.Text.Contains(text, StringComparison.Ordinal)).Select(l => (double?)l.At).FirstOrDefault();

async Task WaitForAnswer(string url, TimeSpan limit)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    await Until(async () =>
    {
        if (watch!.HasExited) throw new InvalidOperationException("dotnet watch exited before the app answered");
        try { return (await http.GetAsync(url)).StatusCode == HttpStatusCode.OK; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return false; }
    }, limit, $"{url} did not answer");
}

static async Task Until(Func<Task<bool>> condition, TimeSpan limit, string failure)
{
    var clock = Stopwatch.StartNew();
    while (!await condition())
    {
        if (clock.Elapsed > limit) throw new TimeoutException($"{failure} in {limit.TotalSeconds:F0}s");
        await Task.Delay(25);
    }
}

static double Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

static int FreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var free = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return free;
}

static void Stop(Process? process)
{
    if (process is null) return;
    try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    catch (InvalidOperationException) { }
    process.WaitForExit(10_000);
    process.Dispose();
}

static double Median(double[] sorted) =>
    sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;

static JsonObject Round(Dictionary<string, double> parts) =>
    new(parts.Where(p => p.Key != "cold").Select(p => KeyValuePair.Create(p.Key, (JsonNode?)Math.Round(p.Value))));

static string Describe(Dictionary<string, double> parts) =>
    string.Join(", ", parts.Where(p => p.Key != "cold")
        .Select(p => p.Key is "flips" or "documents" ? $"{p.Key} {p.Value:F0}" : $"{p.Key} {p.Value:F0}ms"));

void WriteSummary()
{
    var text = new StringBuilder();
    text.Append("### Edit to view: ").Append(target).Append(", warm median against a bar of ").Append(BarMs).Append("ms\n\n");
    text.Append("| part | min | median | max | cold |\n|---|---:|---:|---:|---:|\n");
    var coldRun = measured.FirstOrDefault(r => r["cold"] == 1);
    foreach (var name in partNames)
    {
        if (summary[name] is not JsonObject part) continue;
        var coldValue = coldRun is not null && coldRun.TryGetValue(name, out var c) ? $"{c:F0}" : "";
        text.Append($"| {name} | {part["min"]} | {part["median"]} | {part["max"]} | {coldValue} |\n");
    }
    Console.WriteLine();
    Console.Write(text);
    if (Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } stepSummary)
        File.AppendAllText(stepSummary, text + "\n");
}

// What each new document records: when it started, and when it drew the edit's text. In
// sessionStorage, which a reload keeps, so the documents a reload replaces leave their answers behind.
// The text is looked for in what is ADDED, never by reading the whole page, which would force a layout
// on every mutation and slow down exactly what is being timed.
partial class Program
{
    const string Observer = """
        (() => {
          const key = 'eq-edit-to-view';
          const read = () => JSON.parse(sessionStorage.getItem(key) || '{"documents":[],"draws":[],"timeline":[]}');
          const write = (state) => sessionStorage.setItem(key, JSON.stringify(state));
          const origin = performance.timeOrigin;
          const state = read();
          // The runtime leaves __eq_hmr__ behind just before it reloads for a rebuild, and boot reads it
          // after this runs: a document started by anything else (dotnet watch's own refresh) finds none.
          state.documents.push({ origin, rebuild: sessionStorage.getItem('__eq_hmr__') !== null });
          write(state);
          // What the boot says in Development as it starts on the server's markup: that it hydrates it,
          // and any warning hydrating it raised. Heard here, before the boot runs, and passed on.
          const note = (field) => {
            const noted = read();
            const mine = noted.documents.find((d) => d.origin === origin);
            if (mine) { mine[field] = true; write(noted); }
          };
          const log = console.log.bind(console);
          const warn = console.warn.bind(console);
          console.log = (...args) => {
            if (typeof args[0] === 'string' && args[0].startsWith('Hydrating:')) note('hydrated');
            log(...args);
          };
          console.warn = (...args) => {
            if (args.some((a) => typeof a === 'string' && a.includes('Hydration warnings'))) note('warned');
            warn(...args);
          };
          let present = false;
          new MutationObserver(() => {
            const marker = sessionStorage.getItem(key + ':marker');
            if (!marker || !document.body) return;
            // textContent forces no layout, unlike innerText, so looking costs the page nothing it draws.
            const now = document.body.textContent.includes(marker);
            if (now === present) return;
            present = now;
            const changed = read();
            changed.timeline.push({ marker, document: origin, at: performance.timeOrigin + performance.now(), present: now });
            write(changed);
            if (!now) return;
            requestAnimationFrame(() => requestAnimationFrame((time) => {
              const drawn = read();
              drawn.draws.push({ marker, document: origin, at: performance.timeOrigin + time });
              write(drawn);
            }));
          }).observe(document, { childList: true, subtree: true, characterData: true });
        })();
        """;
}

/// <summary>Enough of the DevTools Protocol to drive one page: a command and its answer.</summary>
sealed class Cdp : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonNode>> _pending = new();
    private readonly CancellationTokenSource _closing = new();
    private Task? _reading;
    private int _next;

    public static async Task<Cdp> Connect(Uri endpoint)
    {
        var cdp = new Cdp();
        await cdp._socket.ConnectAsync(endpoint, CancellationToken.None);
        cdp._reading = cdp.Read();
        return cdp;
    }

    public async Task<JsonNode> Send(string method, object? parameters = null)
    {
        var id = Interlocked.Increment(ref _next);
        var answer = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = answer;
        var message = JsonSerializer.SerializeToUtf8Bytes(new { id, method, @params = parameters ?? new { } });
        await _socket.SendAsync(message, WebSocketMessageType.Text, true, CancellationToken.None);
        var result = await answer.Task.WaitAsync(TimeSpan.FromSeconds(30));
        if (result["error"] is { } error) throw new InvalidOperationException($"{method}: {error.ToJsonString()}");
        return result["result"]!;
    }

    /// <summary>The value an expression evaluates to in the page's current document.</summary>
    public async Task<JsonNode?> Evaluate(string expression) =>
        (await Send("Runtime.evaluate", new { expression, returnByValue = true }))["result"]?["value"];

    private async Task Read()
    {
        var buffer = new byte[1 << 16];
        var message = new MemoryStream();
        try
        {
            while (_socket.State == WebSocketState.Open)
            {
                var received = await _socket.ReceiveAsync(buffer, _closing.Token);
                if (received.MessageType == WebSocketMessageType.Close) break;
                message.Write(buffer, 0, received.Count);
                if (!received.EndOfMessage) continue;
                var node = JsonNode.Parse(message.ToArray());
                message.SetLength(0);
                if (node?["id"] is JsonValue id && _pending.TryRemove(id.GetValue<int>(), out var answer)) answer.SetResult(node);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
            // The page went away with the browser.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _closing.Cancel();
        if (_reading is not null) await _reading;
        _socket.Dispose();
    }
}
