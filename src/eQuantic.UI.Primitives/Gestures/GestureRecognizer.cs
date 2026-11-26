namespace eQuantic.UI.Primitives;

/// <summary>
/// A gesture of the app's own, written in C# from the raw pointer (#1563): Flutter's <c>GestureRecognizer</c>. An
/// app subclasses it and hosts it over any node with a <see cref="RawGestureDetector"/>; the same class runs in
/// the browser, transpiled, and on Photon.
/// <para>
/// Each pointer that goes down where its detector is hit is offered to it (<see cref="IsPointerAllowed"/>); a
/// pointer it allows enters the pointer's arena beside the SDK's own gestures under it, and the recognizer then
/// tracks it (<see cref="AddAllowedPointer"/>) and hears its every move, up and cancel, in the detector's box,
/// past the box's edge too (<see cref="HandleEvent"/>). It asks for the pointer
/// (<see cref="Resolve"/> with <see cref="GestureDisposition.Accepted"/>) or lets it go
/// (<see cref="GestureDisposition.Rejected"/>), and hears the outcome (<see cref="AcceptGesture"/>,
/// <see cref="RejectGesture"/>), once per pointer. A pointer it lost is heard no more. A recognizer that waits for
/// a time asks for a deadline (<see cref="StartDeadline"/>) and hears it pass (<see cref="DidExceedDeadline"/>),
/// counted by the realizer's own clock.
/// </para>
/// <para>
/// It owns nothing a realizer must release: its deadline is its router's. A detector keeps one recognizer per
/// place in its list across rebuilds, so state kept in its fields lives as long as the detector's position.
/// </para>
/// </summary>
public abstract class GestureRecognizer : IGestureArenaMember
{
    /// <summary>The entry through which it resolves each pointer it joined the arena of and has not heard the
    /// outcome of yet.</summary>
    private readonly Dictionary<int, GestureArenaEntry> _entries = new();

    /// <summary>The pointers it hears, in the order they went down.</summary>
    private readonly List<int> _tracked = new();

    /// <summary>The router that offered it its pointers, which counts its deadline.</summary>
    private GestureRouter? _router;

    /// <summary>The latest pointer it allowed, which a deadline is for.</summary>
    private int? _primary;

    /// <summary>The detector's position the router keeps it for, or null when it was not made by a router.</summary>
    internal object? Position { get; set; }

    /// <summary>Whether the detector's position dropped it: it hears nothing more.</summary>
    internal bool Retired { get; private set; }

    /// <summary>The pointers it hears, in the order they went down.</summary>
    protected IReadOnlyList<int> TrackedPointers => _tracked;

    /// <summary>Whether it wants the pointer that went down with <paramref name="down"/>, in its detector's box: a
    /// button, a device. A pointer it does not allow never enters the arena for it. Every pointer, by default.</summary>
    public virtual bool IsPointerAllowed(PointerEvent down) => true;

    /// <summary>It joined the arena of the pointer that went down with <paramref name="down"/> and tracks it from
    /// now on: the place to read where it went down and to start a deadline.</summary>
    protected virtual void AddAllowedPointer(PointerEvent down)
    {
    }

    /// <summary>A move, an up or a cancel of a pointer it tracks, in its detector's box: a pointer followed past the
    /// box's edge reads negative or past its size.</summary>
    protected abstract void HandleEvent(PointerEvent ev);

    /// <summary>It won <paramref name="pointer"/>: every other member under it lost it.</summary>
    public virtual void AcceptGesture(int pointer)
    {
    }

    /// <summary>It lost <paramref name="pointer"/>: it hears it no more, and its deadline for it is dropped.</summary>
    public virtual void RejectGesture(int pointer)
    {
    }

    /// <summary>The deadline it asked for with <see cref="StartDeadline"/> passed while it still tracked
    /// <paramref name="pointer"/>.</summary>
    public virtual void DidExceedDeadline(int pointer)
    {
    }

    /// <summary>Asks for every pointer it tracks and has no outcome of (<see cref="GestureDisposition.Accepted"/>),
    /// or lets every one go (<see cref="GestureDisposition.Rejected"/>).</summary>
    protected void Resolve(GestureDisposition disposition)
    {
        var entries = new List<GestureArenaEntry>(_entries.Values);
        foreach (var entry in entries) entry.Resolve(disposition);
    }

    /// <summary>Asks for <paramref name="pointer"/>, or lets it go, alone.</summary>
    protected void ResolvePointer(int pointer, GestureDisposition disposition)
    {
        if (_entries.TryGetValue(pointer, out var entry)) entry.Resolve(disposition);
    }

    /// <summary>It hears <paramref name="pointer"/> no more, and its deadline for it is dropped. It stays in the
    /// pointer's arena until the arena decides.</summary>
    protected void StopTrackingPointer(int pointer)
    {
        _tracked.Remove(pointer);
        _router?.DropDeadline(this, pointer);
    }

    /// <summary>
    /// Asks to hear, through <see cref="DidExceedDeadline"/>, when <paramref name="milliseconds"/> passed for the
    /// latest pointer it allowed, counted by the realizer's clock from the first moment it counts after the ask. One
    /// deadline at a time: a second replaces the first. Dropped when the recognizer loses the pointer or stops
    /// tracking it, and when the pointer ends.
    /// </summary>
    protected void StartDeadline(double milliseconds)
    {
        if (_router is { } router && _primary is { } pointer) router.StartDeadline(this, pointer, milliseconds);
    }

    /// <summary>The router offers it <paramref name="down"/>'s pointer, which it allowed, through
    /// <paramref name="entry"/>.</summary>
    internal void Join(GestureRouter router, GestureArenaEntry entry, PointerEvent down)
    {
        _router = router;
        _entries[down.Pointer] = entry;
        if (!_tracked.Contains(down.Pointer)) _tracked.Add(down.Pointer);
        _primary = down.Pointer;
        AddAllowedPointer(down);
    }

    /// <summary>Whether it hears <paramref name="pointer"/>.</summary>
    internal bool Tracks(int pointer) => !Retired && _tracked.Contains(pointer);

    /// <summary>The router hands it an event of a pointer it tracks.</summary>
    internal void Hear(PointerEvent ev) => HandleEvent(ev);

    /// <summary>Its deadline for <paramref name="pointer"/> passed.</summary>
    internal void Exceed(int pointer)
    {
        if (Tracks(pointer)) DidExceedDeadline(pointer);
    }

    /// <summary>The arena gave it <paramref name="pointer"/>.</summary>
    internal void Won(int pointer)
    {
        _entries.Remove(pointer);
        if (!Retired) AcceptGesture(pointer);
    }

    /// <summary>The arena took <paramref name="pointer"/> from it: it hears it no more.</summary>
    internal void Lost(int pointer)
    {
        _entries.Remove(pointer);
        StopTrackingPointer(pointer);
        if (!Retired) RejectGesture(pointer);
    }

    /// <summary><paramref name="pointer"/> went up or was cancelled: it hears it no more.</summary>
    internal void Ended(int pointer) => StopTrackingPointer(pointer);

    /// <summary>Its detector's position dropped it: it lets every pointer go, without hearing so, and hears nothing
    /// more.</summary>
    internal void Retire()
    {
        Retired = true;
        Resolve(GestureDisposition.Rejected);
        foreach (var pointer in new List<int>(_tracked)) StopTrackingPointer(pointer);
    }
}
