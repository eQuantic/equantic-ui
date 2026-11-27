using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// The door that runs the app's layout: a <see cref="CustomMultiChildLayout"/>'s delegate measures each child with
/// the constraints it chooses and places it, in this pass, where Flutter's <c>RenderCustomMultiChildLayoutBox</c>
/// runs it. A <see cref="CustomSingleChildLayout"/> is laid out through the same door, as a layout of one.
/// </summary>
internal sealed partial class MeasureVisitor
{
    public LayoutNode Visit(CustomMultiChildLayout node, MeasureState s) =>
        MeasureCustomLayout(node, node.Delegate, node.Children, s.Constraints, _ctx, s.At);

    public LayoutNode Visit(CustomSingleChildLayout node, MeasureState s) =>
        MeasureCustomLayout(node, node.MultiChildDelegate, [node.LaidOutChild], s.Constraints, _ctx, s.At);

    // Outside a custom layout an id says nothing, and the node is its child's.
    public LayoutNode Visit(LayoutId node, MeasureState s) => MeasureWrapper(node, node.Child, s.Constraints, _ctx, s.At);

    /// <summary>
    /// Tells the delegate the room a filling box has (<see cref="LayoutConstraints.FillingRoom"/>, a
    /// <see cref="LayoutBuilder"/>'s), lets it lay out and place the children, and takes the room's size on an axis
    /// the parent decides and the delegate's on an axis sized by content. The children keep their order, which is
    /// the order a reader reaches them in, wherever they were placed.
    /// </summary>
    private LayoutNode MeasureCustomLayout(VisualNode node, MultiChildLayoutDelegate layoutDelegate,
        IReadOnlyList<LayoutId> children, LayoutConstraints constraints, LayoutContext ctx, Element? at)
    {
        var room = constraints.FillingRoom();
        var result = ctx.Node(node);
        var site = new CustomLayoutChildren(this, children, ctx, at);
        var size = layoutDelegate.LayoutWith(site, room, site.Ids);
        var width = float.IsPositiveInfinity(room.MaxWidth) ? size.Width : room.MaxWidth;
        var height = float.IsPositiveInfinity(room.MaxHeight) ? size.Height : room.MaxHeight;
        result.Bounds = new Rect(0, 0, width, height);
        result.AdoptAll(site.LaidOut!);
        for (var i = 0; i < children.Count; i++)
            site.LaidOut[i]!.Bounds = site.LaidOut[i]!.Bounds with { X = site.Places[i].X, Y = site.Places[i].Y };
        return result;
    }

    /// <summary>
    /// The bounds a delegate's constraints set on the box its child is laid out in. A child is measured as the
    /// content of a box with those bounds, in a room of their maximum: the <see cref="Box"/> rule both targets
    /// already agree on, so a hugging child is its own size inside them and a tight constraint is its size.
    /// </summary>
    private static BoxStyle BoundsOf(BoxConstraints constraints) => new()
    {
        MinWidth = constraints.MinWidth,
        MaxWidth = constraints.HasBoundedWidth ? SizeValue.Fixed(constraints.MaxWidth) : default,
        MinHeight = constraints.MinHeight,
        MaxHeight = constraints.HasBoundedHeight ? SizeValue.Fixed(constraints.MaxHeight) : default,
    };

    /// <summary>A custom layout's children as its delegate reaches them for one layout: measured and placed here.</summary>
    private sealed class CustomLayoutChildren(MeasureVisitor visitor, IReadOnlyList<LayoutId> children,
        LayoutContext ctx, Element? at) : ILayoutChildren
    {
        public LayoutNode?[] LaidOut { get; } = new LayoutNode?[children.Count];
        public Point[] Places { get; } = new Point[children.Count];
        public IReadOnlyList<object> Ids { get; } = [.. children.Select(child => child.Id)];

        public bool Has(object id) => IndexOf(id) >= 0;

        public Size Layout(object id, BoxConstraints constraints)
        {
            var index = IndexOf(id);
            var child = children[index];
            var laidOut = visitor.MeasureBox(child, BoundsOf(constraints), child.Child,
                LayoutConstraints.Of(constraints.MaxWidth, constraints.MaxHeight), ctx, at?.ChildAt(index));
            LaidOut[index] = laidOut;
            return new Size(laidOut.Bounds.Width, laidOut.Bounds.Height);
        }

        public void Position(object id, Point offset) => Places[IndexOf(id)] = offset;

        private int IndexOf(object id)
        {
            for (var i = 0; i < children.Count; i++)
                if (Equals(children[i].Id, id)) return i;
            return -1;
        }
    }
}
