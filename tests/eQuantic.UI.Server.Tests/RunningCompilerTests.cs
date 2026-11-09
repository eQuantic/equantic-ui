using System.Diagnostics;
using eQuantic.UI.Server.HotReload;

namespace eQuantic.UI.Server.Tests;

/// <summary>
/// The eqc a server keeps running for an app in development (#1276), played by a script that speaks
/// its protocol: <c>compile</c> on its standard input, one <c>@eqc</c> line of JSON in answer. Every
/// save used to start a new MSBuild and a new eqc, measured at 6.8s of MSBuild alone on CI (#900).
/// </summary>
public class RunningCompilerTests
{
    private const string Compiled = """{"compiled":true,"errors":[]}""";

    /// <summary>A stand-in eqc that answers every compile with <paramref name="answer"/>.</summary>
    private static ProcessStartInfo Answering(string answer)
    {
        var folder = Directory.CreateTempSubdirectory("eq-eqc-").FullName;
        ProcessStartInfo start;
        if (OperatingSystem.IsWindows())
        {
            var script = Path.Combine(folder, "eqc.cmd");
            File.WriteAllText(script, string.Join("\r\n",
                "@echo off", ":loop", "set \"line=\"", "set /p line=", "if not defined line exit /b 0",
                $"echo {RunningCompiler.AnswerPrefix}{answer}", "goto loop", ""));
            start = RunningCompiler.Started("cmd", folder, ["/c", script]);
        }
        else
        {
            var script = Path.Combine(folder, "eqc.sh");
            File.WriteAllText(script, $"while read line; do echo '{RunningCompiler.AnswerPrefix}{answer}'; done\n");
            start = RunningCompiler.Started("/bin/sh", folder, [script]);
        }
        start.RedirectStandardInput = true;
        return start;
    }

    /// <summary>A stand-in eqc that never answers.</summary>
    private static ProcessStartInfo Silent()
    {
        var start = OperatingSystem.IsWindows()
            ? RunningCompiler.Started("ping", Path.GetTempPath(), ["-n", "120", "127.0.0.1"])
            : RunningCompiler.Started("sleep", Path.GetTempPath(), ["120"]);
        start.RedirectStandardInput = true;
        return start;
    }

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

    private static void EndIfRunning(int? pid)
    {
        if (pid is not { } id || !Running(id)) return;
        using var process = Process.GetProcessById(id);
        process.Kill(entireProcessTree: true);
    }

    [Fact]
    public void ACompile_IsAnsweredByTheRunningEqc_AndTheNextOneGoesToTheSameProcess()
    {
        var found = 0;
        using var compiler = new RunningCompiler(_ =>
        {
            found++;
            return Answering(Compiled);
        });

        var first = compiler.Compile(TimeSpan.FromSeconds(30));
        var pid = compiler.ProcessId;
        var second = compiler.Compile(TimeSpan.FromSeconds(30));

        first.Should().Be(CompileOutcome.Done);
        second.Compiled.Should().BeTrue();
        compiler.ProcessId.Should().Be(pid, "a save goes to the eqc already running, with no process started for it");
        found.Should().Be(1, "MSBuild says how to start eqc once for a run of the server");
    }

    [Fact]
    public void AFailedCompile_AnswersWithItsErrorLines()
    {
        using var compiler = new RunningCompiler(_ =>
            Answering("""{"compiled":false,"errors":["Screens/A.cs(3,7): error CS1002: expected"]}"""));

        var outcome = compiler.Compile(TimeSpan.FromSeconds(30));

        outcome.Compiled.Should().BeFalse();
        outcome.Errors.Should().Equal("Screens/A.cs(3,7): error CS1002: expected");
    }

