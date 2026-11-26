namespace eQuantic.UI.Primitives;

/// <summary>
/// Gestures of the app's own over any subtree (#1563): Flutter's <c>RawGestureDetector</c>. The child is what it
/// wraps, and each of <see cref="Gestures"/> makes a <see cref="GestureRecognizer"/> the detector keeps for as long
/// as its position lives, handing it, at every build, what that build wrote. Every pointer that goes down where the
/// detector is hit (by <see cref="Behavior"/>, the rule a <see cref="Listener"/> is heard by) is offered to its
/// recognizers, which compete for it in the pointer's arena with each other, with the recognizers of the detectors
/// around it and with the SDK's own gestures under it: a press, a drag, a sort, a context hold, a scroll.
/// <para>
/// A block, not a member of <see cref="Pressable"/>: a gesture the catalog does not ship (a long press that then
/// drags, a two-step swipe) is a C# class and this one wrapper, on every target. Layout-transparent: the child owns
/// every visual.
/// </para>
/// </summary>
public sealed class RawGestureDetector : SingleChildNode
{
    public override string NodeKind => "rawGestureDetector";

    /// <param name="child">What the gestures are over.</param>
    /// <param name="gestures">One factory per recognizer, each kept by its place in the list across rebuilds.</param>
    public RawGestureDetector(VisualNode child, IReadOnlyList<GestureRecognizerFactory> gestures)
        : base(child)
    {
        Gestures = gestures;
    }

    /// <summary>The recognizers' factories, each kept by its place: a place whose factory makes another type of
    /// recognizer than the one it keeps makes a new one.</summary>
    public IReadOnlyList<GestureRecognizerFactory> Gestures { get; init; }

    /// <summary>Where the detector is hit: only where its child is (the default), or anywhere in its box, hiding the
    /// detectors and listeners behind it or letting them be hit too.</summary>
    public HitTestBehavior Behavior { get; init; } = HitTestBehavior.DeferToChild;

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
