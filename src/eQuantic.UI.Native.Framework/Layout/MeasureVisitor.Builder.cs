using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// The one door that BUILDS: a <see cref="LayoutBuilder"/>'s child is what its builder answers for the
/// room the node has, and the room is only known here, in the layout pass, which is where Flutter's
/// <c>LayoutBuilder</c> builds too (docs/ELEMENT-TREE-PLAN.md, 3.2). Every other door measures what the
/// build phase built.
/// </summary>
internal sealed partial class MeasureVisitor
{
    /// <summary>The box a builder takes its room as: one that fills on both axes, with nothing else.</summary>
    private static readonly BoxStyle FillingBox = new() { Width = SizeValue.Fill, Height = SizeValue.Fill };

    public LayoutNode Visit(LayoutBuilder node, MeasureState s) => MeasureBuilder(node, s.Constraints, _ctx, s.At);

    /// <summary>
    /// Runs the builder for the room the node has, unless the node and the room are the ones it last ran
    /// for, shows what it answered in the position's slot, which builds its components, and lays it out
    /// as a filling box's child. The slot is shown in every frame the node is measured in, built again or
    /// not, because a slot no frame showed is taken out when that frame ends.
    /// <para>
    /// A parent may measure the node twice in one frame under two rooms: a box whose minimum or maximum
    /// bites measures its child again at the size it settled on. Flutter lays out once and keeps the
    /// result until something changes, so a builder there is told such a pair once; Photon lays the tree
    /// out in every frame, so the pair would build two children in every frame, and the slot would take
    /// one's components out and put the other's in, twice a frame, losing what they hold. So the room a
    /// frame was first measured in, when a later measurement in the same frame built another child, is
    /// remembered beside the room that child was built for, and the next frame measured in that first
    /// room shows the child the last one settled on: built once for each room, and stable from then on.
    /// </para>
    /// </summary>
    private LayoutNode MeasureBuilder(LayoutBuilder builder, LayoutConstraints constraints, LayoutContext ctx, Element? at)
    {
        if (at is not NodeElement holder)
            throw new InvalidOperationException(
                "A LayoutBuilder was measured with no position to build its child at: its child is built in "
                + "layout, at the builder's own position.");

        var room = constraints.FillingRoom();
        var last = holder.State as BuiltFor;
        var sameNode = last is not null && ReferenceEquals(last.Builder, builder);
        // A context is made for each frame, so the one a measurement ran in says which frame it was.
        var firstThisFrame = !sameNode || !ReferenceEquals(last!.Frame, ctx);
        var firstRoom = firstThisFrame ? room : last!.FirstRoom;

        BuiltFor now;
        // The settled child stands in for the first room only as a frame's first measurement: a later
        // measurement of the same frame in that room is the one the frame settles on, and gets its own.
        if (sameNode && (last!.Room == room || (firstThisFrame && last.SettledFrom == room)))
            now = last with { Frame = ctx, FirstRoom = firstRoom };
        else
            now = new BuiltFor(builder, room, builder.Builder(room), ctx, firstRoom,
                // Built by a later measurement of the frame: the first room is the one it settled from.
                SettledFrom: firstThisFrame || firstRoom == room ? null : firstRoom);

        holder.State = now;
        holder.ShowDeferred(now.Child);
        return MeasureBox(builder, FillingBox, now.Child, constraints, ctx, at);
    }

    /// <summary>
    /// What a builder answered, for which node and which room, kept on the position so the same node in
    /// the same room is shown its child again rather than built again. <see cref="Frame"/> and
    /// <see cref="FirstRoom"/> are the frame it was last measured in and that frame's first room;
    /// <see cref="SettledFrom"/> is the first room of the frame that built <see cref="Child"/> in a
    /// later measurement, which shows the same child.
    /// </summary>
    private sealed record BuiltFor(LayoutBuilder Builder, BoxConstraints Room, VisualNode Child,
        LayoutContext Frame, BoxConstraints FirstRoom, BoxConstraints? SettledFrom);
}
