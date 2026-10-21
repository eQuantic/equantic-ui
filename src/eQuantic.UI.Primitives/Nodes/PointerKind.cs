namespace eQuantic.UI.Primitives;

/// <summary>
/// What kind of device a pointer event came from. The names are Flutter's
/// (<c>PointerDeviceKind.mouse</c>, <c>touch</c>, <c>stylus</c>), and the distinction carries two
/// contracts on every target. Hover belongs to pointers that can REST OVER things without touching
/// them (spec S5: "hover never fires on touch"), so the hover pipeline stays on the mouse side. And a
/// drag means different things: a mouse dragged over a grid selects, a finger dragging the same grid
/// scrolls it.
/// <para>
/// Per EVENT rather than per host, because the kind belongs to the device in the hand, not to the
/// app: an iPad gains a trackpad, an Android gains a mouse, and the same host serves both kinds in
/// one session.
/// </para>
/// </summary>
public enum PointerKind : byte
{
    /// <summary>A tracked pointer (mouse, trackpad): moving it hovers.</summary>
    Mouse = 0,

    /// <summary>A finger: it presses, drags and pans, and never hovers.</summary>
    Touch = 1,

    /// <summary>A pen or a stylus: it presses and drags like a finger.</summary>
    Stylus = 2,
}
