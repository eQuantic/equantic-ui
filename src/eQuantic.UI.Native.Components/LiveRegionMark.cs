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
/// <param name="Element">The region's position, which keeps what it said between frames.</param>
/// <param name="Label">What the region is called, said before its text where the platform does.</param>
/// <param name="Urgency">How hard its announcement interrupts.</param>
/// <param name="Node">This frame's subtree, whose text is the announcement unless <paramref name="Text"/>
/// says it.</param>
/// <param name="Text">What the region says, when it says it in words of its own rather than through what
/// it draws: a spreadsheet's surface announcing its moves (<see cref="SheetSurface.Announcement"/>, #1081).
/// Null reads it from <paramref name="Node"/>'s subtree, as a <see cref="LiveRegion"/>'s.</param>
internal readonly record struct LiveRegionMark(
    Element Element,
    string Label,
    LiveRegionUrgency Urgency,
    LayoutNode Node,
    string? Text = null);
