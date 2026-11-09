using System.Diagnostics;
using System.Text;
using eQuantic.UI.Server.HotReload;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// What the rebuild stream tells a page, which is every reason the page has to reload (#1275). Each
/// save reloaded it twice: dotnet watch refreshed it as soon as the server took the edit, with the
/// modules from before it, and the rebuild reloaded it again seconds later. The stream now says
/// which run of the server the page talks to, when the modules are new and whether the server's
/// markup holds the edit too, and why a rebuild failed.
/// </summary>
public class HotReloadStreamTests
{
    /// <summary>A stand-in rebuild that says <paramref name="says"/> and exits with <paramref name="code"/>.</summary>
    private static Process Exits(int code, string says = "") => OperatingSystem.IsWindows()
        ? ChildProcess.Started("cmd", ["/c", $"echo {says}& exit /b {code}"])
        : ChildProcess.Started("/bin/sh", ["-c", $"echo '{says}'; exit {code}"]);

    private static HotReloadService Service(Func<Process?> rebuild, ServerEdits edits) =>
        new(Path.GetTempPath(), rebuild, TimeSpan.FromSeconds(30), edits);

    /// <summary>What a page reads off the stream: every frame written to it, as it was written.</summary>
    private sealed class Page : Stream
    {
        private readonly StringBuilder _read = new();

        public string Heard
        {
            get
            {
                lock (_read) return _read.ToString();
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_read) _read.Append(Encoding.UTF8.GetString(buffer, offset, count));
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            lock (_read) _read.Append(Encoding.UTF8.GetString(buffer.Span));
            return ValueTask.CompletedTask;
        }

