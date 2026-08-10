using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// <c>NSUserDefaults</c> — the store macOS and iOS both mean by "the app's preferences", and the
/// one the system already backs up, migrates and deletes along with the app.
/// <para>
/// One realization for both platforms because it IS one API on both. No <c>synchronize</c> call:
/// it has been a no-op since 10.12/iOS 12, the system flushes on its own, and calling it is the
/// classic way to make a preference write look like it needs babysitting when it does not.
/// </para>
/// </summary>
public sealed class AppleAppStorage : IAppStorage
{
    private static IntPtr Defaults() =>
        Send(objc_getClass("NSUserDefaults"), Sel("standardUserDefaults"));

    public string? Get(string key) =>
        FromNSString(Send(Defaults(), Sel("stringForKey:"), NSString(key)));

    public void Set(string key, string value) =>
        Send(Defaults(), Sel("setObject:forKey:"), NSString(value), NSString(key));

    public void Remove(string key) =>
        Send(Defaults(), Sel("removeObjectForKey:"), NSString(key));
}
