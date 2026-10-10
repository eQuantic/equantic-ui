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
    private static ProcessStartInfo Sleeping() => OperatingSystem.IsWindows()
        ? new ProcessStartInfo("ping", "-n 60 127.0.0.1") { RedirectStandardOutput = true, RedirectStandardError = true }
        : new ProcessStartInfo("sleep", "60") { RedirectStandardOutput = true, RedirectStandardError = true };

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

    [Fact]
    public void ARebuildPastItsLimit_IsStopped_NotAbandoned()
    {
        var pid = 0;
        using var service = new HotReloadService(Path.GetTempPath(), () =>
        {
            var process = Process.Start(Sleeping())!;
            pid = process.Id;
            return process;
        }, TimeSpan.FromSeconds(1));
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
            var process = Process.Start(Sleeping())!;
            pid = process.Id;
            started.SetResult();
            return process;
        }, TimeSpan.FromMinutes(1));
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
            var process = Process.Start(Sleeping())!;
            pid = process.Id;
            return process;
        }, TimeSpan.FromMinutes(1));
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
