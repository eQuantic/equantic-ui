using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// ONE LIVE REGION, AS THIS FRAME FOUND IT — where it is, what it is called, how hard it
/// interrupts, and the subtree whose text IS the announcement.
///
/// <para>
/// Recorded by the emit walk rather than derived from the semantics tree, and that is the whole
/// design. <c>PhotonHost.Semantics()</c> is an ON-DEMAND snapshot: a bridge may ask for it never,
/// once, or three times in a frame, so an announcement posted from that walk would fire a number of
/// times nobody chose. The emit walk runs exactly once per frame, which is the cadence a
/// frame-to-frame diff needs.
/// </para>
///
/// <para>
/// It carries the NODE because the announcement is what a reader would say for the subtree, and the
/// only thing that knows that is the semantics walk — run over this node alone
/// (<see cref="SemanticsTree.CollectChildren"/>), never over the whole frame. The node is safe to
/// hold for exactly as long as the frame is: <c>RenderFrame</c> recycles the PREVIOUS frame's tree,
/// so a mark is read while its own tree is still live. What the announcer keeps between frames is
/// the resulting STRING and never this.
/// </para>
/// </summary>
internal readonly record struct LiveRegionMark(
    string Path,
    string Label,
    LiveRegionUrgency Urgency,
    LayoutNode Node);
