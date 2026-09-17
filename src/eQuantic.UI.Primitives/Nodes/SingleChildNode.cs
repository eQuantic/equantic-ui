namespace eQuantic.UI.Primitives;

/// <summary>
/// A node that wraps exactly ONE child — twenty of the vocabulary's forty, and the shape Flutter
/// names <c>SingleChildRenderObjectWidget</c>.
///
/// <para>
/// It exists because the shape was being re-derived by hand in every pass that asks a question ABOUT
/// a node rather than visiting it. <c>CrossSizeKind</c> in the layout engine carried fourteen
/// identical arms — `Pressable`, `Hoverable`, `Pinned`, `Link`, `Presence` … — each saying "look
/// through to my child", and a wrapper added without joining that list did not fail: it answered
/// <see cref="SizeKind.Hug"/> and stretched where it should have looked through. One arm over this
/// base is the same statement the compiler keeps true for the twenty-first wrapper.
/// </para>
///
/// <para>
/// <see cref="Box"/> is deliberately NOT one of these. Its child is optional — a Box with a
/// background and a size and nothing inside it is an ordinary thing to write — and a base whose
/// whole meaning is "wraps a child" would have to make <c>Child</c> nullable for everyone to admit
/// it, which is the contract twenty nodes have and one does not.
/// </para>
/// </summary>
/// <remarks>
/// CLOSED BY CONSTRUCTION, for the same reason <see cref="VisualNode"/> and <see cref="FlexNode"/>
/// are: a public constructor on a public abstract class is a second door into the vocabulary, and
/// the visitor is exhaustive only while there is none.
/// <para>
/// HOST ONLY, and the alternative is worse than it looks. Every public `Primitives` type silently
/// promises a runtime export of the same name, because the transpiler routes that namespace to
/// `@equantic/runtime` — so a component naming this one in a property or a signature would emit an
/// import of an export that is not there and die at hydration. The exception list exists for that,
/// and the list's own note says which side of it a type like this belongs on: a `[ServerOnly]` type
/// is REFUSED at the call site (EQ2010) and drops out of the pin by that rule, "gone from here
/// rather than excused here". A shape is not something a page names anyway; it is how a realizer
/// asks what a node is.
/// </para>
/// </remarks>
[ServerOnly]
public abstract class SingleChildNode : VisualNode
{
    private protected SingleChildNode(VisualNode child) => Child = child;

    /// <summary>What this node wraps. Never null: a wrapper with nothing to wrap is not one.</summary>
    public VisualNode Child { get; init; }
}
