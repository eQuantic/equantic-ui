using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// A layer the paint walk found, waiting for the realizer's overlay pass: the layer, and the
/// position whose deferred slot builds what it shows.
/// <para>
/// That position is the <see cref="Overlay"/>'s own, whose child is built only when the layer is
/// shown, or the <see cref="Anchored"/> node's, whose panel is shown inside a layer the paint walk
/// makes around it. Either way the pass shows the slot just before laying the layer out, and a layer
/// that is not shown in a frame takes what its slot held out of the tree when the frame ends.
/// </para>
/// </summary>
/// <param name="Layer">What the pass lays out against the viewport and paints above the page.</param>
/// <param name="Holder">The position whose deferred slot holds the layer's content, or null for a
/// layer no build made, which the pass lays out from a build of its own.</param>
internal readonly record struct QueuedLayer(Overlay Layer, NodeElement? Holder);
