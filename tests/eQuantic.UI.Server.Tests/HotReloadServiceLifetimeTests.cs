using eQuantic.UI.Server.HotReload;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The rebuilds and the streams of hot reload live as long as the app. dotnet watch stops an app it
/// restarts with SIGTERM to that process alone, and each open tab's stream held the shutdown for
/// seconds (#627). What stops the running eqc is <see cref="RunningCompilerTests"/>'s.
/// </summary>
public class HotReloadServiceLifetimeTests
{
    /// <summary>A server no edit reaches, as one run outside dotnet watch is.</summary>
    private static ServerEdits NoEdits => new(arriveOnSave: false, TimeSpan.Zero);

    /// <summary>
    /// A save while a rebuild runs is rebuilt after it, once, however many saves came. The server used
    /// to drop it, and the browser stayed on the edit before it until the next save (#1262). A rebuild
    /// is measured in seconds (#900), so two saves inside one are the ordinary case, not a race.
    /// </summary>
    [Fact]
    public async Task SavesDuringARebuild_AreRebuiltAfterIt_Once()
    {
        var started = 0;
        using var running = new ManualResetEventSlim();
        using var service = new HotReloadService(Path.GetTempPath(), new StandInCompiler(_ =>
        {
            Interlocked.Increment(ref started);
            running.Set();
            Thread.Sleep(1_000);
            return CompileOutcome.Done;
        }), TimeSpan.FromSeconds(30), NoEdits);

        var first = Task.Run(service.Rebuild);
        running.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("the first rebuild starts");
        // Two saves while it runs: each returns at once, and the one running owes them a rebuild.
        service.Rebuild();
        service.Rebuild();
        await first.WaitAsync(TimeSpan.FromSeconds(30));

        started.Should().Be(2, "the saves during a rebuild are one more rebuild, neither none nor one each");
    }

    /// <summary>The service owns what compiles the modules, and stopping the app stops it.</summary>
    [Fact]
    public void Disposing_DisposesTheCompiler()
    {
        var compiler = StandInCompiler.Compiling();
        var service = new HotReloadService(Path.GetTempPath(), compiler, TimeSpan.FromSeconds(30), NoEdits);

        service.Dispose();

        compiler.Disposals.Should().Be(1);
    }

    [Fact]
    public async Task AStreamThatRegistersAfterShutdown_EndsAtOnce()
    {
        var service = new HotReloadService(Path.GetTempPath());
        service.Dispose();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handling = service.HandleClient(context);
        var finished = await Task.WhenAny(handling, Task.Delay(TimeSpan.FromSeconds(5)));

        finished.Should().BeSameAs(handling,
            "a stream registered after the service stopped would hold the app's shutdown until the host gave up on it");
    }
}
