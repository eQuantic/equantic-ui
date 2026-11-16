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
    /// not, because a slot no frame showed is taken out when that frame ends. A parent that measures the
    /// node twice under two rooms builds twice, as Flutter's does.
    /// </summary>
    private LayoutNode MeasureBuilder(LayoutBuilder builder, LayoutConstraints constraints, LayoutContext ctx, Element? at)
    {
        if (at is not NodeElement holder)
            throw new InvalidOperationException(
                "A LayoutBuilder was measured with no position to build its child at: its child is built in "
                + "layout, at the builder's own position.");

        var room = constraints.FillingRoom();
        var child = holder.State is BuiltFor last && ReferenceEquals(last.Builder, builder) && last.Room == room
            ? last.Child
            : builder.Builder(room);
        holder.State = new BuiltFor(builder, room, child);
        holder.ShowDeferred(child);
        return MeasureBox(builder, FillingBox, child, constraints, ctx, at);
    }

    /// <summary>What a builder answered, for which node and which room: kept on the position, so the same
    /// node in the same room is shown its child again rather than built again.</summary>
    private sealed record BuiltFor(LayoutBuilder Builder, BoxConstraints Room, VisualNode Child);
}
