using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The one way a test starts a program, held to the hang it exists for (#971). A stream ends when the
/// last process holding its write end closes it, not when the program exits, so a program that leaves
/// a child running with its output is a program whose output does not end.
/// </summary>
public class ChildProcessTests
{
    /// <summary>
    /// The Web suite's hang, made deterministic: the shell exits at once, and the sleep it leaves
    /// running holds its stdout for thirty seconds. The run answers within its two-second deadline,
    /// as a <see cref="TimeoutException"/> naming the command, instead of waiting out the child. The
    /// case gives it twenty seconds, so a run still waiting by then is waiting for the sleep.
    /// </summary>
    [UnixFact]
    public void AProgramThatLeavesAChildHoldingItsOutput_FailsWithinItsDeadline_InsteadOfWaitingForTheChild()
    {
        var run = Task.Run(() => ChildProcess.Run("/bin/sh", ["-c", "sleep 30 & echo done"], TimeSpan.FromSeconds(2)));

        Task.WaitAny([run], TimeSpan.FromSeconds(20)).Should().Be(0,
            "the run's deadline is two seconds, and a run still waiting after twenty is waiting for the "
            + "thirty-second sleep the shell left holding its stdout");
        (run.Exception?.InnerException).Should().BeOfType<TimeoutException>()
            .Which.Message.Should().Contain("/bin/sh -c sleep 30 & echo done", "the failure names the command");
    }

    /// <summary>
    /// The build the suite hung in, under the conditions that made it hang, on every run rather than on
    /// about every other one. <c>dotnet test</c> hands the test host
    /// <c>MSBUILDENSURESTDOUTFORTASKPROCESSES</c>, given here as well so the case does not depend on
    /// what started this host, and with it MSBuild starts each worker node on the probe's own stdout and
    /// stderr. Every project builds out of process, so the build starts a node, and under a handshake
    /// salt no other build's node carries, so the node is its own rather than an idle one an earlier
    /// build left. That node outlived the build, reusable, and the probe's read waited for it. Under the
    /// probe's environment it ends with the build, and the probe returns: were anything still holding
    /// the pipes, the probe would fail at its deadline instead, saying so.
    /// </summary>
    [Fact]
    public void AProbeBuild_LeavesNoWorkerNodeHoldingItsOutput_EvenOneStartedOnIt()
    {
        var scratch = Directory.CreateTempSubdirectory("eq-probe-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(scratch, "Probe.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(scratch, "Probe.cs"), "public static class Probe { public static int One() => 1; }\n");

            var (exitCode, output, error) = SdkProbe.Run(scratch, new Dictionary<string, string>
            {
                ["MSBUILDENSURESTDOUTFORTASKPROCESSES"] = "1",
                ["MSBUILDNOINPROCNODE"] = "1",
                ["MSBUILDNODEHANDSHAKESALT"] = Path.GetFileName(scratch),
            }, "build", "-nologo", "-v:m");

            exitCode.Should().Be(0, output + error);
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }
}
