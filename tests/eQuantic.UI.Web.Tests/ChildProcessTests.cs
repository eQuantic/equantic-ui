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
}
