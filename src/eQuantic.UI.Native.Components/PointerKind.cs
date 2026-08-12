namespace eQuantic.UI.Native.Components;

/// <summary>
/// What kind of device a pointer event came from. The distinction carries exactly one contract:
/// hover belongs to pointers that can REST OVER things without touching them (spec S5 — "hover
/// never fires on touch"), so shells label each event and the host keeps the hover pipeline on
/// the mouse side. Per EVENT rather than per host, because hover-ability belongs to the device
/// in the hand, not to the app: an iPad gains a trackpad, an Android gains a mouse, and the same
/// host serves both kinds in one session.
/// </summary>
public enum PointerKind : byte
{
    /// <summary>A tracked pointer (mouse/trackpad): moving it hovers.</summary>
    Mouse = 0,

    /// <summary>A finger: it presses, drags and pans — it never hovers.</summary>
    Touch = 1,
}
