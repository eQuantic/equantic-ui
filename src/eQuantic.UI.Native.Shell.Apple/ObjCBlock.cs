using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// An Objective-C BLOCK, built by hand so a shell with no bindings can call the asynchronous half
/// of the Apple frameworks — which is most of the interesting half: authenticating, loading,
/// anything that finishes later.
/// <para>
/// A block is a struct with a known layout: an isa pointer naming its kind, flags, a function
/// pointer, and a descriptor. The runtime exports <c>_NSConcreteStackBlock</c> as the isa for one
/// built on the stack; this one lives on the heap for as long as the call needs it, which is what
/// the pinned handle is for. Getting the layout wrong does not fail politely — it jumps to whatever
/// the fourth field happened to be — so it is written once, here, rather than at each call site.
/// </para>
/// </summary>
internal sealed class ObjCBlock : IDisposable
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";

    /// <summary>BLOCK_HAS_COPY_DISPOSE is deliberately NOT set: this block owns nothing the runtime
    /// would need to copy, and claiming otherwise means a descriptor with two more function
    /// pointers that do not exist.</summary>
    private const int BlockFlagsNone = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct Layout
    {
        public IntPtr Isa;
        public int Flags;
        public int Reserved;
        public IntPtr Invoke;
        public IntPtr Descriptor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Descriptor
    {
        public nuint Reserved;
        public nuint Size;
    }

    private readonly GCHandle _callback;
    private IntPtr _block;
    private IntPtr _descriptor;

    /// <summary>
    /// Wraps a managed delegate. The delegate MUST be kept alive by the caller for as long as the
    /// framework might call it — a block whose function pointer outlives its delegate is a crash
    /// that arrives minutes later, in another thread, with nothing pointing back here.
    /// </summary>
    internal ObjCBlock(Delegate callback)
    {
        _callback = GCHandle.Alloc(callback);

        var descriptor = new Descriptor { Reserved = 0, Size = (nuint)Marshal.SizeOf<Layout>() };
        _descriptor = Marshal.AllocHGlobal(Marshal.SizeOf<Descriptor>());
        Marshal.StructureToPtr(descriptor, _descriptor, false);

        var layout = new Layout
        {
            Isa = ConcreteStackBlock(),
            Flags = BlockFlagsNone,
            Reserved = 0,
            Invoke = Marshal.GetFunctionPointerForDelegate(callback),
            Descriptor = _descriptor,
        };
        _block = Marshal.AllocHGlobal(Marshal.SizeOf<Layout>());
        Marshal.StructureToPtr(layout, _block, false);
    }

    /// <summary>What gets passed where a framework expects a block.</summary>
    internal IntPtr Handle => _block;

    private static IntPtr ConcreteStackBlock()
    {
        var handle = dlopen(ObjCLib, 2 /* RTLD_NOW */);
        return dlsym(handle, "_NSConcreteStackBlock");
    }

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);

    public void Dispose()
    {
        if (_block != IntPtr.Zero) { Marshal.FreeHGlobal(_block); _block = IntPtr.Zero; }
        if (_descriptor != IntPtr.Zero) { Marshal.FreeHGlobal(_descriptor); _descriptor = IntPtr.Zero; }
        if (_callback.IsAllocated) _callback.Free();
    }
}
