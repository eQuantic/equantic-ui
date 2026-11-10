namespace eQuantic.UI.Primitives;

/// <summary>
/// A value for a subtree: a node with one child, which every component below it can read through
/// <see cref="ComponentContext.DependOn{T}"/>, and which rebuilds the components that read it when it
/// changes, and nothing else. Flutter's <c>InheritedWidget</c> (docs/ELEMENT-TREE-PLAN.md, 3.4).
/// <para>
/// An app writes its own: a subclass holds the value as properties, takes its child, and answers
/// <see cref="UpdateShouldNotify"/>. A component reads it with <c>context.DependOn&lt;T&gt;()</c>, which
/// makes the component a dependent, or with <c>context.Find&lt;T&gt;()</c> for a value read once.
/// When a parent's build places one with a different value at the same position, the components
/// that depend on it build again in the same frame, and the subtree between them does not.
/// </para>
/// <para>
/// Every target draws its child as if it were not there. Photon builds an element per position, so
/// there a component finds the nearest one above it. The web and the server build with no element per
/// position until #1395, and a context there answers null.
/// </para>
/// </summary>
public abstract class InheritedComponent : SingleChildNode
{
    /// <summary>A value for <paramref name="child"/> and everything below it.</summary>
    protected InheritedComponent(VisualNode child) : base(child)
    {
    }

    /// <summary>All inherited components share one wire kind: a visitor answers "pass through" once.</summary>
    public sealed override string NodeKind => "inherited";

    /// <summary>
    /// SEALED, as <see cref="UiComponent.Accept{TState, TResult}"/> is: an app's inherited components are
    /// unbounded, so a visitor answers for them once.
    /// </summary>
    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);

    /// <summary>
    /// Whether the components that depend on this value build again, now that this one took the place
    /// of <paramref name="old"/> at the same position: true when what they read of it changed. Asked
    /// only of a node of the same type and key, which is when a position keeps its element.
    /// </summary>
    protected internal abstract bool UpdateShouldNotify(InheritedComponent old);
}
