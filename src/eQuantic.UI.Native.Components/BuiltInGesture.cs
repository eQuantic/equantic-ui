using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// One of the host's own gestures as a member of a pointer's arena (#1563): a press, a drag, a sort, a context
/// hold, a table's pan or a scroll view's pan, armed at the down exactly as before the arena existed. The host asks
/// for the pointer through <see cref="Entry"/> when the gesture arms, and hears the outcome here.
/// </summary>
/// <param name="kind">Which gesture it is.</param>
/// <param name="at">The position of the node it belongs to, which orders it among the recognizers by containment.</param>
/// <param name="kindOfPointer">The device that pressed, which a table's pan is cancelled with.</param>
/// <param name="lost">What the host drops when the gesture loses the pointer.</param>
internal sealed class BuiltInGesture(BuiltInGestureKind kind, Element? at, PointerKind kindOfPointer,
    Action<BuiltInGesture> lost) : IGestureArenaMember
{
    public BuiltInGestureKind Kind { get; } = kind;

    public Element? At { get; } = at;

    public PointerKind Pointer { get; } = kindOfPointer;

    /// <summary>Its place in the arena, through which it asks for the pointer; null until the arena took it.</summary>
    public GestureArenaEntry? Entry { get; set; }

    /// <summary>Whether it won the pointer.</summary>
    public bool Won { get; private set; }

    /// <summary>Whether it lost the pointer: what it armed is gone, and it never asks again.</summary>
    public bool Lost { get; private set; }

    public void AcceptGesture(int pointer) => Won = true;

    public void RejectGesture(int pointer)
    {
        Lost = true;
        lost(this);
    }
}
