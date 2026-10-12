using System.Collections;
using Microsoft.Build.Framework;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// The build engine a task runs against outside MSBuild: it keeps every error, warning and message the
/// task logs, so a case reads what a build would have shown.
/// </summary>
internal sealed class RecordingBuildEngine : IBuildEngine
{
    /// <summary>The errors logged, as their code and text.</summary>
    public List<(string? Code, string Message)> Errors { get; } = [];

    /// <summary>The warnings logged.</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>Every message logged, the command lines included, at any importance.</summary>
    public List<string> Messages { get; } = [];

    /// <inheritdoc />
    public bool ContinueOnError => false;

    /// <inheritdoc />
    public int LineNumberOfTaskNode => 0;

    /// <inheritdoc />
    public int ColumnNumberOfTaskNode => 0;

    /// <inheritdoc />
    public string ProjectFileOfTaskNode => "probe.proj";

    /// <inheritdoc />
    public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add((e.Code, e.Message ?? ""));

    /// <inheritdoc />
    public void LogWarningEvent(BuildWarningEventArgs e) => Warnings.Add(e.Message ?? "");

    /// <inheritdoc />
    public void LogMessageEvent(BuildMessageEventArgs e) => Messages.Add(e.Message ?? "");

    /// <inheritdoc />
    public void LogCustomEvent(CustomBuildEventArgs e) => Messages.Add(e.Message ?? "");

    /// <inheritdoc />
    public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) =>
        throw new NotSupportedException("A task under test builds no project.");
}
