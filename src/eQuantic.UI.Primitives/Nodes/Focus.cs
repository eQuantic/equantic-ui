namespace eQuantic.UI.Primitives;

/// <summary>
/// The focus of any subtree, held by a <see cref="FocusNode"/> (#1897): Flutter's <c>Focus</c>. The child is what
/// the node stands for: it asks for the keyboard on the child's first stop, tells when the child has it, and is
/// reached by Tab in the tree's order unless it says otherwise. A child with no control of its own (a panel of text)
/// becomes a stop itself, so a <see cref="Shortcut"/> scoped to the focus inside it has an answer.
/// <para>
/// A block, not a flag on <see cref="Pressable"/> or a text field: a capability the catalog gives its own
/// components, and an app gives any of its own, is this one wrapper on every target. Layout-transparent: the child
/// owns every visual. The node is the app's, held in its component's state and outliving every rebuild; given none,
/// the realizer keeps one at the node's place, as Flutter's <c>Focus</c> makes one in its state.
/// </para>
/// </summary>
public sealed class Focus : SingleChildNode
{
    public override string NodeKind => "focus";

    /// <param name="child">What the node stands for.</param>
    /// <param name="focusNode">The node the app holds, or null for one the realizer keeps at this position.</param>
    /// <param name="autofocus">Whether it asks for the keyboard once, when this position first appears, unless the
    /// keyboard is already inside its nearest scope: Flutter's <c>autofocus</c>.</param>
    /// <param name="onFocusChange">Called with <see cref="FocusNode.HasFocus"/> when it changes: Flutter's
    /// <c>onFocusChange</c>.</param>
    /// <param name="canRequestFocus">Written onto the node at every build when given: Flutter's
    /// <c>canRequestFocus</c>. Null leaves the node's own value.</param>
    /// <param name="skipTraversal">Written onto the node at every build when given: Flutter's
    /// <c>skipTraversal</c>. Null leaves the node's own value.</param>
    public Focus(VisualNode child, FocusNode? focusNode = null, bool autofocus = false, Action<bool>? onFocusChange = null,
        bool? canRequestFocus = null, bool? skipTraversal = null)
        : base(child)
    {
        FocusNode = focusNode;
        Autofocus = autofocus;
        OnFocusChange = onFocusChange;
        CanRequestFocus = canRequestFocus;
        SkipTraversal = skipTraversal;
    }

    /// <summary>The node the app holds, or null for one the realizer keeps at this position.</summary>
    public FocusNode? FocusNode { get; init; }

    /// <summary>Whether it asks for the keyboard once, when this position first appears.</summary>
    public bool Autofocus { get; init; }

    /// <summary>Called with <see cref="FocusNode.HasFocus"/> when it changes.</summary>
    public Action<bool>? OnFocusChange { get; init; }

    /// <summary>Whether the node may take the keyboard, written onto it at every build; null leaves it as it is.</summary>
    public bool? CanRequestFocus { get; init; }

    /// <summary>Whether Tab goes past the node, written onto it at every build; null leaves it as it is.</summary>
    public bool? SkipTraversal { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
