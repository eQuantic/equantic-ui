namespace eQuantic.UI.Primitives;

/// <summary>
/// A node whose stops are a group the focus walks inside (#1897): Flutter's <c>FocusScopeNode</c>. Tab goes through
/// the traversable stops of the innermost scope around the one that has the keyboard, and <see cref="TraversalEdgeBehavior"/>
/// says what happens past its last one. <see cref="FocusNode.RequestFocus"/> on a scope gives the keyboard to
/// <see cref="FocusedChild"/>, or to the scope's first traversable stop.
/// </summary>
public sealed class FocusScopeNode : FocusNode
{
    /// <summary>The node directly under the scope that last had the keyboard, kept after it leaves: Flutter's
    /// <c>focusedChild</c>. Null until one has, and when it leaves the tree.</summary>
    public FocusNode? FocusedChild { get; internal set; }

    /// <summary>What the focus does past the scope's last traversable stop, or before its first: Flutter's
    /// <c>traversalEdgeBehavior</c>, <see cref="Primitives.TraversalEdgeBehavior.ClosedLoop"/> by default.</summary>
    public TraversalEdgeBehavior TraversalEdgeBehavior { get; set; } = TraversalEdgeBehavior.ClosedLoop;
}
