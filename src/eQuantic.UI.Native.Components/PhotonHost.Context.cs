using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// The Photon half of <see cref="ContextTarget"/> (#1531): a person asks for the menu of something by a secondary
/// click, which a shell hands over through <see cref="ContextRequest"/>; by a finger held still for
/// <see cref="ContextTarget.HoldMs"/>, which the host counts itself, so every touch shell has the long press; or by
/// the menu key or Shift+F10 with the focus inside the target. The innermost target under the pointer or around the
/// focus answers alone, as the browser's <c>dom/context-request.ts</c> answers it.
/// </summary>
public sealed partial class PhotonHost
{
    /// <summary>A finger pressing a target that may become a long press: where, the frame time its press was first
    /// seen at (a press carries no time of its own), and its region's index in the frame it was found in.</summary>
    private (Point At, float? PressedAtMs)? _contextHold;

    /// <summary>
    /// A shell's secondary click at (<paramref name="x"/>, <paramref name="y"/>): the right button, a Control-click on
    /// a Mac, a pointer's secondary click on a tablet. The innermost <see cref="ContextTarget"/> under the point hears
    /// the request, at the point in its own box. Answers whether one did, so a shell that answers nothing can let
    /// the platform's own menu open. <paramref name="buttons"/> and <paramref name="modifiers"/> are what the shell
    /// saw, which the listeners under the point hear (#1562): a Control-click is the primary button with Control held.
    /// <paramref name="timeMs"/> is when the shell saw the click, in milliseconds on its monotonic clock (#1563);
    /// without it, the host's own clock.
    /// </summary>
    public bool ContextRequest(float x, float y, PointerKind kind = PointerKind.Mouse,
        PointerButtons buttons = PointerButtons.Secondary, KeyModifiers modifiers = KeyModifiers.None,
        double? timeMs = null)
    {
        y += _lift;
        _contextHold = null;
        var point = new Point(x, y);
        var answered = RequestAt(point);
        // The listeners under the click hear it as the button the shell saw, its down and its up at once (#1562). A
        // Control-click nothing answered presses as it always did, and the listeners hear that press instead. A click
        // while a press is followed is a second button on the same pointer, which a browser reports as no new down:
        // the press stays followed.
        if (kind != PointerKind.Touch && _listening is null && (buttons != PointerButtons.Primary || answered))
        {
            var time = PointerTime(timeMs);
            HearDown(point, kind, buttons, modifiers, time);
            // One click: the up holds the keys its down held, at the time its down was seen.
            HearUp(point, modifiers, time);
        }
        // An answered Control-click keeps its button down for the menu, and the shell ends it with PointerCancel rather
        // than a press's up: until then the pointer is held, and its moves hover nothing.
        if (answered && buttons == PointerButtons.Primary) _held = true;
        return answered;
    }

