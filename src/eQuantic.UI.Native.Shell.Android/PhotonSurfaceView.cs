using Android.Content;
using Android.Views;
using Android.Views.Accessibility;

namespace eQuantic.UI.Native.Shell.Android;

/// <summary>
/// The surface the engine presents into, and the ONE view the platform sees. Everything drawn on it
/// is described through a virtual hierarchy (<see cref="PhotonAccessibility"/>) rather than through
/// child views — the same shape a chart or a map view uses, and the reason a Photon screen can be
/// read at all.
/// </summary>
internal sealed class PhotonSurfaceView : SurfaceView
{
    internal PhotonSurfaceView(Context context) : base(context) =>
        // Without this the platform decides a bare SurfaceView is not worth describing — and since
        // it is the ONLY view in the window, the window ends up with no accessible root at all:
        // uiautomator answers "null root node" and a screen reader finds nothing to read. Saying
        // YES is what puts the virtual hierarchy on the map.
        ImportantForAccessibility = ImportantForAccessibility.Yes;

    /// <summary>The semantics bridge, armed once the host exists. Null before that — a view with no
    /// host has nothing to describe.</summary>
    internal PhotonAccessibility? Semantics { get; set; }

    public override AccessibilityNodeProvider? AccessibilityNodeProvider => Semantics;

    /// <summary>A finger exploring the screen asks what is under it; the bridge answers by naming a
    /// virtual child. Unhandled hovers fall through, so nothing changes when TalkBack is off.</summary>
    protected override bool DispatchHoverEvent(MotionEvent? e) =>
        (e is not null && Semantics?.DispatchHover(e) == true) || base.DispatchHoverEvent(e);
}
