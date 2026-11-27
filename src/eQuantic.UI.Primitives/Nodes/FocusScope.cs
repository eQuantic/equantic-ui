namespace eQuantic.UI.Primitives;

/// <summary>
/// A group of stops the focus walks inside, held by a <see cref="FocusScopeNode"/> (#1897): Flutter's
/// <c>FocusScope</c>. Tab goes through the traversable stops under the innermost scope around the one that has the
/// keyboard, and <see cref="TraversalEdgeBehavior"/> says what it does past the last one: wrap, stay, or go on in
/// the scope around this one. The scope remembers which child had the keyboard last, and asking for the keyboard on
/// it gives it back to that child.
/// <para>
/// A block around any subtree, layout-transparent, as <see cref="Focus"/> is. A modal layer stands in the walk as a
/// closed-loop scope of its own, which realizers make.
/// </para>
/// </summary>
public sealed class FocusScope : SingleChildNode
{
    public override string NodeKind => "focusScope";

    /// <param name="child">The group.</param>
    /// <param name="node">The scope the app holds, or null for one the realizer keeps at this position.</param>
    /// <param name="autofocus">Whether it asks for the keyboard once, when this position first appears, unless the
    /// keyboard is already inside its nearest scope.</param>
    /// <param name="onFocusChange">Called with <see cref="FocusNode.HasFocus"/> when it changes.</param>
    /// <param name="traversalEdgeBehavior">Written onto the scope at every build when given; null leaves the
    /// scope's own, <see cref="Primitives.TraversalEdgeBehavior.ClosedLoop"/> for a new one.</param>
    public FocusScope(VisualNode child, FocusScopeNode? node = null, bool autofocus = false, Action<bool>? onFocusChange = null,
        TraversalEdgeBehavior? traversalEdgeBehavior = null)
        : base(child)
    {
        Node = node;
        Autofocus = autofocus;
        OnFocusChange = onFocusChange;
        TraversalEdgeBehavior = traversalEdgeBehavior;
    }

    /// <summary>The scope the app holds, or null for one the realizer keeps at this position.</summary>
    public FocusScopeNode? Node { get; init; }

    /// <summary>Whether it asks for the keyboard once, when this position first appears.</summary>
    public bool Autofocus { get; init; }

    /// <summary>Called with <see cref="FocusNode.HasFocus"/> when it changes.</summary>
    public Action<bool>? OnFocusChange { get; init; }

    /// <summary>What the focus does past the scope's last stop, written onto it at every build; null leaves it.</summary>
    public TraversalEdgeBehavior? TraversalEdgeBehavior { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
