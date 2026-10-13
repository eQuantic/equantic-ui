using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>
/// The links this app is opened with, as iOS hands them to a scene: the URL contexts of its
/// connection, and <c>scene:openURLContexts:</c> while it runs.
/// <para>
/// ONE relay for the process, as on the desktop shells and for the same reason: the link arrives
/// with the scene, before the app's first tree exists, and whoever asks the container for
/// <see cref="IDeepLinks"/> later must be answered by the one that was told.
/// </para>
/// </summary>
internal static class IosDeepLinks
{
    internal static DeepLinkRelay Relay { get; } = new();
}
