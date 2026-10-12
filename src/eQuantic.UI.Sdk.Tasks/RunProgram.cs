using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace eQuantic.UI.Sdk.Tasks;

/// <summary>
/// Starts a program with a list of arguments and waits for it, with no shell between the build and
/// the program. MSBuild's own <c>Exec</c> writes its command into a script that <c>/bin/sh</c> runs on
/// macOS and Linux and <c>cmd.exe</c> on Windows, and each reads the command before the program does:
/// a <c>$</c> or a backtick in a path was expanded or run, and so was a <c>%</c> on Windows. Measured
/// with the package folder under a folder named <c>nuget $b</c>, a build stopped at the first program
/// it started (#841). Here each argument reaches the program as it was written.
/// </summary>
/// <remarks>
/// What the program writes is logged as <c>Exec</c> logs it: a line in the canonical
/// <c>file(line,col): error CODE: text</c> shape is an error or a warning of the build, and every
/// other line a message, at high importance unless the call says otherwise.
/// </remarks>
public sealed class RunProgram : ToolTask
{
    private readonly List<ITaskItem> _console = [];

    /// <summary>The program: a file name the PATH finds, such as <c>dotnet</c> or <c>codesign</c>, or a path.</summary>
    [Required]
    public string Program { get; set; } = "";

    /// <summary>
    /// What the program is told, in order. Each item says one thing: its <c>Value</c> metadata, after
    /// its <c>Option</c> metadata when it names one, so <c>Option="--sign" Value="$(Identity)"</c> is two
    /// arguments and <c>Value="--force"</c> one. A value is passed even when it is empty, as a
    /// quoted <c>"$(X)"</c> was. The item's own name is a label for the reader, and never passed:
    /// what a project's property holds goes in metadata, where MSBuild neither splits it at a
    /// semicolon nor expands a wildcard in it.
    /// </summary>
    public ITaskItem[] Arguments { get; set; } = [];

    /// <summary>The folder the program runs in; the project's when it is not set.</summary>
    public string WorkingDirectory { get; set; } = "";

    /// <summary>
    /// Whether the step succeeds whatever the program answers, and when the program is not on this
    /// machine at all, which is what an <c>Exec</c> ignoring its exit code tolerated: its shell answered
    /// 127 for a program it could not find.
    /// </summary>
    public bool IgnoreExitCode { get; set; }

    /// <summary>Whether <see cref="ConsoleOutput"/> collects the lines the program writes.</summary>
    public bool ConsoleToMSBuild { get; set; }

    /// <summary>The lines the program wrote, one item each, when <see cref="ConsoleToMSBuild"/> is set.</summary>
    [Output]
    public ITaskItem[] ConsoleOutput => [.. _console];

    /// <inheritdoc />
    protected override string ToolName => Path.GetFileName(Program);

    /// <inheritdoc />
    protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;

    /// <inheritdoc />
    protected override MessageImportance StandardErrorLoggingImportance => MessageImportance.High;

    /// <inheritdoc />
    public override bool Execute()
    {
        // ToolTask can hand its command to cmd.exe or /bin/sh when asked to. This task exists so that
        // nothing does, whatever a call site sets.
        UseCommandProcessor = false;
        if (IgnoreExitCode && !IsOnThisMachine(Program))
        {
            Log.LogMessage(MessageImportance.Low, "{0} is not on this machine, and this step does without it.", Program);
            return true;
        }
        return base.Execute();
    }

    /// <inheritdoc />
    protected override string GenerateFullPathToTool() => Program;

    /// <inheritdoc />
    protected override string GenerateCommandLineCommands() => CommandLine.Of(Words());

    /// <summary>
    /// The command line as <see cref="CommandLine"/> wrote it. ToolTask's own turns every backslash on
    /// macOS and Linux into a slash, taking it for a Windows path, and a backslash an app's title or a
    /// folder's name holds is not one.
    /// </summary>
    protected override string AdjustCommandsForOperatingSystem(string input) => input;

    /// <inheritdoc />
    protected override string? GetWorkingDirectory() => WorkingDirectory.Length > 0 ? WorkingDirectory : null;

    /// <summary>
    /// The command line, unless <see cref="ToolTask.EchoOff"/> is set: one that carries a password
    /// says nothing of it. ToolTask logs it either way, and it is <c>Exec</c> that asks first.
    /// </summary>
    protected override void LogToolCommand(string message)
    {
        if (!EchoOff) base.LogToolCommand(message);
    }

    /// <inheritdoc />
    protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
    {
        if (ConsoleToMSBuild) _console.Add(new TaskItem(Escaped(singleLine), treatAsFilePath: false));
        base.LogEventsFromTextOutput(singleLine, messageImportance);
    }

    /// <inheritdoc />
    protected override bool HandleTaskExecutionErrors()
    {
        if (IgnoreExitCode)
        {
            Log.LogMessage(MessageImportance.Normal, "{0} ended with exit code {1}, which this step ignores.", Program, ExitCode);
            return true;
        }
        // A program that reported its own errors has said what went wrong; one that did not gets the
        // command it was started with, which is what a reader needs to run it again, unless its
        // command line is not to be echoed.
        if (!Log.HasLoggedErrors)
        {
            var arguments = EchoOff ? "" : GenerateCommandLineCommands();
            Log.LogError(null, "EQ4004", null, null, 0, 0, 0, 0, "{0} ended with exit code {1}.",
                arguments.Length > 0 ? $"{Program} {arguments}" : Program, ExitCode);
        }
        return false;
    }

    /// <summary>The arguments the items say, in order: see <see cref="Arguments"/>.</summary>
    private IEnumerable<string> Words()
    {
        foreach (var item in Arguments)
        {
            var option = item.GetMetadata("Option");
            if (option.Length > 0) yield return option;
            yield return item.GetMetadata("Value");
        }
    }

    /// <summary>
    /// <paramref name="text"/> as an item's name holds it: MSBuild reads a percent sign, a wildcard, a
    /// semicolon and the characters of its own syntax in a name as escapes, and gives the text back
    /// when the item is read.
    /// </summary>
    private static string Escaped(string text)
    {
        var escaped = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            if ("%*?@$();'".IndexOf(c) >= 0) escaped.Append('%').Append(((int)c).ToString("X2"));
            else escaped.Append(c);
        }
        return escaped.ToString();
    }

    /// <summary>Whether <paramref name="program"/> names a file, directly or through the PATH.</summary>
    private static bool IsOnThisMachine(string program)
    {
        if (Path.GetFileName(program).Length != program.Length) return File.Exists(program);
        var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var names = windows && Path.GetExtension(program).Length == 0 ? new[] { program, program + ".exe" } : new[] { program };
        return (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator)
            .Select(directory => directory.Trim('"'))
            .Where(directory => directory.Length > 0)
            .Any(directory => names.Any(name => File.Exists(Path.Combine(directory, name))));
    }
}
