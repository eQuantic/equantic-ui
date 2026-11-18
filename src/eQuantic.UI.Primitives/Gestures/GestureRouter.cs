namespace eQuantic.UI.Primitives;

/// <summary>
/// What a realizer drives with the pointer, one per host (#1563): Flutter's pointer router and the gesture
/// binding's dispatch beside its arena, written once and run by every target.
/// <list type="bullet">
/// <item><see cref="Recognizers"/>: a detector's recognizers, kept by its position across rebuilds and handed what
/// the latest build wrote, and <see cref="Forget"/> when the position leaves the tree.</item>
/// <item><see cref="Down"/>: a pointer went down; the members under it join its arena in hit order, the innermost
/// first, each recognizer only if it allows the pointer, and the arena closes.</item>
/// <item><see cref="Route"/>: a move, an up or a cancel reaches every recognizer tracking the pointer, in its
/// detector's box; an up then sweeps the arena, and a cancel takes the pointer from every member.</item>
/// <item><see cref="Tick"/> and <see cref="NextDeadlineMs"/>: the recognizers' deadlines, on the realizer's clock.</item>
/// </list>
/// </summary>
public sealed class GestureRouter
{
    /// <summary>The recognizers that joined each pointer's arena, in the order they joined.</summary>
    private readonly Dictionary<int, List<GestureRecognizer>> _routes = new();

    /// <summary>Each detector position's recognizers, by their place in its list.</summary>
    private readonly Dictionary<object, List<GestureRecognizer>> _kept = new();

    /// <summary>The deadlines asked for, in the order they were asked.</summary>
    private readonly List<GestureDeadline> _deadlines = new();

    /// <summary>The arena every pointer's members compete in.</summary>
    public GestureArena Arena { get; } = new();

    /// <summary>
    /// The recognizers of the detector at <paramref name="position"/>, one per place of <paramref name="gestures"/>:
    /// the one the place already keeps when its factory makes that type, a new one otherwise, each handed what this
    /// build wrote. A recognizer a place no longer keeps hears nothing more.
    /// </summary>
    /// <param name="position">What identifies the detector's place in the tree across builds: an element on Photon,
    /// the detector's DOM element on the web.</param>
    /// <param name="gestures">The factories the latest build handed the detector.</param>
    public IReadOnlyList<GestureRecognizer> Recognizers(object position, IReadOnlyList<GestureRecognizerFactory> gestures)
    {
        if (!_kept.TryGetValue(position, out var kept))
        {
            kept = new List<GestureRecognizer>();
            _kept[position] = kept;
        }
        for (var i = 0; i < gestures.Count; i++)
        {
            var factory = gestures[i];
            if (i < kept.Count && !factory.Makes(kept[i]))
            {
                Retire(kept[i]);
                kept[i] = Made(factory, position);
            }
            else if (i >= kept.Count)
            {
                kept.Add(Made(factory, position));
            }
            factory.Initialize(kept[i]);
        }
        while (kept.Count > gestures.Count)
        {
            Retire(kept[kept.Count - 1]);
            kept.RemoveAt(kept.Count - 1);
        }
        return kept;
    }

    /// <summary>The detector at <paramref name="position"/> left the tree: its recognizers let every pointer go and
    /// hear nothing more.</summary>
    public void Forget(object position)
    {
        if (!_kept.TryGetValue(position, out var kept)) return;
        _kept.Remove(position);
        foreach (var recognizer in kept) Retire(recognizer);
    }

    /// <summary>
    /// A pointer went down: each of <paramref name="members"/>, in hit order, the innermost first, joins its arena,
    /// a recognizer only if it allows the pointer (and then it tracks it from now on), and the arena closes. An
    /// arena the same pointer left undecided (an up a shell never delivered) is cancelled first.
    /// </summary>
    /// <param name="down">The down, in the realizer's coordinates.</param>
    /// <param name="members">The recognizers and the SDK's gestures under the down, the innermost first.</param>
    /// <param name="inBoxOf">The down in a detector's box, from the detector's position; without it, a recognizer
    /// hears <paramref name="down"/> as it is.</param>
    /// <returns>The entry of each member, by its place in <paramref name="members"/>, through which a gesture of the
    /// SDK's resolves the pointer; null for a recognizer that did not allow it.</returns>
    public IReadOnlyList<GestureArenaEntry?> Down(PointerEvent down, IReadOnlyList<IGestureArenaMember> members,
        Func<object, PointerEvent>? inBoxOf = null)
    {
        var pointer = down.Pointer;
        if (Arena.IsOpen(pointer) || _routes.ContainsKey(pointer)) End(pointer, cancelled: true);
        var routed = new List<GestureRecognizer>();
        _routes[pointer] = routed;
        var entries = new List<GestureArenaEntry?>(members.Count);
        foreach (var member in members)
        {
            if (member is GestureRecognizer recognizer)
            {
                var local = InBox(recognizer, down, inBoxOf);
                if (recognizer.Retired || !recognizer.IsPointerAllowed(local))
                {
                    entries.Add(null);
                    continue;
                }
                var entry = Arena.Add(pointer, new GestureRecognizerMember(recognizer));
                routed.Add(recognizer);
                entries.Add(entry);
                recognizer.Join(this, entry, local);
            }
            else
            {
                entries.Add(Arena.Add(pointer, member));
            }
        }
        Arena.Close(pointer);
        return entries;
    }

