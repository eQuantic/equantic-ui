namespace eQuantic.UI.Primitives;

/// <summary>The axis a <see cref="Draggable"/> follows. One at a time: a gesture that tracks both
/// competes with the scroll it usually lives inside.
/// <para>
/// <see cref="Horizontal"/> is measured toward the END of the direction where the surface reads, as
/// Flutter's <c>DismissDirection.startToEnd</c> is: travel toward the end is positive whether the page
/// reads left to right or right to left. <see cref="Vertical"/> is down.
/// </para></summary>
public enum DragAxis : byte
{
    Vertical = 0,
    Horizontal = 1,
}
