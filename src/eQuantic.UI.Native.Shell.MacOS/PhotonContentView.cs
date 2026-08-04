using System.Runtime.InteropServices;
using eQuantic.UI.Native.Shell.Apple;
using static eQuantic.UI.Native.Shell.Apple.ObjC;

namespace eQuantic.UI.Native.Shell.MacOS;

/// <summary>
/// The window's content view, defined at RUNTIME so it can override one method: the one AppKit
/// calls while it is resizing.
/// <para>
/// A live resize never returns to the app's own loop — AppKit runs its own inside <c>sendEvent:</c>
/// until the button comes up — and it hands out no time there either: a run-loop observer and a
/// common-modes timer were both tried in the real window and neither fired once. What AppKit DOES
/// do is send <c>setFrameSize:</c> to the content view on every step of the drag. Overriding it is
/// the only place the frame can be re-measured while the edge is still moving.
/// </para>
/// <para>
/// One class, one method, one callback into managed code. The callback is a static field rather
/// than an instance variable because there is exactly one window per process on this head, and a
/// GCHandle for a singleton is bookkeeping that buys nothing.
/// </para>
/// </summary>
internal static class PhotonContentView
{
    private delegate void SetFrameSize(IntPtr self, IntPtr selector, CGSize size);

    private static SetFrameSize? _override;      // kept alive for the runtime's sake
    private static IntPtr _superSetFrameSize;
    private static Action<float, float>? _onResized;

    /// <summary>What runs after AppKit has applied a new size — the frame drawn from there is
    /// measured against it. Set once the host exists; the view is created before it does.</summary>
    internal static Action<float, float>? OnResized
    {
        get => _onResized;
        set => _onResized = value;
    }

    /// <summary>The class, created once.</summary>
    internal static IntPtr Register()
    {
        if (_cls != IntPtr.Zero) return _cls;

        var selector = sel_registerName("setFrameSize:");
        var super = objc_getClass("NSView");
        _superSetFrameSize = method_getImplementation(class_getInstanceMethod(super, selector));

        _cls = objc_allocateClassPair(super, "EQPhotonContentView", 0);
        _override = OnSetFrameSize;
        // v@:{CGSize=dd} — returns void, takes self, the selector, and a struct of two doubles.
        class_addMethod(_cls, selector, Marshal.GetFunctionPointerForDelegate(_override), "v@:{CGSize=dd}");
        objc_registerClassPair(_cls);
        return _cls;
    }

    private static IntPtr _cls;

    private static void OnSetFrameSize(IntPtr self, IntPtr selector, CGSize size)
    {
        // Let AppKit do its part first: the view has to BE the new size before anything measures
        // against it.
        var super = Marshal.GetDelegateForFunctionPointer<SetFrameSize>(_superSetFrameSize);
        super(self, selector, size);
        _onResized?.Invoke((float)size.Width, (float)size.Height);
    }
}
