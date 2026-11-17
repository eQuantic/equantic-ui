namespace eQuantic.UI.Primitives;

/// <summary>
/// What a <see cref="Listener"/> hears of a pointer (#1562): Flutter's <c>PointerEvent</c>, in the listener's
/// own coordinates.
/// </summary>
/// <param name="Phase">Where the pointer is in its life: down, move, up, cancel, hover or scroll.</param>
/// <param name="Position">Where it is, relative to the listener's top-left corner. A pointer followed past the
/// listener's edge reads negative or past its size.</param>
/// <param name="Kind">The device: a mouse, a finger or a pen.</param>
/// <param name="Buttons">The buttons held, none on an up and on a hover.</param>
/// <param name="Modifiers">The modifier keys held.</param>
/// <param name="Pointer">Which pointer it is, the same from its down to its up or its cancel: two fingers are two
/// pointers.</param>
/// <param name="ScrollDelta">How far a scroll moves, in dp toward the content's end (right and down), and zero
/// for every other phase.</param>
public sealed record PointerEvent(
    PointerPhase Phase,
    Point Position,
    PointerKind Kind,
    PointerButtons Buttons,
    KeyModifiers Modifiers,
    int Pointer,
    Point ScrollDelta);
