using System.Runtime.InteropServices;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A fact that needs a specific FONT on the machine, not only the right OS. macOS guarantees an
/// operating system, never a font list: the Nerd Font cut this suite pins the alias case against is
/// something a developer installed, and on a clean Mac the test would go red saying "descriptor
/// matching is broken" when what actually happened is that the fixture is absent.
/// <para>
/// The availability probe deliberately asks a DIFFERENT question from the one under test.
/// <c>CTFontManagerCopyAvailableFontFamilyNames</c> enumerates what is installed;
/// <c>CTFontDescriptorCreateMatchingFontDescriptor</c> is the thing being pinned. Sharing one API
/// between the gate and the assertion would make a broken matcher SKIP its own test — the failure
/// mode that turns a suite into decoration.
/// </para>
/// </summary>
public sealed class MacFontFactAttribute : FactAttribute
{
    /// <param name="family">
    /// The CANONICAL family name, as a font list reports it — not the alias a test may ask for. The
    /// alias is the thing being measured, so the gate must not depend on it resolving.
    /// </param>
    public MacFontFactAttribute(string family)
    {
        if (!OperatingSystem.IsMacOS())
            Skip = "Requires macOS (Apple frameworks: CoreFoundation/CoreText/ObjC runtime).";
        else if (!InstalledFaces.Has(family))
            Skip = $"Requires the font \"{family}\", which is not installed on this machine.";
    }
}

/// <summary>What this machine actually has, read once from the font manager.</summary>
internal static class InstalledFaces
{
    private const string CoreTextLib = "/System/Library/Frameworks/CoreText.framework/CoreText";
    private const string CoreFoundationLib =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    // DllImport rather than LibraryImport: every signature here is blittable, and the generated
    // form would make the whole test project unsafe for three declarations.
    [DllImport(CoreTextLib)]
    private static extern IntPtr CTFontManagerCopyAvailableFontFamilyNames();

    [DllImport(CoreFoundationLib)]
    private static extern long CFArrayGetCount(IntPtr array);

    [DllImport(CoreFoundationLib)]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, long index);

    [DllImport(CoreFoundationLib)]
    private static extern void CFRelease(IntPtr value);

    private static readonly Lazy<HashSet<string>> Families = new(Read);

    internal static bool Has(string family) =>
        OperatingSystem.IsMacOS() && Families.Value.Contains(family);

    private static HashSet<string> Read()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var array = CTFontManagerCopyAvailableFontFamilyNames();
        if (array == IntPtr.Zero) return names;
        try
        {
            for (long i = 0; i < CFArrayGetCount(array); i++)
            {
                // CFString is toll-free bridged to NSString, so the shell's reader handles it.
                if (eQuantic.UI.Native.Shell.Apple.ObjC.FromNSString(CFArrayGetValueAtIndex(array, i))
                    is { Length: > 0 } name)
                    names.Add(name);
            }
        }
        finally { CFRelease(array); }
        return names;
    }
}
