using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// All that is AppKit's alone: loading the framework, which registers NSApplication and — through
/// the QuartzCore it links — CAMetalLayer. Everything else a window sends is the ordinary ObjC
/// runtime, shared with every other Apple shell.
/// </summary>
internal static partial class AppKit
{
    private const string AppKitFramework = "/System/Library/Frameworks/AppKit.framework/AppKit";

    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dlopen", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr dlopen(string path, int mode);

    internal static void LoadFrameworks() => dlopen(AppKitFramework, 2 /* RTLD_NOW */);
}
