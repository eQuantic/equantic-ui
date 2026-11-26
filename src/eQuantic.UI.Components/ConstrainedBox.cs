using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A box that applies the <see cref="BoxConstraints"/> an author holds: its child is given the room its
/// parent gives, at most the constraints' maximums, and the box is at least their minimums. What a
/// <see cref="LayoutBuilder"/> was told, deflated or tightened, applied to the child it builds.
/// <para>
/// Flutter's <c>ConstrainedBox</c>, and a component rather than a node because a <see cref="Box"/>
/// already takes a minimum and a maximum on each axis and both targets agree on how: this builds one
/// with the constraints' bounds and no other style, so its parity is the box's. Those bounds follow
/// CSS, where a stated maximum wins over a parent's stretch, so in a slot that stretches wider than the
/// maximum the box is the maximum wide, where Flutter's <c>enforce</c> would keep the slot's width.
/// </para>
/// </summary>
public sealed class ConstrainedBox : StatelessComponent
{
    /// <param name="constraints">The room the box allows, inside its parent's.</param>
    /// <param name="child">What the box holds.</param>
    public ConstrainedBox(BoxConstraints constraints, VisualNode child)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        Constraints = constraints;
        Child = child;
    }

    /// <summary>The room the box allows.</summary>
    public BoxConstraints Constraints { get; }

    /// <summary>What the box holds.</summary>
    public VisualNode Child { get; }

    public override VisualNode Build(ComponentContext context) => new Box(new BoxStyle
    {
        MinWidth = Constraints.MinWidth,
        MinHeight = Constraints.MinHeight,
        // An unbounded maximum is no cap at all, which a box says by leaving it unset.
        MaxWidth = Constraints.HasBoundedWidth ? SizeValue.Fixed(Constraints.MaxWidth) : default,
        MaxHeight = Constraints.HasBoundedHeight ? SizeValue.Fixed(Constraints.MaxHeight) : default,
    }, Child);
}