        public override void Flush() { }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    /// <summary>A page listening to <paramref name="service"/>, once its first frame has arrived.</summary>
    private static async Task<(Page Page, Task Listening, CancellationTokenSource Leave)> ListenAsync(HotReloadService service)
    {
        var page = new Page();
        var leave = new CancellationTokenSource();
        var context = new DefaultHttpContext { RequestAborted = leave.Token };
        context.Response.Body = page;
        var listening = service.HandleClient(context);
        var clock = Stopwatch.StartNew();
        while (page.Heard.Length == 0 && clock.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(10);
        return (page, listening, leave);
    }

    private static async Task<string> UntilAsync(Page page, string frame)
    {
        var clock = Stopwatch.StartNew();
        while (!page.Heard.Contains(frame, StringComparison.Ordinal) && clock.Elapsed < TimeSpan.FromSeconds(20))
            await Task.Delay(10);
        return page.Heard;
    }

    /// <summary>
    /// Every connection opens by naming the run of the server it reached, so a page that reconnects to
    /// another run reloads. dotnet watch's script did that after a restart, and it stays out of the page
    /// now. Two runs never share a name, and one run gives every page the same.
    /// </summary>
    [Fact]
    public async Task EveryConnection_OpensByNamingTheRunOfTheServer()
    {
        using var first = Service(() => Exits(0), new ServerEdits(false, TimeSpan.Zero));
        using var second = Service(() => Exits(0), new ServerEdits(false, TimeSpan.Zero));

        var (a, aListening, aLeave) = await ListenAsync(first);
        var (b, bListening, bLeave) = await ListenAsync(first);
        var (c, cListening, cLeave) = await ListenAsync(second);
        aLeave.Cancel();
        bLeave.Cancel();
        cLeave.Cancel();
        await Task.WhenAll(aListening, bListening, cListening);

        a.Heard.Should().MatchRegex("^retry: 1000\nevent: server\ndata: [0-9a-f]{32}\n\n$",
            "a page waits a second between attempts to reach the next run, and is told this one's name first");
        b.Heard.Should().Be(a.Heard, "one run names itself the same to every page");
        c.Heard.Should().NotBe(a.Heard, "a page that reconnects to another run must be able to tell");
    }

    /// <summary>
    /// The edit of a save reaches the server while the modules rebuild, and the reload waits for it:
    /// the page it reloads into is served by a server holding the edit, so it hydrates. The edit lands
    /// here after the save and before the rebuild starts, which is the ordinary order, since the wait
    /// after a save is shorter than dotnet watch takes to apply it, or about it.
    /// </summary>
    [Fact]
    public async Task ARebuild_WhoseSaveReachedTheServer_SaysThePageHydrates()
    {
        var edits = new ServerEdits(arriveOnSave: true, TimeSpan.FromSeconds(10));
        using var service = new HotReloadService(
            Directory.CreateTempSubdirectory("eq-stream-").FullName, () => Exits(0), TimeSpan.FromSeconds(30), edits);
        service.Start();
        var (page, listening, leave) = await ListenAsync(service);

        service.SourceChanged();
        edits.Take();
        var read = await UntilAsync(page, "event: rebuilt");
        leave.Cancel();
        await listening;

        read.Should().EndWith("event: rebuilt\ndata: hydrate\n\n");
    }

    /// <summary>
    /// When every save brings the server its edit, the rebuild waits for it, and announces itself once
    /// it lands, not before: a page reloaded before would hydrate markup from before the edit.
    /// </summary>
    [Fact]
    public async Task ARebuild_ThatEndsBeforeTheEditLands_WaitsForIt()
    {
        var edits = new ServerEdits(arriveOnSave: true, TimeSpan.FromSeconds(10));
        using var service = Service(() => Exits(0), edits);
        var (page, listening, leave) = await ListenAsync(service);

        var rebuild = Task.Run(service.Rebuild);
        await Task.Delay(500);
        var before = page.Heard;
        edits.Take();
        await rebuild.WaitAsync(TimeSpan.FromSeconds(20));
        leave.Cancel();
        await listening;

        before.Should().NotContain("event: rebuilt", "the edit had not reached the server yet");
        page.Heard.Should().EndWith("event: rebuilt\ndata: hydrate\n\n");
    }

    /// <summary>
    /// A save that brings no edit (one that changes nothing .NET compiles) still reloads the page,
    /// once the edit is no longer coming, and the page draws over the server's markup.
    /// </summary>
    [Fact]
    public async Task ARebuild_WhoseEditNeverCame_SaysThePageMounts_OnceItIsNoLongerComing()
    {
        var limit = TimeSpan.FromMilliseconds(400);
        using var service = Service(() => Exits(0), new ServerEdits(arriveOnSave: true, limit));
        var (page, listening, leave) = await ListenAsync(service);

        var clock = Stopwatch.StartNew();
        service.Rebuild();
        var took = clock.Elapsed;
        leave.Cancel();
        await listening;

        page.Heard.Should().EndWith("event: rebuilt\ndata: mount\n\n");
        took.Should().BeGreaterThanOrEqualTo(limit - TimeSpan.FromMilliseconds(50),
            "an edit may arrive until the limit after the save");
    }

    /// <summary>
    /// A server no save reaches (a run outside dotnet watch) renders markup from before the edit, and
    /// the page is told at once to draw over it, as it did before the stream said anything else.
    /// </summary>
    [Fact]
    public async Task ARebuild_OfAServerNoSaveReaches_SaysThePageMounts_AtOnce()
    {
        using var service = Service(() => Exits(0), new ServerEdits(arriveOnSave: false, TimeSpan.FromMinutes(1)));
        var (page, listening, leave) = await ListenAsync(service);

        var clock = Stopwatch.StartNew();
        service.Rebuild();
        var took = clock.Elapsed;
        leave.Cancel();
        await listening;

        page.Heard.Should().EndWith("event: rebuilt\ndata: mount\n\n");
        took.Should().BeLessThan(TimeSpan.FromSeconds(30), "nothing is waited for when no edit is coming");
    }

    /// <summary>
    /// A rebuild that fails says why to the page, which shows it: dotnet watch's script showed the
    /// compiler's errors over the page, and it stays out of the page now.
    /// </summary>
    [Fact]
    public async Task AFailedRebuild_SendsItsErrors_AndNoReload()
    {
        const string error = "Screens/A.cs(3,7): error CS1002: ; expected";
        using var service = Service(() => Exits(1, error), new ServerEdits(arriveOnSave: false, TimeSpan.Zero));
        var (page, listening, leave) = await ListenAsync(service);

        service.Rebuild();
        var read = await UntilAsync(page, "event: failed");
        leave.Cancel();
        await listening;

        read.Should().EndWith("event: failed\ndata: [\"Screens/A.cs(3,7): error CS1002: ; expected\"]\n\n");
        read.Should().NotContain("event: rebuilt");
    }

    /// <summary>
    /// The app's own static files reload the page, which dotnet watch's refresh script did before it was
    /// kept out of the page, and the page hydrates, since nothing a file holds is in the markup. eqc's
    /// output is the rebuild's to announce, never a file change of its own.
    /// </summary>
    [Fact]
    public async Task AStaticFileOfTheApp_ReloadsThePage_AndEqcsOutputDoesNot()
    {
        var root = Directory.CreateTempSubdirectory("eq-assets-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "wwwroot", "_equantic"));
        using var service = new HotReloadService(root, () => Exits(0), TimeSpan.FromSeconds(30), new ServerEdits(false, TimeSpan.Zero));
        service.Start();
        var (page, listening, leave) = await ListenAsync(service);

        await File.WriteAllTextAsync(Path.Combine(root, "wwwroot", "_equantic", "Page.js"), "export {};");
        await Task.Delay(1500);
        var afterModule = page.Heard;
        await File.WriteAllTextAsync(Path.Combine(root, "wwwroot", "logo.svg"), "<svg/>");
        var read = await UntilAsync(page, "event: rebuilt");
        leave.Cancel();
        await listening;

        afterModule.Should().NotContain("event: rebuilt", "a module eqc wrote is announced by the rebuild that wrote it");
        read.Should().EndWith("event: rebuilt\ndata: hydrate\n\n");
    }

    /// <summary>
    /// An app that stops is not held by a rebuild waiting for an edit that will never arrive: dotnet
    /// watch stops an app it restarts, and the wait gave way only at its limit.
    /// </summary>
    [Fact]
    public void Stopping_EndsTheWaitForAnEdit()
    {
        var edits = new ServerEdits(arriveOnSave: true, TimeSpan.FromMinutes(1));
        using var stopping = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var clock = Stopwatch.StartNew();
        var arrived = edits.ArrivedAfter(edits.Taken, Stopwatch.GetTimestamp(), stopping.Token);

        arrived.Should().BeFalse();
        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), "the wait ends when the app stops, not at its limit");
    }

    /// <summary>MSBuild's error lines, each once, and the end of the output when it wrote none.</summary>
    [Fact]
    public void TheErrorsOfAFailedRebuild_AreMSBuildsErrorLines_OrTheEndOfWhatItSaid()
    {
        var error = "/app/Screens/A.cs(3,7): error CS1002: ; expected [/app/App.csproj]";
        HotReloadService.ErrorsOf($"Build started\n{error}\n  warning CS0168: x\n{error}\n")
            .Should().Equal(error);

        var said = Enumerable.Range(1, HotReloadService.ErrorsShown + 5).Select(n => $"line {n}").ToArray();
        HotReloadService.ErrorsOf(string.Join('\n', said))
            .Should().Equal(said.TakeLast(HotReloadService.ErrorsShown));
    }
}
