namespace eQuantic.UI.Primitives;

/// <summary>
/// Where a pointer is in its life: pressed, moving while pressed, lifted, or passing over without a
/// press. The names are Flutter's (<c>PointerDownEvent</c>, <c>PointerMoveEvent</c>,
/// <c>PointerUpEvent</c>, <c>PointerHoverEvent</c>, <c>PointerExitEvent</c>), because a pointer's
/// phases are the same on every target and the web's own words for them would not survive a native
/// shell.
/// </summary>
public enum PointerPhase : byte
{
    /// <summary>A button went down (or a finger touched) over the surface.</summary>
    Down = 0,

    /// <summary>The pointer moved while the press that began on the surface is still held.</summary>
    Move = 1,

    /// <summary>The press ended.</summary>
    Up = 2,

    /// <summary>The pointer moved over the surface with nothing pressed.</summary>
    Hover = 3,

    /// <summary>The pointer left the surface.</summary>
    Exit = 4,
}
