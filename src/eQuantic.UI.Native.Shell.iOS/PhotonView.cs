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
}
