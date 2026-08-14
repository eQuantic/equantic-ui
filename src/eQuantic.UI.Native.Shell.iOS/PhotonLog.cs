using System.Runtime.InteropServices;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>
/// Where a diagnostic goes on a phone. An iOS app has no terminal behind it — whatever it writes to
/// stdout is discarded — so the shell's own messages go to the SYSTEM log instead, which is what
/// Console.app reads on a device and <c>xcrun simctl spawn booted log stream</c> reads on a
/// simulator. The macOS shell prints to the console because there IS one; this is the same evidence
/// through the only channel this platform has.
/// </summary>
internal static partial class PhotonLog
{
    /// <summary>
    /// NSLog is variadic, and only its FIXED argument is passed here — which keeps this an ordinary
    /// call, rather than one that has to honour the platform's rules for where the variable part
    /// lives. The message is escaped for the same reason: a stray percent in a control's label
    /// would otherwise be read as a placeholder and print whatever happened to be in a register.
    /// </summary>
    [LibraryImport("/System/Library/Frameworks/Foundation.framework/Foundation", EntryPoint = "NSLog")]
    private static partial void NSLog(IntPtr format);

    internal static void Write(string message) => NSLog(NSString(message.Replace("%", "%%")));
}