    /// <summary>An eqc that died is the next save's to start again, with what MSBuild said before.</summary>
    [Fact]
    public void AnEqcThatDied_IsStartedAgainByTheNextSave()
    {
        var found = 0;
        using var compiler = new RunningCompiler(_ =>
        {
            found++;
            return Answering(Compiled);
        });
        compiler.Compile(TimeSpan.FromSeconds(30)).Compiled.Should().BeTrue();
        var first = compiler.ProcessId!.Value;
        using (var process = Process.GetProcessById(first)) process.Kill(entireProcessTree: true);
        SpinWait.SpinUntil(() => !Running(first), TimeSpan.FromSeconds(10));

        var again = compiler.Compile(TimeSpan.FromSeconds(30));

        again.Compiled.Should().BeTrue();
        compiler.ProcessId.Should().NotBe(first);
        found.Should().Be(1);
    }

    /// <summary>A compile past its limit stops eqc rather than leaving it to write beside the next one.</summary>
    [Fact]
    public void ACompilePastItsLimit_StopsEqc_NotAbandoned()
    {
        using var compiler = new RunningCompiler(_ => Silent());
        try
        {
            var outcome = compiler.Compile(TimeSpan.FromSeconds(1));

            outcome.Compiled.Should().BeFalse();
            SpinWait.SpinUntil(() => !Running(compiler.ProcessId!.Value), TimeSpan.FromSeconds(10));
            Running(compiler.ProcessId!.Value).Should().BeFalse("a compile past its limit is stopped, never abandoned");
        }
        finally
        {
            EndIfRunning(compiler.ProcessId);
        }
    }

    /// <summary>Stopping the app stops eqc, and the compile in flight answers at once.</summary>
    [Fact]
    public async Task Disposing_StopsACompileInFlight()
    {
        var compiler = new RunningCompiler(_ => Silent());
        int? pid = null;
        try
        {
            var compile = Task.Run(() => compiler.Compile(TimeSpan.FromMinutes(2)));
            SpinWait.SpinUntil(() => compiler.ProcessId is not null, TimeSpan.FromSeconds(30));
            pid = compiler.ProcessId;

            compiler.Dispose();
            var outcome = await compile.WaitAsync(TimeSpan.FromSeconds(30));

            outcome.Compiled.Should().BeFalse();
            Running(pid!.Value).Should().BeFalse("eqc belongs to the app, and stops with it");
        }
        finally
        {
            EndIfRunning(pid);
        }
    }

    /// <summary>
    /// A shutdown while MSBuild is still saying how to start eqc starts no eqc after it: the process
    /// started late was nobody's to stop (Copilot's second review of #666, for the rebuild before).
    /// </summary>
    [Fact]
    public async Task Disposing_WhileEqcIsBeingFound_StartsNoProcess()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var compiler = new RunningCompiler(_ =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(30));
            return Answering(Compiled);
        });

        var compile = Task.Run(() => compiler.Compile(TimeSpan.FromSeconds(30)));
        entered.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue();
        compiler.Dispose();
        release.Set();
        var outcome = await compile.WaitAsync(TimeSpan.FromSeconds(30));

        outcome.Compiled.Should().BeFalse();
        compiler.ProcessId.Should().BeNull("nothing starts once the app has stopped");
    }

    /// <summary>
    /// eqc is started without the variables dotnet watch gives the app it runs: with them, eqc loaded
    /// dotnet watch's agent and reached for the app's own channel to it.
    /// </summary>
    [Fact]
    public void Eqc_StartsWithoutDotnetWatchsVariables()
    {
        // The one of them no other test's process reads, set as dotnet watch sets it on the app.
        const string channel = "DOTNET_WATCH_HOTRELOAD_NAMEDPIPE_NAME";
        var before = Environment.GetEnvironmentVariable(channel);
        Environment.SetEnvironmentVariable(channel, "app-channel");
        try
        {
            var started = RunningCompiler.Started("dotnet", Path.GetTempPath(), ["eqc.dll"]);

            started.Environment.ContainsKey(channel).Should().BeFalse();
            started.Environment.Keys.Should().NotContain(["DOTNET_STARTUP_HOOKS", "DOTNET_MODIFIABLE_ASSEMBLIES",
                "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(channel, before);
        }
    }
}
