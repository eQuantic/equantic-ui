namespace eQuantic.UI.Primitives;

/// <summary>
/// A LABELLED GROUP: a reader names it and then walks into what it holds. Layout-transparent and
/// non-interactive, like <see cref="LiveRegion"/> — it draws nothing, takes no focus, answers no key
/// and adds no geometry; what it changes is that a reader hears the group's name before its
/// contents, as a stop of its own.
///
/// <para>
/// It is a NODE rather than an attribute of a layout node because no word in the vocabulary could
/// say it: a <c>Label</c> on a <see cref="Column"/> would put semantics on a node every semantics
/// walk treats as pure layout. A Select drawing its options under headings was the first to need it
/// (the open panel's groups), and the node is the general one, not the Select's.
/// </para>
///
/// <para>
/// The name is not the web's. AppKit has <c>AXGroup</c>, UIKit the
/// <c>UIAccessibilityContainerType.semanticGroup</c> container, Android a group, and the vocabulary's
/// own <see cref="SemanticRole.Group"/> is what Photon announces. <c>Section</c> was refused: SwiftUI's
/// draws a header and lays out its content, and this node draws nothing.
/// </para>
/// </summary>
public sealed class Group : SingleChildNode
{
    public override string NodeKind => "group";

    public Group(VisualNode child)
        : base(child)
    {
    }

    /// <summary>
    /// The group's name, what a reader hears on arriving at it. Left empty the group is announced
    /// with no name, which is noise to every reader: a component that has no name for a group wraps
    /// none.
    /// </summary>
    public string Label { get; init; } = "";

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
