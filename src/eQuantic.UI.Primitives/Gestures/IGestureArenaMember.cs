namespace eQuantic.UI.Primitives;

/// <summary>
/// Something that competes for a pointer in its arena (#1563): Flutter's <c>GestureArenaMember</c>. An app's
/// <see cref="GestureRecognizer"/> is one, and so is each of the SDK's own gestures (a press, a drag, a sort, a
/// context hold, a scroll's pan), which a realizer enters for the pointers it arms for. A member hears exactly
/// one of the two outcomes for each pointer it joined the arena of, exactly once.
/// </summary>
public interface IGestureArenaMember
{
    /// <summary>The member won <paramref name="pointer"/>: the gesture is its own, and every other member lost it.</summary>
    void AcceptGesture(int pointer);

    /// <summary>The member lost <paramref name="pointer"/>: another member won it, the member let it go, or the system
    /// took it away. What the member armed for it is dropped.</summary>
    void RejectGesture(int pointer);
}
