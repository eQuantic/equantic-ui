using System.Diagnostics;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Any subtree hears the raw pointer (#1562): the listeners under a pointer hear it, by the frame's pointer targets
/// and the rule both targets share (<see cref="ListenerHits"/>), from the presses, moves, cancels and scrolls the
/// shells already deliver. A listener only listens, so everything here runs BESIDE the press, drag and scroll
/// routing and changes none of it.
/// </summary>
public sealed partial class PhotonHost
{
    /// <summary>The one pointer Photon's shells report: two fingers are a gesture of the shell's, not two pointers.</summary>
    private const int TheOnePointer = 1;

    /// <summary>The listeners a press went down on, followed to its up or its cancel wherever it goes, by their
    /// position in the tree; null when no press is followed.</summary>
    private List<Element>? _listening;

    /// <summary>The buttons the followed press holds, which every move reports.</summary>
    private PointerButtons _listenedButtons;

    /// <summary>The device of the followed press.</summary>
    private PointerKind _listenedKind;

    /// <summary>Where the followed press was last, which its cancel reports.</summary>
    private Point _listenedAt;

    /// <summary>The host's own monotonic clock, which times a pointer event a shell hands no time with (#1563).</summary>
    private readonly long _pointerClockStart = Stopwatch.GetTimestamp();

    /// <summary>When a pointer event was seen, in milliseconds: the shell's own time when it hands one, the host's
    /// monotonic clock when it does not.</summary>
    private double PointerTime(double? shellMs) =>
        shellMs ?? Stopwatch.GetElapsedTime(_pointerClockStart).TotalMilliseconds;

    /// <summary>A press went down at <paramref name="point"/>: the listeners under it hear it, and follow it.</summary>
    private void HearDown(Point point, PointerKind kind, PointerButtons buttons, KeyModifiers modifiers, double timeMs)
    {
        _listening = null;
        if (_lastFrame is not { PointerTargets: { Count: > 0 } targets }) return;
        var heard = HeardAt(targets, point);
        // A detector under the press is hit by the same rule and offers it to its recognizers instead (#1563).
        heard.RemoveAll(index => targets[index].Listener is null);
        if (heard.Count == 0) return;
        _listening = new List<Element>(heard.Count);
        _listenedButtons = buttons;
        _listenedKind = kind;
        _listenedAt = point;
        foreach (var index in heard)
        {
            if (targets[index].Element is { } element) _listening.Add(element);
            Tell(targets[index], PointerPhase.Down, point, kind, buttons, modifiers, default, timeMs);
        }
    }

    /// <summary>The pointer moved: the followed press tells the listeners it went down on, and a mouse or a pen with
    /// nothing pressed hovers the ones under it. A finger never hovers.</summary>
    private void HearMove(Point point, PointerKind kind, KeyModifiers modifiers, double timeMs)
    {
        if (_listening is { } followed)
        {
            _listenedAt = point;
            TellFollowed(followed, PointerPhase.Move, point, _listenedKind, _listenedButtons, modifiers, timeMs);
            return;
        }
        // A press that went down outside every listener is held, and followed by none of them: no hover either.
        if (kind == PointerKind.Touch || _held || _lastFrame is not { PointerTargets: { Count: > 0 } targets }) return;
        foreach (var index in HeardAt(targets, point))
            Tell(targets[index], PointerPhase.Hover, point, kind, PointerButtons.None, modifiers, default, timeMs);
    }

    /// <summary>The followed press lifted at <paramref name="point"/> with <paramref name="modifiers"/> held: an up,
    /// with no button held any more.</summary>
    private void HearUp(Point point, KeyModifiers modifiers, double timeMs)
    {
        if (_listening is not { } followed) return;
        _listening = null;
        TellFollowed(followed, PointerPhase.Up, point, _listenedKind, PointerButtons.None, modifiers, timeMs);
    }

    /// <summary>The system took the followed press away: a cancel, where it was last.</summary>
    private void HearCancel(double timeMs)
    {
        if (_listening is not { } followed) return;
        _listening = null;
        TellFollowed(followed, PointerPhase.Cancel, _listenedAt, _listenedKind, PointerButtons.None, _pointerModifiers, timeMs);
    }

    /// <summary>A wheel or a trackpad scrolled over <paramref name="point"/> by (<paramref name="across"/>,
    /// <paramref name="down"/>) dp toward the content's end.</summary>
    private void HearScroll(Point point, float across, float down, KeyModifiers modifiers, double timeMs)
    {
        if (_lastFrame is not { PointerTargets: { Count: > 0 } targets }) return;
        foreach (var index in HeardAt(targets, point))
            Tell(targets[index], PointerPhase.Scroll, point, PointerKind.Mouse, PointerButtons.None, modifiers,
                new Point(across, down), timeMs);
    }

    /// <summary>The listeners and the gesture detectors of <paramref name="targets"/> that hear a pointer at
    /// <paramref name="point"/>, as indices, the latest painted first: one hit test for both, as Flutter's is one
    /// for every render object, so an opaque detector hides a listener behind it and the other way round.</summary>
    private static List<int> HeardAt(IReadOnlyList<PointerTarget> targets, Point point)
    {
        var under = new List<ListenerHits.Under>();
        var indices = new List<int>();
        for (var i = 0; i < targets.Count; i++)
        {
            if (!targets[i].Visible.Contains(point)) continue;
            under.Add(new ListenerHits.Under(i, targets[i].End, targets[i].Solid, targets[i].Behavior));
            indices.Add(i);
        }
        var heard = ListenerHits.Hear(under);
        for (var i = 0; i < heard.Count; i++) heard[i] = indices[heard[i]];
        return heard;
    }

    /// <summary>Tells the followed listeners, each found in the current frame by its position, so a rebuilt
    /// listener is told through its current callbacks, at the point in its current box. One the frame no longer
    /// draws hears nothing.</summary>
    private void TellFollowed(List<Element> followed, PointerPhase phase, Point point, PointerKind kind,
        PointerButtons buttons, KeyModifiers modifiers, double timeMs)
    {
        if (_lastFrame is not { PointerTargets: { Count: > 0 } targets }) return;
        foreach (var element in followed)
        {
            for (var i = 0; i < targets.Count; i++)
            {
                if (!ReferenceEquals(targets[i].Element, element) || targets[i].Listener is null) continue;
                Tell(targets[i], phase, point, kind, buttons, modifiers, default, timeMs);
                break;
            }
        }
    }

    private static void Tell(in PointerTarget target, PointerPhase phase, Point point, PointerKind kind,
        PointerButtons buttons, KeyModifiers modifiers, Point scrollDelta, double timeMs)
    {
        if (target.Listener is not { } listener) return;
        var heard = phase switch
        {
            PointerPhase.Down => listener.OnPointerDown,
            PointerPhase.Move => listener.OnPointerMove,
            PointerPhase.Up => listener.OnPointerUp,
            PointerPhase.Cancel => listener.OnPointerCancel,
            PointerPhase.Hover => listener.OnPointerHover,
            PointerPhase.Scroll => listener.OnPointerScroll,
            _ => null,
        };
        heard?.Invoke(new PointerEvent(phase,
            new Point(point.X - target.Bounds.X, point.Y - target.Bounds.Y),
            kind, buttons, modifiers, TheOnePointer, scrollDelta, timeMs));
    }
}