    /// <summary>The request of the innermost enabled target under <paramref name="point"/>, at the point in its box.
    /// A disabled target answers nothing, and no target around it answers in its place.</summary>
    private bool RequestAt(Point point)
    {
        // A field inside a target keeps its own menu: its copy and paste are the field's.
        if (_lastFrame is not { ContextRegions: { Count: > 0 } regions } || OverTextField(point)) return false;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            if (!Covers(regions, i, regions[i].Visible, point)) continue;
            var target = regions[i].Node;
            if (!target.Enabled) return false;
            var turned = TransformedAt(regions, i);
            var at = turned is { } drawn
                ? drawn.Inverse.Transform(point) - new Point(drawn.Local.X, drawn.Local.Y)
                : point - new Point(regions[i].Bounds.X, regions[i].Bounds.Y);
            target.OnRequested(new ContextRequest(at));
            NeedsRender = true;
            return true;
        }
        return false;
    }

    /// <summary>The menu key, or Shift+F10: the request of the innermost enabled target around the focus, with no
    /// point. Answers whether one did.</summary>
    private bool RequestByKeyboard(string key, KeyModifiers modifiers)
    {
        if (!(key == "ContextMenu" && modifiers == KeyModifiers.None) && !(key == "F10" && modifiers == KeyModifiers.Shift))
            return false;
        // A field being edited keeps its own menu, as it keeps the pointer's.
        if (_textAt is not null || _focusedAt is not { } stop
            || _lastFrame is not { ContextRegions: { Count: > 0 } regions })
            return false;
        // A composite keeps the focus on its one stop and names where the keyboard is by its active cell: the
        // target around THAT item answers (#1532), as the web's follows aria-activedescendant.
        var focused = ActiveItemOf(stop) ?? stop;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            var at = regions[i].Element;
            if (!ReferenceEquals(at, focused) && !IsUnder(focused, at)) continue;
            if (!regions[i].Node.Enabled) return false;
            regions[i].Node.OnRequested(new ContextRequest(null));
            NeedsRender = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// The position of the item a focused composite names as active (<see cref="Navigable.ActiveCell"/>): its
    /// row's, then the row's cell of that index among the composite's kind of item (a tree's items, a grid's
    /// cells), counted in tree order as the web numbers them. Null when the focus is not on a composite, or it
    /// names no cell, or the cell is not there.
    /// </summary>
    private Element? ActiveItemOf(Element focused)
    {
        if (focused.Configuration is not Navigable { ActiveCell: { } cell } grid) return null;
        if (focused.ChildAt(cell.Row) is not { } row) return null;
        var role = grid.Role == NavigableRole.Tree ? PressableRole.TreeItem : PressableRole.GridCell;
        var seen = 0;
        Element? found = null;
        void Walk(Element at)
        {
            if (found is not null) return;
            if (at.Configuration is Pressable pressable && pressable.Role == role && seen++ == cell.Item)
            {
                found = at;
                return;
            }
            at.VisitChildren(Walk);
        }
        Walk(row);
        return found ?? row;
    }

    /// <summary>Whether a point lands on a text field, whose own menu a request there leaves alone.</summary>
    private bool OverTextField(Point point)
    {
        if (_lastFrame is not { } frame) return false;
        var fields = frame.TextRegions;
        for (var i = 0; i < fields.Count; i++)
        {
            if (Covers(fields, i, fields[i].Bounds, point)) return true;
        }
        return false;
    }

    /// <summary>A finger or a pen pressing a target: a candidate long press, timed from the frame it asks for.</summary>
    private void PressContext(Point point, PointerKind kind)
    {
        _contextHold = null;
        if (kind == PointerKind.Mouse || _lastFrame is not { ContextRegions: { Count: > 0 } regions }
            || OverTextField(point)) return;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            if (!Covers(regions, i, regions[i].Visible, point)) continue;
            if (!regions[i].Node.Enabled) return;
            _contextHold = (point, null);
            NeedsRender = true;
            return;
        }
    }

    /// <summary>A finger that travels past the slop is scrolling or dragging, and its hold asks for nothing.</summary>
    private void MoveContext(Point point)
    {
        if (_contextHold is not { } hold) return;
        var dx = point.X - hold.At.X;
        var dy = point.Y - hold.At.Y;
        if (dx * dx + dy * dy > Touch.PressCancelSlop * Touch.PressCancelSlop) _contextHold = null;
    }

    /// <summary>
    /// Before a frame: a hold is stamped with the first frame's time, and once it lasted
    /// <see cref="ContextTarget.HoldMs"/> it is a long press. The target under it hears the request, and the press it
    /// began presses nothing on release: the menu was the answer.
    /// </summary>
    private void TickContext(float timeMs)
    {
        if (_contextHold is not { } hold) return;
        // A hold that lifted a sortable item is the lift's (250 ms, before this one's 500): it asks for nothing.
        if (_sort is { Active: true })
        {
            _contextHold = null;
            return;
        }
        if (hold.PressedAtMs is not { } pressedAt)
        {
            _contextHold = hold with { PressedAtMs = timeMs };
            return;
        }
        if (timeMs - pressedAt < ContextTarget.HoldMs) return;
        _contextHold = null;
        if (!RequestAt(hold.At)) return;
        _pressed = null;
        _pressedAt = null;
        _pan = null;
        _drag = null;
        _pressSwallowed = true;
    }

    /// <summary>When a finger's hold is due, for a frame then even if nothing else asks for one.</summary>
    private float? ContextDueMs() =>
        _contextHold is { PressedAtMs: { } pressedAt } ? pressedAt + ContextTarget.HoldMs : null;
}
