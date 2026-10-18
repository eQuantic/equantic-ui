using System.Diagnostics;

namespace eQuantic.UI.Tests;

/// <summary>
/// The one way a test starts a program and reads what it wrote: its arguments go to it as a list,
/// never through a shell, and both of its streams drain from the start, so a child that fills the
/// pipe nobody is reading never stops on it.
/// <para>
/// Linked into each test project that starts a program.
/// </para>
/// </summary>
internal static class ChildProcess
{
    /// <summary>
    /// Runs <paramref name="program"/> with <paramref name="arguments"/> in
    /// <paramref name="workingDirectory"/>, this process's own when it is null, with
    /// <paramref name="environment"/> over this process's own, a null value removing the variable it
    /// names, and answers its exit code and what it wrote to each stream. A program that has not
    /// exited by <paramref name="deadline"/> is killed with every process it started, and the run
    /// fails as a <see cref="TimeoutException"/> naming the command.
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
        using var expiry = new CancellationTokenSource(deadline);
        try
        {
            process.WaitForExitAsync(expiry.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* it exited between the deadline and the kill */ }
            throw new TimeoutException($"{command} did not finish within {deadline}.");
        }
        return (process.ExitCode, output.Result, error.Result);
    }
}
