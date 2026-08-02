using System.Runtime.InteropServices;
using Android.Runtime;
using Android.Views;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// The native window behind a Java <c>Surface</c> — what Vulkan needs to make a surface of its own.
/// libandroid is the platform's, like Metal is on Apple: no third party crosses this line either.
/// </summary>
internal static partial class AndroidWindow
{
    [LibraryImport("android", EntryPoint = "ANativeWindow_fromSurface")]
    private static partial IntPtr FromSurface(IntPtr env, IntPtr surface);

    [LibraryImport("android", EntryPoint = "ANativeWindow_release")]
    internal static partial void Release(IntPtr window);

    /// <summary>
    /// The JNI environment of the calling thread is what turns a managed Surface into the handle
    /// the NDK understands — and this only ever runs on the UI thread, which is where the surface
    /// callbacks arrive.
    /// </summary>
    internal static IntPtr FromSurface(Surface surface) =>
        FromSurface(JNIEnv.Handle, surface.Handle);
}
