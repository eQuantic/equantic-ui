namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A fact that needs a POSIX shell: its stand-in programs are shell scripts on the PATH, which is how a
/// case puts a recorder where the SDK looks for <c>dotnet</c>, <c>codesign</c> or <c>git</c>, or it
/// starts <c>/bin/sh</c> itself. Windows has neither, so there it skips with the reason, and the task's
/// own cases, which start a real .NET program, run on every system.
/// </summary>
public sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute()
    {
        if (OperatingSystem.IsWindows())
            Skip = "The case runs a shell script, a stand-in program on the PATH or /bin/sh itself, and Windows starts neither.";
    }
}
