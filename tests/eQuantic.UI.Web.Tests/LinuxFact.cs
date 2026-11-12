namespace eQuantic.UI.Web.Tests;

/// <summary>
/// A fact about a rule only Linux enforces: a program open for writing anywhere is refused when it is
/// started (ETXTBSY), which macOS and Windows never answer. Elsewhere it skips with the reason, so a
/// green run there never reads as the rule proved.
/// </summary>
public sealed class LinuxFactAttribute : FactAttribute
{
    public LinuxFactAttribute()
    {
        if (!OperatingSystem.IsLinux())
            Skip = "Only Linux refuses to start a program that is open for writing (ETXTBSY).";
    }
}
