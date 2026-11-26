namespace eQuantic.UI.Primitives;

/// <summary>
/// THE PART THAT DESCRIBES ITS NEAREST NAMEABLE: what its subtree says is what a reader hears after the nameable's name
/// and state, a field's helper or error, a dialog's message. It reaches its nameable exactly as its twin
/// <see cref="LabelPart"/> does, which says which nameables there are and which one a part reaches, and several parts
/// describe one nameable together, in reading order. Layout-transparent and non-interactive: it draws nothing, adds no
/// geometry and is no stop of its own, so what it holds is read where it stands, as a field's message line is.
///
/// <para>
/// The web lists the part in the nameable's <c>aria-describedby</c>. Photon carries the words as the stop's
/// <see cref="SemanticNode.Description"/>, the help AppKit reads after the name, the hint UIKit reads and the hint text
/// Android's node holds.
/// </para>
/// </summary>
public sealed class DescriptionPart : SingleChildNode
{
    public override string NodeKind => "descriptionPart";

    public DescriptionPart(VisualNode child)
        : base(child)
    {
    }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
