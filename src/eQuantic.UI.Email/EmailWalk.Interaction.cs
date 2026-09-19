using eQuantic.UI.Primitives;

namespace eQuantic.UI.Email;

/// <summary>
/// Interaction and motion — fourteen words, of which the medium has one. A <see cref="Link"/> is the
/// whole of what a reader can DO with a message; pressing, dragging, hovering, adjusting and
/// animating each need script or a pointer the client will not give, and an email that pretended
/// otherwise would be a control that does nothing when tapped.
/// </summary>
internal abstract partial class EmailWalk
{
    /// <summary>A destination and a child — the one interaction an inbox has.</summary>
    public abstract Nothing Visit(Link node, Nothing state);

    public Nothing Visit(Adjustable node, Nothing state) => Refuse(node);

    public Nothing Visit(Progress node, Nothing state) => Refuse(node);

    public Nothing Visit(LiveRegion node, Nothing state) => Refuse(node);

    public Nothing Visit(DragDismiss node, Nothing state) => Refuse(node);

    public Nothing Visit(Draggable node, Nothing state) => Refuse(node);

    public Nothing Visit(Hoverable node, Nothing state) => Refuse(node);

    public Nothing Visit(InFlow node, Nothing state) => Refuse(node);

    public Nothing Visit(InView node, Nothing state) => Refuse(node);

    public Nothing Visit(LoopMotion node, Nothing state) => Refuse(node);

    public Nothing Visit(Navigable node, Nothing state) => Refuse(node);

    public Nothing Visit(Presence node, Nothing state) => Refuse(node);

    public Nothing Visit(Pressable node, Nothing state) => Refuse(node);

    public Nothing Visit(Shortcut node, Nothing state) => Refuse(node);

    public Nothing Visit(Simulated node, Nothing state) => Refuse(node);
}
