using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// The arena on Photon (#1563): Flutter's, whole. A pointer that goes down opens an arena whose members are the
/// recognizers of the <see cref="RawGestureDetector"/>s under it (hit by the listeners' rule, <see cref="HeardAt"/>)
/// and the host's own gestures the press armed (<see cref="BuiltInGesture"/>), in hit order, the innermost first;
/// the arena is the vocabulary's <see cref="GestureRouter"/>, the same code the browser runs transpiled.
/// <para>
/// Every built-in keeps the arming it had and asks for the pointer when it arms (<see cref="AskFor"/>); among
/// themselves they resolve exactly as before, because the first to arm still clears the others as it always did and
/// a built-in that loses to another built-in drops nothing more. Only a recognizer's win takes the pointer from them:
/// each drops what it armed, the scroll view's pan included, and the release presses nothing.
/// </para>
/// </summary>
public sealed partial class PhotonHost
{
    /// <summary>The pointer's arena and the recognizers' deadlines, on this host's frame clock.</summary>
    private readonly GestureRouter _router = new();

    /// <summary>Each detector position's recognizers, kept by the router and handed what the latest build wrote, with
    /// the box the detector was last drawn in, which its recognizers hear the pointer in.</summary>
    private readonly Dictionary<Element, (IReadOnlyList<GestureRecognizer> Recognizers, Rect Bounds)> _detectors =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>The host's own gestures in the current pointer's arena. A member of an earlier arena is not here,
    /// and what it hears changes nothing.</summary>
    private readonly List<BuiltInGesture> _gestures = [];

    /// <summary>Whether one of the host's own gestures took the current pointer, or is taking it: the others then
    /// lose it as they always did, with nothing more dropped.</summary>
    private bool _builtInWins;

    /// <summary>Whether a recognizer took the current pointer from the host's gestures: what they armed is gone, and
    /// the release presses nothing and follows no link.</summary>
    private bool _gestureTaken;

    /// <summary>Where the pointer the router follows was last, which its cancel is reported at.</summary>
    private Point _routedAt;

    /// <summary>Whether the router is taking the pointer away from every member, which drops nothing the host's own
    /// cancel does not drop itself.</summary>
    private bool _cancelling;

    /// <summary>
    /// After a frame is realized: every detector it drew keeps its recognizers by its position, each handed what this
    /// build wrote (a place handed a factory of another type gets a new one), and a position that left the tree drops
    /// its recognizers, which let every pointer go.
    /// </summary>
    private void KeepDetectors()
    {
        if (_lastFrame is { PointerTargets: { Count: > 0 } targets })
        {
            foreach (var target in targets)
            {
                if (target is not { Detector: { } detector, Element: { } at }) continue;
                _detectors[at] = (_router.Recognizers(at, detector.Gestures), target.Bounds);
            }
        }
        if (_detectors.Count == 0) return;
        List<Element>? gone = null;
        foreach (var at in _detectors.Keys)
            if (!at.IsActive) (gone ??= []).Add(at);
        if (gone is null) return;
        foreach (var at in gone)
        {
            _detectors.Remove(at);
            _router.Forget(at);
        }
    }

