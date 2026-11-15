using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Sortable lists and their items (#1537): each registers where it was drawn, for the host to lift, find and
/// place an item by, and an item the host carries is dimmed in its place, while a copy of it, recorded the
/// first time it is drawn lifted, follows the pointer over the frame (<c>PhotonHost.Sorting</c>).
/// </summary>
internal sealed partial class EmitVisitor
{
    private void EmitSortable(Sortable node, EmitState s)
    {
        if (s.Node.Element is { } at) s.Input.Add(new SortRegion(s.Node.Bounds, s.Node.Bounds, at, node));
        Descend(s);
    }

    private void EmitSortableItem(SortableItem node, EmitState s)
    {
        if (s.Node.Element is not { } at)
        {
            Descend(s);
            return;
        }
        s.Input.Add(new SortRegion(s.Node.Bounds, s.Node.Bounds, at, node));
        if (NodeStates.Find<SortLiftState>(at) is not { Lifted: true } lift)
        {
            Descend(s);
            return;
        }
        if (lift is { Follows: true, Copy: null }) RecordCopy(lift, s);
        s.Builder.PushLayer((float)SortPlacement.LiftedOpacity);
        Descend(s);
        s.Builder.PopLayer();
    }

    /// <summary>
    /// The copy that follows the pointer: the item drawn once more, into a builder of its own, where it stands
    /// on screen, at rest and outside every clip around it, under the shadow of something held. A picture, as
    /// the web's copy is a clone: it shows the item as it was picked up, and the host draws it over the frame
    /// wherever the pointer takes it.
    /// <para>
    /// Drawn with nothing that remembers: no press, focus or hover (the copy is held, not hovered), no input
    /// (it takes none), and no clock of presences or transitions, which would take the copy's paint for the
    /// item's and glide the item between the two.
    /// </para>
    /// </summary>
    private void RecordCopy(SortLiftState lift, EmitState s)
    {
        var transform = s.Builder.CurrentTransform;
        var bounds = s.Node.Bounds;
        var copy = new DisplayListBuilder();
        copy.PushTransform(transform);
        copy.ShadowRRect(new RRect(bounds, CornerOf(s.Node)), SortPlacement.CopyShadowOffsetY,
            SortPlacement.CopyShadowBlur, 0, new Color(0, 0, 0, (byte)Math.Round(SortPlacement.CopyShadowAlpha * 255)));
        var still = s with
        {
            Input = new InputSink(new FrameRegions()),
            Press = new PressScope(null, null, density: s.Press.Density) { Surface = s.Press.Surface },
            Motion = s.Motion.Still(),
            Builder = copy,
            Overlays = [],
        };
        foreach (var child in s.Node)
            Emit(still with { Node = child });
        copy.Pop();
        lift.Copy = copy.PictureFrom(0);
        lift.Center = transform.Transform(new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
    }

    /// <summary>The corners the item's shadow follows: those of the first box down its first children, the
    /// card a list's item usually is, or square.</summary>
    private static CornerRadii CornerOf(LayoutNode item)
    {
        var at = item;
        for (var depth = 0; depth < 6; depth++)
        {
            switch (at.Source)
            {
                case Box box:
                    return box.Style.CornerRadius;
                case FlexNode flex when !flex.CornerRadius.Equals(default(CornerRadii)):
                    return flex.CornerRadius;
            }
            if (at.Children.Count == 0) break;
            at = at.Children[0];
        }
        return default;
    }
}
