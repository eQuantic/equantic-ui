namespace eQuantic.UI.Primitives;

/// <summary>
/// The direction a subtree reads in, after Flutter's <c>Directionality</c>: a component below it reads
/// <see cref="ComponentContext.TextDirection"/> as this node's <see cref="Direction"/>, and builds again
/// when a new one replaces it; a component with none above it reads its UI culture's.
/// <para>
/// The vocabulary's values are directional already (<see cref="EdgeInsets"/> has a start and an end,
/// <see cref="Positioned"/> a start and an end, <see cref="TextAlignment"/> a start and an end), so a
/// tree written for one direction reads in the other under this node. The web writes the subtree's
/// direction on its child's own element, so a start and an end mirror there; Photon reads the
/// direction and lays out left to right until it mirrors too.
/// </para>
/// <para>
/// A part of a page that places its content by a physical position (a code view, a diagram) wraps
/// itself in one that reads <see cref="TextDirection.LeftToRight"/>, whatever the page's language.
/// </para>
/// </summary>
public sealed class Directionality : InheritedComponent
{
    /// <summary>A subtree that reads in <paramref name="direction"/>.</summary>
    /// <param name="direction">The direction the subtree reads in.</param>
    /// <param name="child">The subtree.</param>
    public Directionality(TextDirection direction, VisualNode child) : base(child)
    {
        Direction = direction;
    }

    /// <summary>The direction the subtree reads in.</summary>
    public TextDirection Direction { get; }

    /// <summary>Its dependents build again when the direction is the other one.</summary>
    protected internal override bool UpdateShouldNotify(InheritedComponent old) => ((Directionality)old).Direction != Direction;
}