    /// <summary>
    /// The press at <paramref name="point"/> was armed: its arena opens with the recognizers of the detectors under
    /// it and the host's gestures the press armed, the innermost first, and closes. With
    /// <paramref name="recognizersOnly"/> none of the host's gestures joins: a press a canvas took is the canvas's
    /// alone, and a secondary click is none of theirs, so only the recognizers around it compete for it. Answers
    /// whether a recognizer joined.
    /// </summary>
    private bool OpenArena(Point point, PointerKind kind, PointerButtons buttons, KeyModifiers modifiers, double timeMs,
        bool recognizersOnly)
    {
        _gestures.Clear();
        _builtInWins = false;
        _gestureTaken = false;
        if (!recognizersOnly) JoinBuiltIns(kind);
        var detectors = DetectorsAt(point);
        if (detectors.Count == 0 && _gestures.Count == 0) return false;

        var order = new List<(Element? At, IGestureArenaMember Member)>(_gestures.Count + detectors.Count);
        foreach (var gesture in _gestures) order.Add((gesture.At, gesture));
        if (detectors.Count > 0) PlaceDetectors(order, detectors);

        var members = new List<IGestureArenaMember>(order.Count);
        foreach (var (_, member) in order) members.Add(member);
        var down = new PointerEvent(PointerPhase.Down, point, kind, buttons, modifiers, TheOnePointer, Point.Zero, timeMs);
        _routedAt = point;
        var entries = _router.Down(down, members, InBoxOf(down));

        var joined = false;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i] is BuiltInGesture gesture) gesture.Entry = entries[i];
            else if (entries[i] is not null) joined = true;
        }
        // A press on a table's scrollbar is the scroll's from the start.
        if (_tablePress is { Active: true }) AskFor(BuiltInGestureKind.TablePan);
        // A recognizer's deadline counts from the next frame, which the press asks for.
        if (joined) NeedsRender = true;
        return joined;
    }

    /// <summary>The host's gestures the press armed, in the order it armed them.</summary>
    private void JoinBuiltIns(PointerKind kind)
    {
        if (_pressed is not null && _pressedAt is { } pressedAt) Join(BuiltInGestureKind.Press, pressedAt, kind);
        if (_drag is { } drag) Join(BuiltInGestureKind.Drag, drag.Element, kind);
        if (_sort is { ByKeyboard: false } sort) Join(BuiltInGestureKind.Sort, sort.Item, kind);
        if (_contextHold is { } hold) Join(BuiltInGestureKind.ContextHold, hold.Target, kind);
        if (_tablePress is { } table) Join(BuiltInGestureKind.TablePan, table.Element, kind);
        // A pan armed under a table's candidate pan is the table's: it moves by what the table passes on (PassOn).
        else if (_pan is { } pan) Join(BuiltInGestureKind.Pan, pan.Element, kind);
    }

    private void Join(BuiltInGestureKind kind, Element at, PointerKind pointer) =>
        _gestures.Add(new BuiltInGesture(kind, at, pointer, Lost));

    /// <summary>
    /// The detectors under <paramref name="point"/> by the listeners' rule, the innermost and topmost first, each with
    /// its paint order in the frame's targets and its recognizers.
    /// </summary>
    private List<(Element At, int Paint, IReadOnlyList<GestureRecognizer> Recognizers)> DetectorsAt(Point point)
    {
        var found = new List<(Element, int, IReadOnlyList<GestureRecognizer>)>();
        if (_detectors.Count == 0 || _lastFrame is not { PointerTargets: { Count: > 0 } targets }) return found;
        foreach (var index in HeardAt(targets, point))
        {
            if (targets[index] is not { Detector: not null, Element: { } at }) continue;
            if (_detectors.TryGetValue(at, out var kept)) found.Add((at, index, kept.Recognizers));
        }
        return found;
    }

    /// <summary>
    /// Places each detector's recognizers among the host's gestures in hit order: before every gesture of a node
    /// around the detector, after every gesture of a node inside it, and between nodes that do not hold each other,
    /// the one painted later first. A node painted before the frame's first listener or detector lies under it.
    /// </summary>
    private void PlaceDetectors(List<(Element? At, IGestureArenaMember Member)> order,
        List<(Element At, int Paint, IReadOnlyList<GestureRecognizer> Recognizers)> detectors)
    {
        var painted = PaintOrder();
        var from = 0;
        foreach (var (at, paint, recognizers) in detectors)
        {
            var place = from;
            while (place < order.Count && !Before(at, paint, order[place].At, painted)) place++;
            foreach (var recognizer in recognizers) order.Insert(place++, (at, recognizer));
            from = place;
        }
    }

    /// <summary>Whether the detector at <paramref name="detector"/> comes before a member of the node at
    /// <paramref name="other"/> in hit order.</summary>
    private static bool Before(Element detector, int paint, Element? other, Dictionary<Element, int> painted)
    {
        if (other is null || ReferenceEquals(other, detector)) return true;
        if (Encloses(other, detector)) return true;
        if (Encloses(detector, other)) return false;
        return !painted.TryGetValue(other, out var otherPaint) || paint > otherPaint;
    }

    /// <summary>Each position's first place in the frame's targets, which is the order they were painted in.</summary>
    private Dictionary<Element, int> PaintOrder()
    {
        var painted = new Dictionary<Element, int>(ReferenceEqualityComparer.Instance);
        if (_lastFrame is not { PointerTargets: { } targets }) return painted;
        for (var i = 0; i < targets.Count; i++)
            if (targets[i].Element is { } at) painted.TryAdd(at, i);
        return painted;
    }

    /// <summary>An event of the pointer as a detector's recognizers hear it: in the box the detector was last drawn
    /// in, wherever the pointer is.</summary>
    private Func<object, PointerEvent> InBoxOf(PointerEvent ev) => position =>
        position is Element at && _detectors.TryGetValue(at, out var kept)
            ? ev with { Position = new Point(ev.Position.X - kept.Bounds.X, ev.Position.Y - kept.Bounds.Y) }
            : ev;

    /// <summary>A move, an up or a cancel of the pointer reaches the recognizers tracking it, before the host's own
    /// gestures hear it: an up then sweeps the arena, and a cancel takes the pointer from every member.</summary>
    private void Route(PointerPhase phase, Point point, PointerKind kind, PointerButtons buttons, KeyModifiers modifiers,
        double timeMs)
    {
        var ev = new PointerEvent(phase, point, kind, buttons, modifiers, TheOnePointer, Point.Zero, timeMs);
        _routedAt = point;
        _cancelling = phase == PointerPhase.Cancel;
        try
        {
            _router.Route(ev, InBoxOf(ev));
        }
        finally
        {
            _cancelling = false;
        }
    }

    /// <summary>The host's gesture of <paramref name="kind"/> armed: it asks for the pointer, and every other member
    /// loses it, a recognizer that was waiting included.</summary>
    private void AskFor(BuiltInGestureKind kind)
    {
        foreach (var gesture in _gestures)
        {
            if (gesture.Kind != kind || gesture.Lost || gesture.Entry is not { } entry) continue;
            _builtInWins = true;
            entry.Resolve(GestureDisposition.Accepted);
            return;
        }
    }

    /// <summary>
    /// A host's gesture lost the pointer. To another of the host's gestures it drops nothing more: the one that armed
    /// cleared what it always cleared. To a recognizer it drops what it armed, and the release presses nothing.
    /// </summary>
    private void Lost(BuiltInGesture gesture)
    {
        if (!_gestures.Contains(gesture) || _builtInWins || _cancelling) return;
        foreach (var other in _gestures)
            if (other.Won) return;
        _gestureTaken = true;
        NeedsRender = true;
        switch (gesture.Kind)
        {
            case BuiltInGestureKind.Press:
                _pressed = null;
                _pressedAt = null;
                break;
            case BuiltInGestureKind.Drag:
                _drag = null;
                break;
            case BuiltInGestureKind.Sort:
                if (_sort is { Active: false, ByKeyboard: false }) _sort = null;
                break;
            case BuiltInGestureKind.ContextHold:
                _contextHold = null;
                break;
            case BuiltInGestureKind.TablePan:
                if (_tablePress is { Active: false } table)
                {
                    _tablePress = null;
                    table.Viewport.Scroll.Pointer(PointerPhase.Cancel, Point.Zero, gesture.Pointer, _lastTimeMs);
                }
                break;
            case BuiltInGestureKind.Pan:
                _pan = null;
                _panBeside = null;
                break;
        }
    }

    /// <summary>Before a frame: the recognizers' deadlines due by <paramref name="timeMs"/> pass, on the frame
    /// clock, and one asked for since the last frame starts counting from this one.</summary>
    private void TickGestures(float timeMs) => _router.Tick(timeMs);

    /// <summary>When the next recognizer's deadline is due, for a frame then even if nothing else asks for one.</summary>
    private float? GestureDueMs() => _router.NextDeadlineMs is { } due ? (float)due : null;

    /// <summary>
    /// A shell's secondary click, or a Control-click a target answered: a pointer that goes down and up at once, which
    /// the recognizers of the detectors under it are offered as every pointer is, each by its own
    /// <see cref="GestureRecognizer.IsPointerAllowed"/>. None of the host's gestures takes such a click.
    /// </summary>
    private void OfferClick(Point point, PointerKind kind, PointerButtons buttons, KeyModifiers modifiers, double timeMs)
    {
        OpenArena(point, kind, buttons, modifiers, timeMs, recognizersOnly: true);
        Route(PointerPhase.Up, point, kind, PointerButtons.None, modifiers, timeMs);
    }
}
