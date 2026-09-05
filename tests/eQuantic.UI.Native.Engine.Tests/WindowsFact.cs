using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A fact that needs the machine to BE Windows — user32, DirectWrite, WIC, the registry, DPAPI. On
/// any other OS it skips with a reason instead of dying on DllNotFound: CI runs the suite on Linux
/// and macOS, and a red build that means "wrong OS" hides every real failure.
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Requires Windows (user32/DirectWrite/WIC/registry/DPAPI).";
    }
}

/// <summary>The theory twin of <see cref="WindowsFactAttribute"/>.</summary>
public sealed class WindowsTheoryAttribute : TheoryAttribute
{
    public WindowsTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Requires Windows (user32/DirectWrite/WIC/registry/DPAPI).";
    }
}
