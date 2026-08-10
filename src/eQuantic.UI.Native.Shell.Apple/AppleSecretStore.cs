using System.Runtime.InteropServices;
using System.Text;
using eQuantic.UI.Primitives;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// The Keychain, through the three calls a token needs — macOS and iOS both, because
/// <c>SecItem*</c> is one API on both.
/// <para>
/// One <c>kSecClassGenericPassword</c> item per key, filed under the app's own bundle identifier,
/// so the system encrypts it at rest, keeps it out of other apps' reach, and drops it when the app
/// is removed. This is what <see cref="ISecretStore"/> exists for, and what the browser has no
/// equivalent of.
/// </para>
/// <para>
/// The <c>kSec*</c> keys are CFString CONSTANTS exported by Security.framework, not literals —
/// read through dlsym exactly as the camera reads CoreVideo's. Hard-coding their values ("genp",
/// "acct", …) is a widespread shortcut and a bet on undocumented internals; the symbol is the
/// contract Apple actually publishes.
/// </para>
/// <para>
/// <c>AfterFirstUnlockThisDeviceOnly</c>, deliberately: a token has to be readable when the app
/// wakes for a background refresh, which <c>WhenUnlocked</c> forbids, while staying unreadable on
/// a device not unlocked since boot — and <c>ThisDeviceOnly</c> keeps it out of backups, because a
/// credential minted for this install belongs to this install.
/// </para>
/// <para>
/// Every failure answers rather than throws. A Keychain call fails for reasons an app cannot act
/// on — a locked device, an item dropped by a restore — and the contract already says a null read
/// means "sign in again", which is the only useful response to any of them.
/// </para>
/// </summary>
public sealed class AppleSecretStore : ISecretStore
{
    private const string SecurityFramework = "/System/Library/Frameworks/Security.framework/Security";
    private const int Success = 0;

    [DllImport(SecurityFramework)]
    private static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);

    [DllImport(SecurityFramework)]
    private static extern int SecItemAdd(IntPtr attributes, IntPtr result);

    [DllImport(SecurityFramework)]
    private static extern int SecItemDelete(IntPtr query);

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, long length);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDataGetBytePtr(IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern long CFDataGetLength(IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr value);

    /// <summary>An exported CFString constant, dereferenced — IntPtr.Zero when the symbol is not
    /// there, which is a platform without the Keychain rather than a mistake to throw about.</summary>
    private static IntPtr Constant(string symbol)
    {
        dlopen(SecurityFramework, 2 /* RTLD_NOW */);
        var address = dlsym(new IntPtr(-2) /* RTLD_DEFAULT */, symbol);
        return address == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(address);
    }

    private static readonly IntPtr SecClass = Constant("kSecClass");
    private static readonly IntPtr GenericPassword = Constant("kSecClassGenericPassword");
    private static readonly IntPtr AttrService = Constant("kSecAttrService");
    private static readonly IntPtr AttrAccount = Constant("kSecAttrAccount");
    private static readonly IntPtr ValueData = Constant("kSecValueData");
    private static readonly IntPtr ReturnData = Constant("kSecReturnData");
    private static readonly IntPtr AttrAccessible = Constant("kSecAttrAccessible");
    private static readonly IntPtr AccessibleAfterFirstUnlock =
        Constant("kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly");

    /// <summary>Whether this host actually has the framework — a symbol that did not resolve means
    /// every call below would fail anyway, and answering null is the honest report.</summary>
    private static bool Available => SecClass != IntPtr.Zero && GenericPassword != IntPtr.Zero;

    /// <summary>The service every item is filed under: the app's own bundle identifier, so two apps
    /// on one device never read each other's.</summary>
    private static string Service()
    {
        var bundle = Send(objc_getClass("NSBundle"), Sel("mainBundle"));
        return FromNSString(Send(bundle, Sel("bundleIdentifier"))) ?? "eQuantic.UI";
    }

    /// <summary>
    /// The query dictionary. NSMutableDictionary rather than CFDictionary because the two are
    /// toll-free bridged and the ObjC path is the one this shell already speaks.
    /// </summary>
    private static IntPtr Query(string key, bool returningData)
    {
        var query = Send(Send(objc_getClass("NSMutableDictionary"), Sel("alloc")), Sel("init"));
        void Put(IntPtr value, IntPtr attribute) =>
            Send(query, Sel("setObject:forKey:"), value, attribute);

        Put(GenericPassword, SecClass);
        Put(NSString(Service()), AttrService);
        Put(NSString(key), AttrAccount);
        if (returningData)
            Put(Send(objc_getClass("NSNumber"), Sel("numberWithBool:"), (long)1), ReturnData);
        return query;
    }

    public string? Get(string key)
    {
        if (!Available) return null;
        if (SecItemCopyMatching(Query(key, returningData: true), out var data) != Success) return null;
        if (data == IntPtr.Zero) return null;

        try
        {
            var length = CFDataGetLength(data);
            if (length <= 0) return null;
            var bytes = new byte[length];
            Marshal.Copy(CFDataGetBytePtr(data), bytes, 0, (int)length);
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            // SecItemCopyMatching hands back a +1 reference — the caller owns it.
            CFRelease(data);
        }
    }

    public void Set(string key, string value)
    {
        if (!Available) return;

        // Add does not replace, so the old item goes first. Deleting what is not there is not an
        // error here — it is the ordinary path on a first write.
        Remove(key);

        var attributes = Query(key, returningData: false);
        var bytes = Encoding.UTF8.GetBytes(value);
        var data = CFDataCreate(IntPtr.Zero, bytes, bytes.Length);
        Send(attributes, Sel("setObject:forKey:"), data, ValueData);
        if (AccessibleAfterFirstUnlock != IntPtr.Zero)
            Send(attributes, Sel("setObject:forKey:"), AccessibleAfterFirstUnlock, AttrAccessible);

        SecItemAdd(attributes, IntPtr.Zero);
        CFRelease(data);
    }

    /// <summary>What a sign-out calls.</summary>
    public void Remove(string key)
    {
        if (!Available) return;
        SecItemDelete(Query(key, returningData: false));
    }
}
