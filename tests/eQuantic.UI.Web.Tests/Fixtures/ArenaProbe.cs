using eQuantic.UI.Primitives;

namespace eQuantic.UI.Web.Tests.Fixtures;

/// <summary>
/// A recognizer written as an app writes one (#1563), which writes down every call the router and the arena make
/// of it: compiled here for <see cref="GestureArenaFixtureTests"/>, and transpiled by eqc into the runtime's
/// <c>shared/__fixtures__/</c>, where <c>gesture-arena.spec.ts</c> drives its twin through the same cases. It
/// keeps fields, a nullable delegate and a nullable disposition, overrides every virtual and the abstract member,
/// and asks for a deadline, resolves and stops tracking from inside them.
/// </summary>
public sealed class ArenaProbe : GestureRecognizer
{
    private readonly string _name;
    private readonly List<string> _heard;
    private readonly double _deadlineMs;
    private readonly GestureDisposition? _atDeadline;
    private readonly GestureDisposition? _onAdd;
    private readonly bool _primaryOnly;
    private GestureDisposition? _onMove;

    /// <param name="name">How it signs what it hears.</param>
    /// <param name="heard">Where it writes what it hears.</param>
    /// <param name="deadlineMs">The deadline it asks for when it joins a pointer's arena, or 0 for none.</param>
    /// <param name="atDeadline">What it says when the deadline passes, or nothing.</param>
    /// <param name="onAdd">What it says as soon as it joins, or nothing.</param>
    /// <param name="onMove">What it says on its first move, or nothing.</param>
    /// <param name="primaryOnly">Whether it allows only a pointer that holds the primary button.</param>
    public ArenaProbe(string name, List<string> heard, double deadlineMs = 0, GestureDisposition? atDeadline = null,
        GestureDisposition? onAdd = null, GestureDisposition? onMove = null, bool primaryOnly = false)
    {
        _name = name;
        _heard = heard;
        _deadlineMs = deadlineMs;
        _atDeadline = atDeadline;
        _onAdd = onAdd;
        _onMove = onMove;
        _primaryOnly = primaryOnly;
    }

    /// <summary>What the latest build asked to hear when the deadline passes: the callback a rebuild hands the kept
    /// recognizer.</summary>
    public Action<string>? OnDeadline { get; set; }

    public override bool IsPointerAllowed(PointerEvent down) =>
        !_primaryOnly || (down.Buttons & PointerButtons.Primary) != 0;

    protected override void AddAllowedPointer(PointerEvent down)
    {
        Write($"add {down.Pointer} {down.Position.X},{down.Position.Y}");
        if (_deadlineMs > 0) StartDeadline(_deadlineMs);
        if (_onAdd is { } disposition) Resolve(disposition);
    }

    protected override void HandleEvent(PointerEvent ev)
    {
        Write($"{PhaseOf(ev.Phase)} {ev.Pointer} {ev.Position.X},{ev.Position.Y}");
        if (ev.Phase == PointerPhase.Move && _onMove is { } disposition)
        {
            _onMove = null;
            ResolvePointer(ev.Pointer, disposition);
        }
    }

    public override void AcceptGesture(int pointer) => Write($"accept {pointer}");

    public override void RejectGesture(int pointer) => Write($"reject {pointer}");

    public override void DidExceedDeadline(int pointer)
    {
        Write($"deadline {pointer} tracking {TrackedPointers.Count}");
        OnDeadline?.Invoke(_name);
        if (_atDeadline is { } disposition) Resolve(disposition);
    }

    /// <summary>What a fixture's step asks of it: it asks for every pointer it tracks, or lets every one go.</summary>
    public void Say(GestureDisposition disposition) => Resolve(disposition);

    /// <summary>What a fixture's step asks of it: it hears <paramref name="pointer"/> no more.</summary>
    public void Stop(int pointer) => StopTrackingPointer(pointer);

    private void Write(string what) => _heard.Add($"{_name}.{what}");

    /// <summary>The phase as the fixture spells it, the same words on both sides.</summary>
    private static string PhaseOf(PointerPhase phase) => phase switch
    {
        PointerPhase.Down => "down",
        PointerPhase.Move => "move",
        PointerPhase.Up => "up",
        PointerPhase.Cancel => "cancel",
        _ => "other",
    };
}
