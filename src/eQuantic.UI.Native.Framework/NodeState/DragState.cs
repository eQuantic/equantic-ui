namespace eQuantic.UI.Native.Framework;

/// <summary>
/// What a <see cref="Primitives.Draggable"/>'s or a <see cref="Primitives.DragDismiss"/>'s position
/// keeps: the offset a finger holds it at, or the glide back to its rest after the finger lifted.
/// Moved through the host's <see cref="DragClock"/>.
/// </summary>
internal sealed class DragState
{
    /// <summary>Whether a finger holds it now, at <see cref="Offset"/>.</summary>
    public bool Dragging;

    /// <summary>Where the finger holds it, in dp along its axis, signed.</summary>
    public float Offset;

    /// <summary>Whether it glides from <see cref="From"/> to <see cref="To"/>, from
    /// <see cref="StartMs"/>, over <see cref="Primitives.Motion.BaseMs"/>.</summary>
    public bool Gliding;

    public float From;

    public float To;

    public float StartMs;
}
