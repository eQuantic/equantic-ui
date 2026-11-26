namespace eQuantic.UI.Native.Components;

/// <summary>Which of the host's own gestures a <see cref="BuiltInGesture"/> is in a pointer's arena (#1563): each
/// keeps the arming it had before the arena existed, and drops what it armed in its own way when it loses.</summary>
internal enum BuiltInGestureKind
{
    /// <summary>A <c>Pressable</c>'s press: it never asks, and wins at the up as the first member.</summary>
    Press,

    /// <summary>A <c>Draggable</c> or a <c>DragDismiss</c>: it asks when it travels past the slop on its axis.</summary>
    Drag,

    /// <summary>A sortable item's pick-up (a table's column, a tree's node): it asks when it lifts.</summary>
    Sort,

    /// <summary>A <c>ContextTarget</c>'s finger hold: it asks when its menu is asked for.</summary>
    ContextHold,

    /// <summary>A table viewport's pan: it asks past its slop, or at once on its scrollbar.</summary>
    TablePan,

    /// <summary>A scroll view's pan: it asks past the slop on its axis.</summary>
    Pan,
}
