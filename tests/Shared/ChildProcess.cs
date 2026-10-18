using System.Diagnostics;

namespace eQuantic.UI.Tests;

/// <summary>
/// How a test starts a program and reads what it wrote (#971): its arguments go to it as a
/// list, never through a shell, both of its streams drain from the start, so a child that fills the
/// pipe nobody is reading never stops on it, and ONE deadline holds the whole run, the exit and the
/// end of both streams alike.
/// <para>
/// A stream ends when the last process holding its write end closes it, not when the program exits.
/// A program that leaves a child running with its output is a program whose output does not end while
/// that child lives, and the Web suite hung on exactly that, every thread waiting, until someone killed
/// it: a probe's <c>dotnet build</c> exited, a worker node it had started kept the probe's stdout and
/// stderr, idle and reusable, and the reads waited for it with no deadline. So a run that has not
/// ended by its deadline fails as a <see cref="TimeoutException"/> naming the command, whichever part
/// of it is late, and never holds the test host.
/// </para>
/// <para>
/// Linked into each test project that starts <c>dotnet</c>, and every <c>dotnet</c> a test starts goes
/// through <see cref="Dotnet"/>, which starts it so that nothing it starts outlives it:
/// <c>ChildProcessTests</c> fails on one started anywhere else.
/// </para>
/// </summary>
internal static class ChildProcess
{
    /// <summary>
    /// What a <c>dotnet</c> a test starts runs under, beneath what the caller hands it: a build that
    /// leaves nothing running behind it, and no worker node handed the test's pipes.
    /// <list type="bullet">
    /// <item><c>MSBUILDENSURESTDOUTFORTASKPROCESSES</c> is taken away. <c>dotnet test</c> sets it for its
    /// own build, the test host carries it, and every child the host starts inherits it. With it, MSBuild
    /// starts each worker node on its own stdout and stderr, which are the test's pipes, and a node is
    /// reusable: it outlives the build, idle, for as long as later builds keep taking it. Measured on a
    /// probe's build (#971): it exited after two seconds and its streams were still open thirty seconds
    /// later, held by the node it started; without the variable, the node held neither.</item>
    /// <item><c>MSBUILDDISABLENODEREUSE=1</c>: the worker nodes end with the build.</item>
    /// <item><c>DOTNET_CLI_USE_MSBUILD_SERVER=0</c>, and <c>UseSharedCompilation=false</c>, a property
    /// MSBuild reads from the environment as it reads every variable: no MSBuild server and no compiler
    /// server outlives the build either.</item>
    /// </list>
    /// </summary>
    private static readonly KeyValuePair<string, string?>[] OwnBuild =
    [
        new("MSBUILDENSURESTDOUTFORTASKPROCESSES", null),
        new("MSBUILDDISABLENODEREUSE", "1"),
        new("DOTNET_CLI_USE_MSBUILD_SERVER", "0"),
        new("UseSharedCompilation", "false"),
    ];

    /// <summary>
    /// <c>dotnet</c> with <paramref name="arguments"/>, as <see cref="Run"/> starts a program, under
    /// <see cref="OwnBuild"/> and then <paramref name="environment"/>, so that a caller's own setting of
    /// any of those variables wins.
    /// </summary>
    public static (int ExitCode, string Output, string Error) Dotnet(
        IEnumerable<string> arguments,
        TimeSpan deadline,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null) =>
        Run("dotnet", arguments, deadline, workingDirectory,
            [.. OwnBuild, .. environment?.Select(setting => KeyValuePair.Create(setting.Key, (string?)setting.Value)) ?? []]);

    /// <summary>
    /// Runs <paramref name="program"/> with <paramref name="arguments"/> in
    /// <paramref name="workingDirectory"/>, this process's own when it is null, with
    /// <paramref name="environment"/> over this process's own, in order, a null value removing the
    /// variable it names, and answers its exit code and what it wrote to each stream. A program still
    /// running at <paramref name="deadline"/> is killed with every process it started; one that exited
    /// while a process it left behind still holds its output is left to it. Either way the run fails
    /// as a <see cref="TimeoutException"/> naming the command.
    /// </summary>
    public static (int ExitCode, string Output, string Error) Run(
        string program,
        IEnumerable<string> arguments,
        TimeSpan deadline,
        string? workingDirectory = null,
        IEnumerable<KeyValuePair<string, string?>>? environment = null)
    {
        var info = new ProcessStartInfo(program)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory ?? string.Empty,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        foreach (var (name, value) in environment ?? [])
        {
            if (value is null) info.Environment.Remove(name);
            else info.Environment[name] = value;
        }
        var command = string.Join(' ', [program, .. info.ArgumentList]);

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (Task.WhenAll(process.WaitForExitAsync(), output, error).Wait(deadline))
            return (process.ExitCode, output.Result, error.Result);

        if (process.HasExited)
            throw new TimeoutException(
                $"{command} exited with code {process.ExitCode}, and its output had not ended within {deadline}: "
                + "a process it started still holds its stdout or stderr.");
        try { process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { /* it exited between the deadline and the kill */ }
        throw new TimeoutException($"{command} did not finish within {deadline}, and was killed with every process it started.");
    }
}
