namespace eQuantic.UI.Primitives;

/// <summary>
/// Fades its subtree along a gradient of opacities (#1938): the child is composited as ONE group and
/// multiplied, at every point, by what its <see cref="Mask"/> keeps there. Flutter's
/// <c>ShaderMask(blendMode: BlendMode.dstIn)</c>, as a node around any child: a box, a row of text, an
/// image or a scroller's content fades the same way, and overlapping children never double-blend.
/// <para>
/// Layout-transparent: the parent sizes, floors and shrinks it as it would its child. It changes no hit
/// test, so a point the mask hides still takes the press it took before. What the child draws outside
/// the node's bounds is cut.
/// </para>
/// </summary>
public sealed class Masked : SingleChildNode
{
    public override string NodeKind => "masked";

    public Masked(Mask mask, VisualNode child)
        : base(child)
    {
        ArgumentNullException.ThrowIfNull(mask);
        Mask = mask;
    }

    /// <summary>What the subtree keeps at each point of its bounds.</summary>
    public Mask Mask { get; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
