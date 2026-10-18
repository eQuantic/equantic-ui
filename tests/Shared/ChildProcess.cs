using System.Diagnostics;
using System.Text;

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
/// Linked into each test project that starts a program, and every process a test starts goes through
/// it: <see cref="Run"/> for one whose end the test waits for, <see cref="Dotnet"/> for <c>dotnet</c>,
/// which also starts it so that nothing it starts outlives it, and <see cref="Started"/> for one the
/// code under test owns. <c>ChildProcessTests</c> fails on one started anywhere else.
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
        IReadOnlyList<string> arguments,
        TimeSpan deadline,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null) =>
        Run("dotnet", arguments, deadline, workingDirectory,
            [.. OwnBuild, .. environment?.Select(setting => KeyValuePair.Create(setting.Key, (string?)setting.Value)) ?? []]);

    /// <summary>
    /// Runs <paramref name="program"/> with <paramref name="arguments"/> in
    /// <paramref name="workingDirectory"/>, this process's own when it is null, with
    /// <paramref name="environment"/> over this process's own, in order, a null value removing the
    /// variable it names, and answers its exit code and what it wrote to each stream, read as
    /// <paramref name="encoding"/> when one is given. A program still running at
    /// <paramref name="deadline"/> is killed with every process it started; one that exited while a
    /// process it left behind still holds its output is left to it. Either way the run fails as a
    /// <see cref="TimeoutException"/> naming the command.
    /// <para>
    /// The conformance suite runs a program through here thousands of times, so the run costs what
    /// starting a process costs and the deadline nothing more: blocking waits on one clock read as a
    /// timestamp, and the command written out only for the failure that names it.
    /// </para>
    /// </summary>
    public static (int ExitCode, string Output, string Error) Run(
        string program,
        IReadOnlyList<string> arguments,
        TimeSpan deadline,
        string? workingDirectory = null,
        IEnumerable<KeyValuePair<string, string?>>? environment = null,
        Encoding? encoding = null)
    {
        var started = Stopwatch.GetTimestamp();
        using var process = Started(program, arguments, workingDirectory, environment, encoding);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(deadline))
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* it exited between the deadline and the kill */ }
            throw new TimeoutException($"{Command(process.StartInfo)} did not finish within {deadline}, and was killed with every process it started.");
        }
        if (!output.Wait(Left()) || !error.Wait(Left()))
            throw new TimeoutException(
                $"{Command(process.StartInfo)} exited with code {process.ExitCode}, and its output had not ended within {deadline}: "
                + "a process it started still holds its stdout or stderr.");
        return (process.ExitCode, output.Result, error.Result);

        TimeSpan Left() => deadline - Stopwatch.GetElapsedTime(started) is var left && left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    /// <summary>
    /// <paramref name="program"/> started as <see cref="Run"/> starts one, both streams redirected,
    /// and handed back running, for the code under test that owns its run: a test that waits for a
    /// program's end and reads what it wrote goes through <see cref="Run"/>, which holds both to the
    /// deadline. Whoever takes it waits for it, reads it, and stops it.
    /// </summary>
    public static Process Started(
        string program,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IEnumerable<KeyValuePair<string, string?>>? environment = null,
        Encoding? encoding = null)
    {
        var info = new ProcessStartInfo(program)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding = encoding,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? string.Empty,
        };
        // By index, and the environment only when there is one: enumerating either through its
        // interface would allocate on every run of the conformance suite's hot path.
        for (var i = 0; i < arguments.Count; i++) info.ArgumentList.Add(arguments[i]);
        if (environment is not null)
            foreach (var (name, value) in environment)
            {
                if (value is null) info.Environment.Remove(name);
                else info.Environment[name] = value;
            }
        return Process.Start(info)!;
    }

    private static string Command(ProcessStartInfo info) => string.Join(' ', [info.FileName, .. info.ArgumentList]);
}
