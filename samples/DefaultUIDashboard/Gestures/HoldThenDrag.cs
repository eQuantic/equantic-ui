using eQuantic.UI.Primitives;

namespace eQuantic.Console;

/// <summary>
/// A long press that then drags (#1563): the dashboard's own gesture, which the catalog does not ship. It takes a
/// pointer that held still for <see cref="ContextTarget.HoldMs"/>, the platforms' long press, and lets go of one that
/// travelled past <see cref="Touch.PressCancelSlop"/> first, which is a scroll's or a swipe's. Once held, every move
/// reports how far the pointer is from where it went down, and so does the release.
/// <para>
/// Written as an app writes it, in C# alone: the browser runs its transpiled twin and Photon this class, and both
/// hand it the same pointer through the same arena.
/// </para>
/// </summary>
public sealed class HoldThenDrag : GestureRecognizer
{
    /// <summary>The pointer it follows, or null while it follows none.</summary>
    private int? _pointer;

    /// <summary>Where that pointer went down, in the detector's box.</summary>
    private Point _start;

    /// <summary>Whether the pointer held still for the whole deadline.</summary>
    private bool _heldLongEnough;

    /// <summary>Whether the arena gave it the pointer.</summary>
    private bool _won;

    /// <summary>Whether the hold was reported, so the moves that follow are a drag.</summary>
    private bool _dragging;

    /// <summary>The pointer held still long enough and the gesture is this one's: what is under it lifts.</summary>
    public Action? OnHeld { get; set; }

    /// <summary>The held pointer moved, by this offset from where it went down.</summary>
    public Action<Point>? OnMoved { get; set; }

    /// <summary>The held pointer lifted, this far from where it went down.</summary>
    public Action<Point>? OnReleased { get; set; }

    /// <summary>The gesture ended without a release that lands: the pointer was taken away, or another gesture won
    /// it after all.</summary>
    public Action? OnCancelled { get; set; }

    /// <summary>One pointer at a time, and only the primary button: a secondary click asks for a menu.</summary>
    public override bool IsPointerAllowed(PointerEvent down) =>
        _pointer is null && (down.Buttons & PointerButtons.Primary) != 0;

    protected override void AddAllowedPointer(PointerEvent down)
    {
        _pointer = down.Pointer;
        _start = down.Position;
        _heldLongEnough = false;
        _won = false;
        _dragging = false;
        // Named in full: in an app the SDK puts the `ContextTarget` factory in scope, which hides the type by its simple name.
        StartDeadline(eQuantic.UI.Primitives.ContextTarget.HoldMs);
    }

    protected override void HandleEvent(PointerEvent ev)
    {
        if (ev.Pointer != _pointer) return;
        var offset = new Point(ev.Position.X - _start.X, ev.Position.Y - _start.Y);
        switch (ev.Phase)
        {
            case PointerPhase.Move when _dragging:
                OnMoved?.Invoke(offset);
                break;
            case PointerPhase.Move when !_heldLongEnough && offset.Length() > Touch.PressCancelSlop:
                // It travelled before the hold: a scroll's or a swipe's, never this gesture.
                Resolve(GestureDisposition.Rejected);
                break;
            case PointerPhase.Up:
                if (_dragging) OnReleased?.Invoke(offset);
                Finish();
                break;
            case PointerPhase.Cancel:
                if (_dragging) OnCancelled?.Invoke();
                Finish();
                break;
        }
    }

    public override void DidExceedDeadline(int pointer)
    {
        if (pointer != _pointer) return;
        _heldLongEnough = true;
        // A pointer the arena already gave it (it was alone under the press) lifts now; otherwise it asks for it.
        if (_won) Lift();
        else Resolve(GestureDisposition.Accepted);
    }

    public override void AcceptGesture(int pointer)
    {
        if (pointer != _pointer) return;
        _won = true;
        if (_heldLongEnough) Lift();
    }

    public override void RejectGesture(int pointer)
    {
        if (pointer != _pointer) return;
        if (_dragging) OnCancelled?.Invoke();
        Finish();
    }

    /// <summary>The hold is this gesture's: what is under the pointer lifts, and the moves drag it.</summary>
    private void Lift()
    {
        if (_dragging) return;
        _dragging = true;
        OnHeld?.Invoke();
    }

    /// <summary>The pointer ended, or is no longer this gesture's.</summary>
    private void Finish()
    {
        _pointer = null;
        _dragging = false;
    }
}
