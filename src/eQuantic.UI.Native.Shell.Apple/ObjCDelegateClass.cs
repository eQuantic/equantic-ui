using System.Numerics;
using System.Runtime.InteropServices;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.Apple;

/// <summary>
/// An Objective-C class defined at run time, for a framework that talks back through a DELEGATE —
/// an object with named methods — rather than a block: CoreLocation, AVFoundation's sample buffers,
/// the AppleEvent manager.
/// <para>
/// Its methods are static <see cref="UnmanagedCallersOnlyAttribute"/> entry points (#795): a device
/// build is compiled ahead of time, with no JIT to make a wrapper for a delegate. Each INSTANCE
/// carries its context, a <see cref="GCHandle"/> in an ivar, which a method reads back from the
/// <c>self</c> it is called with through <see cref="Context{T}"/> — the delegate object's twin of
/// the block's own storage in <see cref="ObjCBlock"/>. So two delegates of one class, two camera
/// sessions say, each answer for their own object rather than for whichever was opened last.
/// </para>
/// </summary>
public sealed unsafe partial class ObjCDelegateClass
{
    private const string ContextIvar = "eqContext";
    private readonly nint _offset;

    private ObjCDelegateClass(IntPtr handle, nint offset)
    {
        Handle = handle;
        _offset = offset;
    }

    /// <summary>The class, as the runtime knows it.</summary>
    public IntPtr Handle { get; }

    /// <summary>
    /// Defines a subclass of <c>NSObject</c> with the context ivar and these methods. A class name
    /// is the process's: define each once and keep it.
    /// </summary>
    /// <param name="name">The class's Objective-C name.</param>
    /// <param name="methods">
    /// Each method's selector, the address of its static <see cref="UnmanagedCallersOnlyAttribute"/>
    /// implementation (whose first two parameters are <c>self</c> and the selector), and its type
    /// encoding.
    /// </param>
    public static ObjCDelegateClass Define(string name, params (string Selector, IntPtr Implementation, string Types)[] methods)
    {
        var cls = objc_allocateClassPair(objc_getClass("NSObject"), name, 0);
        if (cls == IntPtr.Zero) throw new InvalidOperationException($"The class {name} is already defined in this process.");

        // A pointer, aligned as one: the alignment argument is its log2.
        if (!class_addIvar(cls, ContextIvar, (nuint)IntPtr.Size, (byte)BitOperations.Log2((uint)IntPtr.Size), "^v"))
            throw new InvalidOperationException($"The class {name} could not take its context ivar.");
        foreach (var (selector, implementation, types) in methods)
            class_addMethod(cls, sel_registerName(selector), implementation, types);
        objc_registerClassPair(cls);

        return new ObjCDelegateClass(cls, ivar_getOffset(class_getInstanceVariable(cls, ContextIvar)));
    }

    /// <summary>
    /// A new instance whose methods answer for <paramref name="context"/>. The handle stays the
    /// caller's: it decides how long the context lives, and whether the instance holds it strongly.
    /// </summary>
    public IntPtr New(GCHandle context)
    {
        var instance = Send(Send(Handle, Sel("alloc")), Sel("init"));
        *(IntPtr*)(instance + _offset) = GCHandle.ToIntPtr(context);
        return instance;
    }

    /// <summary>
    /// The context of an instance of this class, read by a method from its <c>self</c>; null when the
    /// instance carries none or its object is gone. Never throws: it runs inside an
    /// <see cref="UnmanagedCallersOnlyAttribute"/> method, where an exception ends the process.
    /// </summary>
    public T? Context<T>(IntPtr self) where T : class
    {
        if (self == IntPtr.Zero) return null;
        var handle = *(IntPtr*)(self + _offset);
        return handle == IntPtr.Zero ? null : GCHandle.FromIntPtr(handle).Target as T;
    }

    [LibraryImport("/usr/lib/libobjc.A.dylib", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool class_addIvar(IntPtr cls, string name, nuint size, byte alignment, string types);

    [LibraryImport("/usr/lib/libobjc.A.dylib", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr class_getInstanceVariable(IntPtr cls, string name);

    [LibraryImport("/usr/lib/libobjc.A.dylib")]
    private static partial nint ivar_getOffset(IntPtr ivar);
}
