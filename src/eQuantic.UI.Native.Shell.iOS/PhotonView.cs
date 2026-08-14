using CoreAnimation;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace eQuantic.UI.Native.Shell.iOS;

/// <summary>
/// A view that IS a Metal surface. UIKit builds a view's backing layer from the class this returns,
/// so the drawable is the view's own layer rather than a second one sitting inside it — no
/// compositing pass between the engine and the screen, and the layer resizes with the view for free.
/// </summary>
[Register("PhotonView")]
public sealed class PhotonView : UIView
{
    public PhotonView(NativeHandle handle) : base(handle) { }

    public PhotonView() { }

    [Export("layerClass")]
    public static Class GetLayerClass() => new(typeof(CAMetalLayer));

    public CAMetalLayer MetalLayer => (CAMetalLayer)Layer;

    /// <summary>
    /// The semantics bridge, armed by the controller once the host exists. Null before that — a
    /// view with no host has nothing to describe.
    /// </summary>
    internal PhotonAccessibility? Semantics { get; set; }

    /// <summary>
    /// VoiceOver's children — the semantics tree, in reading order. The view stays a CONTAINER (a
    /// UIView is not an accessibility element unless it says so, and this one must not be: it draws
    /// every control on the screen, so answering as one would announce the whole app as a single
    /// unlabelled surface).
    /// </summary>
    [Export("accessibilityElements")]
    public NSObject[] AccessibilityElements() => Semantics?.Elements() ?? [];
}
