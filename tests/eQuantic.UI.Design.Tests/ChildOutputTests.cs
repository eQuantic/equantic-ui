using eQuantic.UI.Tests;
using FluentAssertions;

namespace eQuantic.UI.Design.Tests;

/// <summary>
/// The design host reads its renderer to the end under one deadline (#997). It waited for the exit before
/// reading either stream, so a renderer that wrote more than a pipe holds blocked on the full pipe, never
/// exited, and was reported as a page whose Build() does not return.
/// </summary>
public sealed class ChildOutputTests
{
    [Fact]
    public void AChildThatWritesMoreThanAPipeHolds_Finishes()
    {
        if (OperatingSystem.IsWindows()) return;
        using var child = ChildProcess.Started("sh", ["-c", "head -c 400000 /dev/zero | tr '\\0' 'x'"]);

        var (ended, output, _) = ChildOutput.Read(child, TimeSpan.FromSeconds(20));

        ended.Should().BeTrue("both streams drain from the start, so the pipe never fills");
        output.Length.Should().Be(400000);
    }

    [Fact]
    public void AChildThatDoesNotEnd_IsToldByTheDeadline()
    {
        if (OperatingSystem.IsWindows()) return;
        using var child = ChildProcess.Started("sleep", ["30"]);

        var (ended, _, _) = ChildOutput.Read(child, TimeSpan.FromMilliseconds(300));
        child.Kill(entireProcessTree: true);

        ended.Should().BeFalse();
    }

    [Fact]
    public void AStreamSomethingElseHolds_IsHeldToTheDeadlineToo()
    {
        if (OperatingSystem.IsWindows()) return;
        using var child = ChildProcess.Started("sh", ["-c", "sleep 8 & exit 0"]);

        var (ended, _, _) = ChildOutput.Read(child, TimeSpan.FromMilliseconds(500));

        ended.Should().BeFalse("a stream ends when the last process holding it closes it, not at the exit");
    }
}
