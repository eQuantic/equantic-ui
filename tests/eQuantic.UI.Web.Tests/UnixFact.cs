namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A fact whose stand-in programs are shell scripts on the PATH, which is how a case puts a recorder
/// where the SDK looks for <c>dotnet</c>, <c>codesign</c> or <c>git</c>. Windows starts no script by a
/// bare name, so there it skips with the reason, and the task's own cases, which start a real .NET
/// program, run on every system.
/// </summary>
public sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute()
    {
        if (OperatingSystem.IsWindows())
            Skip = "The stand-in programs are shell scripts on the PATH, which Windows does not start by a bare name.";
    }
}
