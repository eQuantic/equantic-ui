using System.Runtime.InteropServices;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// An Objective-C BLOCK, built by hand so a shell with no bindings can call the asynchronous half
/// of the Apple frameworks — which is most of the interesting half: authenticating, loading,
/// anything that finishes later.
/// <para>
/// A block is a struct with a known layout: an isa pointer naming its kind, flags, a function
/// pointer, and a descriptor. Getting the layout wrong does not fail politely — it jumps to
/// whatever the fourth field happened to be — so it is written once, here, rather than at each
/// call site.
/// </para>
/// <para>
/// It declares itself GLOBAL, and that is the whole trick. A framework that will call back later
/// does not borrow a block, it OWNS one: it copies on the way in and releases when done. A block
/// that says it lives on the stack invites the runtime to copy it to the heap and free it — with
/// memory this class allocated and still intends to free itself. A global block is immortal by
/// contract: copy hands back the same pointer, release does nothing, and the lifetime stays here,
/// where the handle keeping the delegate alive already is.
/// </para>
/// </summary>
public sealed class ObjCBlock : IDisposable
{
    /// <summary>Look a symbol up in everything already loaded, rather than naming the library that
    /// exports it — the blocks runtime has moved between libSystem's sub-libraries before.</summary>
    private static readonly IntPtr AnyLoadedLibrary = new(-2); // RTLD_DEFAULT

    /// <summary>BLOCK_IS_GLOBAL. BLOCK_HAS_COPY_DISPOSE is deliberately NOT set: this block owns
    /// nothing the runtime would need to copy, and claiming otherwise means a descriptor with two
    /// more function pointers that do not exist.</summary>
    private const int BlockIsGlobal = 1 << 28;

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
    public ObjCBlock(Delegate callback)
    {
        _callback = GCHandle.Alloc(callback);

        var descriptor = new Descriptor { Reserved = 0, Size = (nuint)Marshal.SizeOf<Layout>() };
        _descriptor = Marshal.AllocHGlobal(Marshal.SizeOf<Descriptor>());
        Marshal.StructureToPtr(descriptor, _descriptor, false);

        var layout = new Layout
        {
            Isa = ConcreteGlobalBlock(),
            Flags = BlockIsGlobal,
            Reserved = 0,
            Invoke = Marshal.GetFunctionPointerForDelegate(callback),
            Descriptor = _descriptor,
        };
        _block = Marshal.AllocHGlobal(Marshal.SizeOf<Layout>());
        Marshal.StructureToPtr(layout, _block, false);
    }

    /// <summary>What gets passed where a framework expects a block.</summary>
    public IntPtr Handle => _block;

    private static IntPtr ConcreteGlobalBlock()
    {
        var isa = dlsym(AnyLoadedLibrary, "_NSConcreteGlobalBlock");
        // A null isa is a block the runtime will follow into nothing, later, on another thread.
        // Better to say so here, where the stack still names the cause.
        if (isa == IntPtr.Zero) throw new InvalidOperationException("_NSConcreteGlobalBlock not found");
        return isa;
    }

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);

    public void Dispose()
    {
        if (_block != IntPtr.Zero) { Marshal.FreeHGlobal(_block); _block = IntPtr.Zero; }
        if (_descriptor != IntPtr.Zero) { Marshal.FreeHGlobal(_descriptor); _descriptor = IntPtr.Zero; }
        if (_callback.IsAllocated) _callback.Free();
    }
}