    /// <summary>
    /// A move, an up or a cancel of a pointer: every recognizer tracking it hears it, in its detector's box. After
    /// an up the arena is swept, the first member winning what nothing decided; after a cancel every member loses
    /// the pointer. Either way the pointer is heard no more.
    /// </summary>
    /// <param name="ev">The event, in the realizer's coordinates.</param>
    /// <param name="inBoxOf">The event in a detector's box, from the detector's position.</param>
    public void Route(PointerEvent ev, Func<object, PointerEvent>? inBoxOf = null)
    {
        var pointer = ev.Pointer;
        if (_routes.TryGetValue(pointer, out var routed))
        {
            foreach (var recognizer in new List<GestureRecognizer>(routed))
                if (recognizer.Tracks(pointer)) recognizer.Hear(InBox(recognizer, ev, inBoxOf));
        }
        if (ev.Phase == PointerPhase.Up) End(pointer, cancelled: false);
        else if (ev.Phase == PointerPhase.Cancel) End(pointer, cancelled: true);
    }

    /// <summary>
    /// The realizer's clock reads <paramref name="nowMs"/>: a deadline asked for since the last tick is due its
    /// length from now, and every deadline due by now passes, the earliest first, each one heard by its recognizer.
    /// </summary>
    public void Tick(double nowMs)
    {
        foreach (var deadline in _deadlines) deadline.DueMs ??= nowMs + deadline.Milliseconds;
        while (Earliest() is { } due && due.DueMs <= nowMs)
        {
            _deadlines.Remove(due);
            due.Recognizer.Exceed(due.Pointer);
        }
    }

    /// <summary>When the next deadline is due on the realizer's clock, or null when none is counting: a deadline
    /// asked for since the last tick counts from the next one, which the pointer's own event asks the realizer
    /// for.</summary>
    public double? NextDeadlineMs => Earliest()?.DueMs;

    /// <summary><paramref name="recognizer"/> waits <paramref name="milliseconds"/> for <paramref name="pointer"/>,
    /// in place of any deadline it waited for before.</summary>
    internal void StartDeadline(GestureRecognizer recognizer, int pointer, double milliseconds)
    {
        _deadlines.RemoveAll(deadline => ReferenceEquals(deadline.Recognizer, recognizer));
        _deadlines.Add(new GestureDeadline(recognizer, pointer, milliseconds));
    }

    /// <summary><paramref name="recognizer"/> no longer waits for <paramref name="pointer"/>.</summary>
    internal void DropDeadline(GestureRecognizer recognizer, int pointer) =>
        _deadlines.RemoveAll(deadline => ReferenceEquals(deadline.Recognizer, recognizer) && deadline.Pointer == pointer);

    /// <summary>The counting deadline due first, the earliest asked among equals, or null.</summary>
    private GestureDeadline? Earliest()
    {
        GestureDeadline? earliest = null;
        foreach (var deadline in _deadlines)
            if (deadline.DueMs is { } due && (earliest is null || due < earliest.DueMs)) earliest = deadline;
        return earliest;
    }

    /// <summary>The pointer ended: no recognizer hears it any more, and its arena is swept, or cancelled.</summary>
    private void End(int pointer, bool cancelled)
    {
        if (_routes.TryGetValue(pointer, out var routed))
        {
            _routes.Remove(pointer);
            foreach (var recognizer in routed) recognizer.Ended(pointer);
        }
        if (cancelled) Arena.Cancel(pointer);
        else Arena.Sweep(pointer);
    }

    /// <summary>A new recognizer for a place of <paramref name="position"/>.</summary>
    private static GestureRecognizer Made(GestureRecognizerFactory factory, object position)
    {
        var made = factory.Create();
        made.Position = position;
        return made;
    }

    /// <summary><paramref name="recognizer"/> leaves its place: it lets its pointers go and hears nothing more.</summary>
    private void Retire(GestureRecognizer recognizer)
    {
        recognizer.Retire();
        _deadlines.RemoveAll(deadline => ReferenceEquals(deadline.Recognizer, recognizer));
        foreach (var routed in _routes.Values) routed.Remove(recognizer);
    }

    /// <summary><paramref name="ev"/> in the box of <paramref name="recognizer"/>'s detector.</summary>
    private static PointerEvent InBox(GestureRecognizer recognizer, PointerEvent ev, Func<object, PointerEvent>? inBoxOf) =>
        recognizer.Position is { } position && inBoxOf is not null ? inBoxOf(position) : ev;
}
