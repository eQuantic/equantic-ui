using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// <c>[NSWorkspace sharedWorkspace]</c>, reached ONE way by the two capabilities that hand something
/// to the system, <see cref="MacWorkspace"/> and <see cref="MacUrlLauncher"/>.
/// </summary>
internal static class SharedWorkspace
{
    /// <summary>
    /// The shared workspace, with AppKit LOADED first. Nothing here can assume the runner already
    /// did it: a capability is resolved from the container and the container is built before the
    /// window, and a test or a tool may hold one with no window at all. Without the dlopen,
    /// <c>AppKit.Class("NSWorkspace")</c> answers nil and every method returns false, which looks
    /// exactly like "the system declined" and is not. Found by a probe that revealed /Applications
    /// and was told no.
    /// </summary>
    internal static IntPtr Get() => Send(AppKit.Class("NSWorkspace"), Sel("sharedWorkspace"));
}
