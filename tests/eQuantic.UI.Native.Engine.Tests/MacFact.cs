using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A fact that needs the machine to BE a Mac — CoreFoundation, CoreText, the ObjC runtime, the
/// device capabilities. On any other OS it skips with a reason instead of dying on DllNotFound:
/// CI runs the suite on Linux, and a red build that means "wrong OS" hides every real failure.
/// </summary>
public sealed class MacFactAttribute : FactAttribute
{
    public MacFactAttribute()
    {
        if (!OperatingSystem.IsMacOS())
            Skip = "Requires macOS (Apple frameworks: CoreFoundation/CoreText/ObjC runtime).";
    }
}

/// <summary>The theory twin of <see cref="MacFactAttribute"/>.</summary>
public sealed class MacTheoryAttribute : TheoryAttribute
{
    public MacTheoryAttribute()
    {
        if (!OperatingSystem.IsMacOS())
            Skip = "Requires macOS (Apple frameworks: CoreFoundation/CoreText/ObjC runtime).";
    }
}
