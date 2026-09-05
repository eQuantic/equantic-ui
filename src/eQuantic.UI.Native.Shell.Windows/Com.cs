using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Shell.Windows;

/// <summary>
/// COM by hand: a vtable is an array of function pointers, an interface pointer points at a pointer
/// to it, and a method is a slot. Calling through the slot with a typed <c>delegate* unmanaged</c>
/// is the Windows twin of <c>objc_msgSend</c> with a selector — no <c>ComImport</c>, no runtime
/// marshalling stubs, nothing the trimmer or the AOT compiler has to be talked into keeping.
/// <para>
/// The slot numbers are spelled beside each call in the wrappers (<c>DWrite</c>, <c>D2D</c>,
/// <c>Wic</c>, the dialogs), copied from the SDK headers in declaration order — IUnknown's three
/// first, then each base interface's, then the interface's own. A wrong slot is not an error the
/// compiler can see; it is a call into the wrong function, so the wrappers are the one place they
/// live and the tests exercise every one of them against the real system.
/// </para>
/// </summary>
internal static unsafe partial class Com
{
    private const string Ole32 = "ole32.dll";

    public const int S_OK = 0;

    /// <summary>HRESULT_FROM_WIN32(ERROR_CANCELLED): the person closed the dialog.</summary>
    public const int E_CANCELLED = unchecked((int)0x800704C7);

    /// <summary>The thread was already initialised in the other apartment model — not a failure
    /// for anything this shell does, which works under either.</summary>
    public const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);

    public const uint COINIT_APARTMENTTHREADED = 0x2;
    public const uint COINIT_DISABLE_OLE1DDE = 0x4;
    public const uint CLSCTX_INPROC_SERVER = 0x1;

    [LibraryImport(Ole32)]
    public static partial int CoInitializeEx(IntPtr reserved, uint flags);

    [LibraryImport(Ole32)]
    public static partial int CoCreateInstance(Guid* clsid, IntPtr outer, uint context, Guid* iid, void** instance);

    [LibraryImport(Ole32)]
    public static partial void CoTaskMemFree(IntPtr memory);

    /// <summary>
    /// Joins the calling thread to COM as a single-threaded apartment — what the shell's dialogs
    /// and the imaging stack expect. Idempotent, and indifferent to a thread that already chose the
    /// other model: WIC and DirectWrite work in both, and the dialogs only ever run on the window's
    /// thread, which this shell initialises first.
    /// </summary>
    public static void EnsureInitialized()
    {
        var hr = CoInitializeEx(IntPtr.Zero, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
        if (hr < 0 && hr != RPC_E_CHANGED_MODE) Check(hr, "COM initialisation");
    }

    public static void Check(int hr, string operation)
    {
        if (hr < 0)
            throw new InvalidOperationException($"{operation} failed: HRESULT 0x{hr:X8}.");
    }

    /// <summary>The function at <paramref name="slot"/> of <paramref name="instance"/>'s vtable.</summary>
    public static void* Method(void* instance, int slot) => (*(void***)instance)[slot];

    public static uint Release(void* instance) =>
        instance is null ? 0 : ((delegate* unmanaged<void*, uint>)Method(instance, 2))(instance);

    /// <summary>Releases and clears in one motion — the shape every <c>finally</c> here wants.</summary>
    public static void Release(ref void* instance)
    {
        if (instance is null) return;
        Release(instance);
        instance = null;
    }

    public static void* Create(Guid clsid, Guid iid)
    {
        void* instance;
        Check(CoCreateInstance(&clsid, IntPtr.Zero, CLSCTX_INPROC_SERVER, &iid, &instance), $"creating {clsid:B}");
        return instance;
    }
}
