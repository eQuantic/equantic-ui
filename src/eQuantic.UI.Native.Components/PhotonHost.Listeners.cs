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

    /// <summary>A press went down at <paramref name="point"/>: the listeners under it hear it, and follow it.</summary>
    private void HearDown(Point point, PointerKind kind, PointerButtons buttons, KeyModifiers modifiers)
    {
        _listening = null;
        if (_lastFrame is not { PointerTargets: { Count: > 0 } targets }) return;
        var heard = HeardAt(targets, point);
        if (heard.Count == 0) return;
        _listening = new List<Element>(heard.Count);
        _listenedButtons = buttons;
        _listenedKind = kind;
        _listenedAt = point;
        foreach (var index in heard)
        {
            if (targets[index].Element is { } element) _listening.Add(element);
            Tell(targets[index], PointerPhase.Down, point, kind, buttons, modifiers, default);
        }
    }

    /// <summary>The pointer moved: the followed press tells the listeners it went down on, and a mouse or a pen with
    /// nothing pressed hovers the ones under it. A finger never hovers.</summary>
    private void HearMove(Point point, PointerKind kind, KeyModifiers modifiers)
    {
        if (_listening is { } followed)
        {
            _listenedAt = point;
            TellFollowed(followed, PointerPhase.Move, point, _listenedKind, _listenedButtons, modifiers);
            return;
        }
        // A press that went down outside every listener is held, and followed by none of them: no hover either.
        if (kind == PointerKind.Touch || _held || _lastFrame is not { PointerTargets: { Count: > 0 } targets }) return;
        foreach (var index in HeardAt(targets, point))
            Tell(targets[index], PointerPhase.Hover, point, kind, PointerButtons.None, modifiers, default);
    }

    /// <summary>The followed press lifted at <paramref name="point"/> with <paramref name="modifiers"/> held: an up,
    /// with no button held any more.</summary>
    private void HearUp(Point point, KeyModifiers modifiers)
    {
        if (_listening is not { } followed) return;
        _listening = null;
        TellFollowed(followed, PointerPhase.Up, point, _listenedKind, PointerButtons.None, modifiers);
    }

    /// <summary>The system took the followed press away: a cancel, where it was last.</summary>
    private void HearCancel()
    {
        if (_listening is not { } followed) return;
        _listening = null;
        TellFollowed(followed, PointerPhase.Cancel, _listenedAt, _listenedKind, PointerButtons.None, _pointerModifiers);
    }

    /// <summary>A wheel or a trackpad scrolled over <paramref name="point"/> by (<paramref name="across"/>,
    /// <paramref name="down"/>) dp toward the content's end.</summary>
    private void HearScroll(Point point, float across, float down, KeyModifiers modifiers)
    {
        if (_lastFrame is not { PointerTargets: { Count: > 0 } targets }) return;
        foreach (var index in HeardAt(targets, point))
            Tell(targets[index], PointerPhase.Scroll, point, PointerKind.Mouse, PointerButtons.None, modifiers,
                new Point(across, down));
    }

    /// <summary>The listeners of <paramref name="targets"/> that hear a pointer at <paramref name="point"/>, as
    /// indices, the latest painted first.</summary>
    private static List<int> HeardAt(IReadOnlyList<PointerTarget> targets, Point point)
    {
        var under = new List<ListenerHits.Under>();
        var indices = new List<int>();
        for (var i = 0; i < targets.Count; i++)
        {
            if (!targets[i].Visible.Contains(point)) continue;
            under.Add(new ListenerHits.Under(i, targets[i].End, targets[i].Solid, targets[i].Listener?.Behavior));
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
        PointerButtons buttons, KeyModifiers modifiers)
    {
        if (_lastFrame is not { PointerTargets: { Count: > 0 } targets }) return;
        foreach (var element in followed)
        {
            for (var i = 0; i < targets.Count; i++)
            {
                if (!ReferenceEquals(targets[i].Element, element) || targets[i].Listener is null) continue;
                Tell(targets[i], phase, point, kind, buttons, modifiers, default);
                break;
            }
        }
    }

    private static void Tell(in PointerTarget target, PointerPhase phase, Point point, PointerKind kind,
        PointerButtons buttons, KeyModifiers modifiers, Point scrollDelta)
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
            kind, buttons, modifiers, TheOnePointer, scrollDelta));
    }
}
