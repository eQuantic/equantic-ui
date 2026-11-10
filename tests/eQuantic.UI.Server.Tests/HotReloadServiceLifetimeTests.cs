using System.Diagnostics;
using eQuantic.UI.Server.HotReload;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The rebuild and the streams of hot reload live as long as the app. dotnet watch stops an app it
/// restarts with SIGTERM to that process alone, and a rebuild left running wrote the files the
/// restart's own build writes, while each open tab's stream held the shutdown for seconds (#627).
/// A stand-in rebuild that sleeps plays the build, on every platform the suite runs on.
/// </summary>
public class HotReloadServiceLifetimeTests
{
    /// <summary>A server no edit reaches, as one run outside dotnet watch is.</summary>
    private static ServerEdits NoEdits => new(arriveOnSave: false, TimeSpan.Zero);

    /// <summary>The stand-in rebuild, started running and handed to the service, which owns its run as
    /// it owns a real rebuild's.</summary>
    private static Process Sleeping() => OperatingSystem.IsWindows()
        ? ChildProcess.Started("ping", ["-n", "60", "127.0.0.1"])
        : ChildProcess.Started("sleep", ["60"]);

    private static bool Running(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void EndIfRunning(int pid)
    {
        if (!Running(pid)) return;
        using var process = Process.GetProcessById(pid);
        process.Kill(entireProcessTree: true);
    }

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
        using var service = new HotReloadService(Path.GetTempPath(), () =>
        {
            Interlocked.Increment(ref started);
            running.Set();
            return OperatingSystem.IsWindows()
                ? ChildProcess.Started("ping", ["-n", "2", "127.0.0.1"])
                : ChildProcess.Started("sleep", ["1"]);
        }, TimeSpan.FromSeconds(30), NoEdits);

        var first = Task.Run(service.Rebuild);
        running.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("the first rebuild starts");
        // Two saves while it runs: each returns at once, and the one running owes them a rebuild.
        service.Rebuild();
        service.Rebuild();
        await first.WaitAsync(TimeSpan.FromSeconds(30));

        started.Should().Be(2, "the saves during a rebuild are one more rebuild, neither none nor one each");
    }

    [Fact]
    public void ARebuildPastItsLimit_IsStopped_NotAbandoned()
    {
        var pid = 0;
        using var service = new HotReloadService(Path.GetTempPath(), () =>
        {
            var process = Sleeping();
            pid = process.Id;
            return process;
        }, TimeSpan.FromSeconds(1), NoEdits);
        try
        {
            service.Rebuild();

            Running(pid).Should().BeFalse(
                "releasing the wrapper ends nothing: a build past its limit is stopped, or it writes beside the next one where shutdown cannot reach it");
        }
        finally
        {
            EndIfRunning(pid);
        }
    }

    [Fact]
    public async Task Disposing_StopsARebuildInFlight()
    {
        var pid = 0;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new HotReloadService(Path.GetTempPath(), () =>
        {
            var process = Sleeping();
            pid = process.Id;
            started.SetResult();
            return process;
        }, TimeSpan.FromMinutes(1), NoEdits);
        try
        {
            var rebuild = Task.Run(service.Rebuild);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(30));

            service.Dispose();
            await rebuild.WaitAsync(TimeSpan.FromSeconds(30));

            Running(pid).Should().BeFalse("a rebuild belongs to the app, and stops with it");
        }
        finally
        {
            EndIfRunning(pid);
        }
    }

    /// <summary>
    /// The host awaits Dispose and nothing after it. A shutdown that came while a rebuild's process was
    /// starting returned at once, before the process existed, and the kill the rebuild ran afterwards
    /// was nobody's to wait for (Copilot's second review of #666).
    /// </summary>
    [Fact]
    public async Task Disposing_WhileARebuildStarts_ReturnsOnlyOnceItHasStopped()
    {
        var pid = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var service = new HotReloadService(Path.GetTempPath(), () =>
        {
            entered.SetResult();
            release.Wait(TimeSpan.FromSeconds(30));
            var process = Sleeping();
            pid = process.Id;
            return process;
        }, TimeSpan.FromMinutes(1), NoEdits);
        try
        {
            var rebuild = Task.Run(service.Rebuild);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));

            var disposing = Task.Run(service.Dispose);
            await Task.Delay(300);
            disposing.IsCompleted.Should().BeFalse("Dispose waits for the rebuild that is starting, or it returns before there is anything to stop");

            release.Set();
            await disposing.WaitAsync(TimeSpan.FromSeconds(30));
            Running(pid).Should().BeFalse("by the time Dispose returns, the rebuild it found starting has stopped");
            await rebuild.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            release.Set();
            if (pid != 0) EndIfRunning(pid);
        }
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
