using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Framework;

/// <summary>
/// The host's drag motion behind <see cref="DragDismiss"/> and <see cref="Draggable"/>. Two regimes:
/// an ACTIVE drag follows the pointer raw (the input marks the frame dirty, so a still finger burns no
/// frames), and a RELEASED drag glides back to rest over Motion.Base as a pure function of time (the
/// same smoothstep stand-in as the value animator, deterministic frames). A dismissal drops the offset
/// outright: the subtree is leaving, and the presence EXIT replays it from wherever the drag left it.
/// <para>
/// The offset lives on the gesture node's position (<see cref="NodeStates"/>), so a node drawn at its
/// place next starts at rest; this clock moves it, by the node's element, and says whether a glide
/// back is still running in the frame.
/// </para>
/// </summary>
public sealed class DragClock
{
    /// <summary>True while any released drag is still gliding back — the host keeps scheduling
    /// frames until every one lands. Reset by <see cref="BeginFrame"/>.</summary>
    public bool AnyActive { get; private set; }

    public void BeginFrame() => AnyActive = false;

    /// <summary>
    /// The pointer moved: the subtree follows raw. SIGNED — a swipe-to-reveal travels negative and a
    /// pull-to-refresh positive, and clamping to zero here would have made one of them impossible.
    /// The CALLER's Min/Max clamp it; this clock only remembers.
    /// </summary>
    public void Drag(Element at, float offset)
    {
        if (NodeStates.For<DragState>(at) is not { } drag) return;
        drag.Gliding = false;
        drag.Dragging = true;
        drag.Offset = offset;
    }

    /// <summary>
    /// The finger lifted: glide from where it was left to where it should REST. That target is the
    /// caller's answer, not zero — a row that stays open after a swipe rests at -96, and gliding it
    /// back to zero would undo the gesture the moment it succeeded.
    /// </summary>
    public void Release(Element at, float timeMs, float restOffset = 0f)
    {
        if (NodeStates.Find<DragState>(at) is not { Dragging: true } drag) return;
        drag.Dragging = false;
        if (Math.Abs(drag.Offset - restOffset) <= 0.01f) return;
        drag.Gliding = true;
        drag.From = drag.Offset;
        drag.To = restOffset;
        drag.StartMs = timeMs;
    }

    /// <summary>Dismissed: nothing holds the subtree and nothing glides it, from now.</summary>
    public void Drop(Element at)
    {
        if (NodeStates.Find<DragState>(at) is not { } drag) return;
        drag.Dragging = false;
        drag.Gliding = false;
    }

    /// <summary>
    /// The offset to PAINT WITH this frame. <paramref name="restOffset"/> is where the subtree sits
    /// when nothing is happening — the caller's state, which is why it is asked for every frame
    /// rather than remembered here.
    /// </summary>
    public float Resolve(Element? at, float timeMs, float restOffset = 0f)
    {
        if (NodeStates.Find<DragState>(at) is not { } drag) return restOffset;
        if (drag.Dragging) return drag.Offset;
        if (!drag.Gliding) return restOffset;

        var t = Math.Clamp((timeMs - drag.StartMs) / Motion.BaseMs, 0f, 1f);
        if (t >= 1f)
        {
            drag.Gliding = false;
            return drag.To;
        }

        AnyActive = true;
        var eased = t * t * (3f - 2f * t); // smoothstep — the standard-curve stand-in
        return drag.From + (drag.To - drag.From) * eased;
    }
}
