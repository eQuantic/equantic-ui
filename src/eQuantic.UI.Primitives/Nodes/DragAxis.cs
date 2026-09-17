namespace eQuantic.UI.Primitives;

/// <summary>The axis a <see cref="Draggable"/> follows. One at a time: a gesture that tracks both
/// competes with the scroll it usually lives inside.</summary>
public enum DragAxis : byte
{
    Vertical = 0,
    Horizontal = 1,
}
