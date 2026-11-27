namespace eQuantic.UI.Primitives;

/// <summary>
/// How a <see cref="CustomSingleChildLayout"/> sizes itself, measures its child and places it, written by the app
/// in C#: Flutter's <c>SingleChildLayoutDelegate</c>. Every step has Flutter's default: the room's biggest size,
/// the room for the child, and the origin.
/// </summary>
public abstract class SingleChildLayoutDelegate
{
    /// <summary>The node's size for <paramref name="constraints"/>: the biggest the room allows by default. An
    /// axis the room leaves unbounded needs a finite size.</summary>
    public virtual Size GetSize(BoxConstraints constraints) => constraints.Biggest;

    /// <summary>The bounds the child is measured with, for the node's room: the room itself by default.</summary>
    public virtual BoxConstraints GetConstraintsForChild(BoxConstraints constraints) => constraints;

    /// <summary>Where the child of <paramref name="childSize"/> is placed in a node of <paramref name="size"/>: the
    /// origin by default.</summary>
    public virtual Point GetPositionForChild(Size size, Size childSize) => Point.Zero;

    /// <summary>Whether this delegate lays out otherwise than <paramref name="oldDelegate"/>, the one it replaces.</summary>
    public virtual bool ShouldRelayout(SingleChildLayoutDelegate oldDelegate) => true;
}
