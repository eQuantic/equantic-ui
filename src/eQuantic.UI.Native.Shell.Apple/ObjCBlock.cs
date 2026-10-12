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
/// where the handle keeping the context alive already is.
/// </para>
/// <para>
/// The function a block calls is a STATIC <see cref="UnmanagedCallersOnlyAttribute"/> entry point, never a
/// delegate (#795). A device build is compiled ahead of time, and there a native caller can only
/// reach a method the compiler saw as an entry point: a delegate to an instance method or a closure
/// has no wrapper prepared, and the first callback fails. So the entry point is static, and it finds
/// the object it answers for in the block's own storage: <see cref="Context{T}"/> reads the
/// <see cref="GCHandle"/> the block carries after its descriptor, where a compiled block keeps what it
/// captured. The same code is right under the JIT, Mono AOT and NativeAOT.
/// </para>
/// </summary>
public sealed unsafe class ObjCBlock : IDisposable
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
        // What the block "captured": a handle to the object its entry point answers for. A block's
        // captures follow its descriptor, and the descriptor's size counts them.
        public IntPtr Context;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Descriptor
    {
        public nuint Reserved;
        public nuint Size;
    }

    private GCHandle _context;
    private IntPtr _block;
    private IntPtr _descriptor;

    /// <summary>
    /// A block that calls <paramref name="invoke"/>, carrying <paramref name="context"/> for it.
    /// </summary>
    /// <param name="invoke">
    /// The address of a static <see cref="UnmanagedCallersOnlyAttribute"/> method whose first parameter is the
    /// block itself, then the block's own parameters: <c>(IntPtr)(delegate* unmanaged&lt;IntPtr, …&gt;)&amp;Entry</c>.
    /// The compiler refuses that expression for a method without the attribute.
    /// </param>
    /// <param name="context">
    /// The object the entry point answers for, kept alive by this block until it is disposed: a
    /// block that outlives its context is a crash that arrives minutes later, in another thread,
    /// with nothing pointing back here.
    /// </param>
    public ObjCBlock(IntPtr invoke, object context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (invoke == IntPtr.Zero) throw new ArgumentException("A block needs an entry point to call.", nameof(invoke));
        _context = GCHandle.Alloc(context);

        var descriptor = new Descriptor { Reserved = 0, Size = (nuint)Marshal.SizeOf<Layout>() };
        _descriptor = Marshal.AllocHGlobal(Marshal.SizeOf<Descriptor>());
        Marshal.StructureToPtr(descriptor, _descriptor, false);

        var layout = new Layout
        {
            Isa = ConcreteGlobalBlock(),
            Flags = BlockIsGlobal,
            Reserved = 0,
            Invoke = invoke,
            Descriptor = _descriptor,
            Context = GCHandle.ToIntPtr(_context),
        };
        _block = Marshal.AllocHGlobal(Marshal.SizeOf<Layout>());
        Marshal.StructureToPtr(layout, _block, false);
    }

    /// <summary>What gets passed where a framework expects a block.</summary>
    public IntPtr Handle => _block;

    /// <summary>
    /// The object a block was made for, read by its entry point from the block it was called with;
    /// null once <see cref="ReleaseContext"/> let it go. Never throws: it runs inside an
    /// <see cref="UnmanagedCallersOnlyAttribute"/> method, where an exception ends the process.
    /// </summary>
    public static T? Context<T>(IntPtr block) where T : class
    {
        if (block == IntPtr.Zero) return null;
        var handle = ((Layout*)block)->Context;
        return handle == IntPtr.Zero ? null : GCHandle.FromIntPtr(handle).Target as T;
    }

    /// <summary>
    /// Lets the context go and keeps the block: what a reply that answers ONCE calls from inside its
    /// own entry point. The framework that called it still holds the block, and releases it after
    /// the call returns, reading the block's flags as it does; freeing the block here would hand it
    /// freed memory. So the block's two small allocations stay for the life of the process, the price
    /// of an ask, and everything the context reaches is free to go. A later call finds no context.
    /// </summary>
    public void ReleaseContext()
    {
        if (_block != IntPtr.Zero) ((Layout*)_block)->Context = IntPtr.Zero;
        if (_context.IsAllocated) _context.Free();
    }

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

    /// <summary>
    /// Frees the block. Only once nothing native holds it any more: a borrowed block after the call
    /// that borrowed it, never a block a framework copied and will release later.
    /// </summary>
    public void Dispose()
    {
        if (_block != IntPtr.Zero) { Marshal.FreeHGlobal(_block); _block = IntPtr.Zero; }
        if (_descriptor != IntPtr.Zero) { Marshal.FreeHGlobal(_descriptor); _descriptor = IntPtr.Zero; }
        if (_context.IsAllocated) _context.Free();
    }
}
