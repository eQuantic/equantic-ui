using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// The Photon half of <see cref="Sortable"/> and <see cref="SortableItem"/> (#1537): an item carried inside
/// its list and across the lists of its group, by a pointer and by the keyboard, as the browser's
/// <c>dom/sortable.ts</c> carries it, with the same arithmetic (<see cref="SortPlacement"/>) and the same
/// reports.
/// <para>
/// A mouse or a pen picks an item up past <see cref="SortPlacement.PickUpTravel"/>, so a press that does not
/// travel still presses it, and a finger after holding it still for <see cref="SortPlacement.HoldMs"/>, so a
/// swipe still scrolls the list. The item is dimmed in its place and a copy of it, recorded the first frame it
/// is drawn lifted, is drawn over the frame where the pointer takes it, leaning with the pointer's speed
/// across. Over a list of the same group the place is computed from the items' boxes, offered to the list's
/// guard and reported while it changes; the scroll view under the pointer scrolls near its edge; on release
/// the list it lands in reports the drop. Escape puts it back.
/// </para>
/// <para>
/// With the focus in an item, Space picks it up: the arrows along its list's axis move it a place, the cross
/// arrows to the neighbouring list of its group, Space or Enter drops it, and Escape, Tab or a press puts it
/// back. The host never applies a drop: the lists are the app's, and the next build shows where the item went.
/// </para>
/// </summary>
public sealed partial class PhotonHost
{
    /// <summary>The item being carried, or a pointer's candidate to carry.</summary>
    private SortGesture? _sort;

    /// <summary>The id of an item the keyboard just dropped, whose focus follows it to where the app builds it.</summary>
    private string? _sortRefocus;

    // Reused while an item is carried, so a move allocates no lists.
    private readonly List<float> _sortStarts = [];
    private readonly List<float> _sortExtents = [];
    private readonly List<string> _sortIds = [];
    private readonly List<Element> _sortNested = [];

    // ---- the pointer ------------------------------------------------------------------------------------------

    /// <summary>
    /// A press over an item of an enabled list: a candidate a pointer may pick up. A press in a field inside an
    /// item is the field's.
    /// </summary>
    private void PressSort(Point point, PointerKind kind)
    {
        _sort = null;
        if (_lastFrame is not { SortRegions: { Count: > 0 } regions } || OverField(point)) return;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            if (regions[i].Node is not SortableItem item || !Covers(regions, i, regions[i].Visible, point)) continue;
            var list = ListOf(regions, i);
            if (!item.Enabled || list < 0 || regions[list].Node is not Sortable { Enabled: true } sortable) return;
            _sort = new SortGesture(regions[i].Element, item, regions[list].Element, sortable)
            {
                Kind = kind,
                Start = point,
                Pointer = point,
            };
            // A finger's hold is counted from the frame this asks for: a press carries no time of its own.
            if (kind == PointerKind.Touch) NeedsRender = true;
            return;
        }
    }

    /// <summary>A move, which is the sort's once its item is picked up, and picks it up when it travels far
    /// enough under a mouse or a pen. A finger that travels before its hold is scrolling, and the candidate
    /// ends.</summary>
    private bool MoveSort(Point point)
    {
        if (_sort is not { ByKeyboard: false } sort) return false;
        if (!sort.Active)
        {
            var travel = MathF.Sqrt((point.X - sort.Start.X) * (point.X - sort.Start.X)
                + (point.Y - sort.Start.Y) * (point.Y - sort.Start.Y));
            if (sort.Kind == PointerKind.Touch)
            {
                if (travel > Touch.PressCancelSlop) _sort = null;
                return false;
            }
            if (travel <= SortPlacement.PickUpTravel) return false;
            sort.Pointer = point;
            if (!LiftSort(sort)) return false;
        }
        sort.Pointer = point;
        Lean(sort);
        PlaceSort(sort);
        NeedsRender = true;
        return true;
    }

    /// <summary>A release: an item picked up lands where it was offered, and a candidate that never was
    /// leaves the press to press.</summary>
    private bool ReleaseSort()
    {
        if (_sort is not { ByKeyboard: false } sort) return false;
        _sort = null;
        if (!sort.Active) return false;
        var landed = sort.Landing;
        EndLift(sort);
        landed?.Node.OnDropped(landed.Value.Drop);
        NeedsRender = true;
        return true;
    }

    /// <summary>The system took the pointer: the item goes back.</summary>
    private void CancelPointerSort()
    {
        if (_sort is not { ByKeyboard: false } sort) return;
        _sort = null;
        if (sort.Active) EndLift(sort);
    }

    /// <summary>Picks the item up: the press under it presses nothing, the list under it does not scroll, and
    /// a drag surface under it does not drag.</summary>
    private bool LiftSort(SortGesture sort)
    {
        if (NodeStates.For<SortLiftState>(sort.Item) is not { } lift)
        {
            _sort = null;
            return false;
        }
        lift.Lifted = true;
        lift.Follows = !sort.ByKeyboard;
        lift.Copy = null;
        sort.Lift = lift;
        sort.Active = true;
        sort.MovedAtMs = sort.TiltFromMs = _lastTimeMs;
        sort.TiltFromX = sort.Pointer.X;
        _pressed = null;
        _pressedAt = null;
        _pan = null;
        _panBeside = null;
        _drag = null;
        NeedsRender = true;
        return true;
    }

    /// <summary>The item is put down: dimmed no more, its copy gone, and the list it was over told it left.</summary>
    private void EndLift(SortGesture sort)
    {
        if (sort.Lift is { } lift)
        {
            lift.Lifted = false;
            lift.Copy = null;
        }
        sort.Landing?.Node.OnHovered?.Invoke(null);
        sort.Landing = null;
        NeedsRender = true;
    }

    /// <summary>How far the copy leans: from the pointer's travel across since the last frame that measured it,
    /// over the time between them. Moves between two frames add up, so the speed is the frames', which is the
    /// only clock the host has. Nothing leans when the person asked for less motion.</summary>
    private void Lean(SortGesture sort)
    {
        sort.MovedAtMs = _lastTimeMs;
        if (ReducedMotion || _lastTimeMs <= sort.TiltFromMs) return;
        if (SortPlacement.Tilt(sort.Pointer.X - sort.TiltFromX, _lastTimeMs - sort.TiltFromMs) is { } angle)
            sort.Lean = angle;
        sort.TiltFromX = sort.Pointer.X;
        sort.TiltFromMs = _lastTimeMs;
    }

    /// <summary>
    /// Where the item would land under the pointer, offered to the list's guard once per place, and reported to
    /// the lists while it changes: the one it leaves hears null, the one it is over hears the place.
    /// </summary>
    private void PlaceSort(SortGesture sort)
    {
        var regions = _lastFrame?.SortRegions ?? Array.Empty<SortRegion>();
        var target = ListUnder(regions, sort);
        (Element List, Sortable Node, SortDrop Drop)? next = null;
        if (target >= 0)
        {
            var list = (Sortable)regions[target].Node;
            var at = regions[target].Element;
            var drop = DropIn(regions, target, sort, sort.Pointer);
            if (sort.Asked is not { } asked || !ReferenceEquals(asked.List, at) || !Same(asked.Drop, drop))
                sort.Asked = (at, drop, list.Accepts?.Invoke(drop) ?? true);
            if (sort.Asked.Value.Accepted) next = (at, list, drop);
        }
        Report(sort, next);
    }

    /// <summary>Tells the lists where the item is now: the one it leaves hears null, the one it lands in hears
    /// the place when it changed.</summary>
    private static void Report(SortGesture sort, (Element List, Sortable Node, SortDrop Drop)? next)
    {
        var was = sort.Landing;
        if (was is { } before && (next is not { } after || !ReferenceEquals(after.List, before.List)))
            before.Node.OnHovered?.Invoke(null);
        if (next is { } now && (was is not { } then || !ReferenceEquals(then.List, now.List) || !Same(then.Drop, now.Drop)))
            now.Node.OnHovered?.Invoke(now.Drop);
        sort.Landing = next;
    }

    /// <summary>
    /// The list under the pointer that takes the item: the deepest one shown there of its group, or with no
    /// group its own list alone, as <see cref="Sortable.Group"/> promises; never one inside the carried item,
    /// which would put it inside itself. -1 when none.
    /// </summary>
    private int ListUnder(IReadOnlyList<SortRegion> regions, SortGesture sort)
    {
        var group = sort.SourceNode.Group;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            if (regions[i].Node is not Sortable { Enabled: true } list) continue;
            var at = regions[i].Element;
            if (ReferenceEquals(at, sort.Item) || IsUnder(at, sort.Item)) continue;
            if (group.Length == 0 ? !ReferenceEquals(at, sort.Source) : list.Group != group) continue;
            if (Covers(regions, i, regions[i].Visible, sort.Pointer)) return i;
        }
        return -1;
    }

    /// <summary>Where the item lands in the <paramref name="list"/>th region, by the arithmetic every realizer
    /// shares: this reads the other items' boxes along the list's axis and hands them over.</summary>
    private SortDrop DropIn(IReadOnlyList<SortRegion> regions, int list, SortGesture sort, Point point)
    {
        var node = (Sortable)regions[list].Node;
        var horizontal = node.Axis == DragAxis.Horizontal;
        ItemsOf(regions, list, sort.Item);
        var (index, into) = SortPlacement.Place(_sortStarts, _sortExtents, horizontal ? point.X : point.Y, node.Nests);
        return new SortDrop(sort.ItemNode.Id, sort.SourceNode.Id, node.Id, index)
        {
            Into = into >= 0 ? _sortIds[into] : null,
        };
    }

    /// <summary>
    /// The <paramref name="list"/>th region's own items, the carried one left out, into the reused lists: their
    /// starts and extents along its axis and their ids, in order. An item of a list nested inside it (a tree's
    /// children) belongs to that list, not this one.
    /// </summary>
    private void ItemsOf(IReadOnlyList<SortRegion> regions, int list, Element carried)
    {
        _sortStarts.Clear();
        _sortExtents.Clear();
        _sortIds.Clear();
        _sortNested.Clear();
        var owner = regions[list].Element;
        var horizontal = ((Sortable)regions[list].Node).Axis == DragAxis.Horizontal;
        for (var i = list + 1; i < regions.Count; i++)
        {
            var at = regions[i].Element;
            if (!IsUnder(at, owner) || InsideAny(at, _sortNested)) continue;
            if (regions[i].Node is Sortable)
            {
                _sortNested.Add(at);
                continue;
            }
            if (ReferenceEquals(at, carried) || regions[i].Node is not SortableItem item) continue;
            var box = regions[i].Bounds;
            _sortStarts.Add(horizontal ? box.X : box.Y);
            _sortExtents.Add(horizontal ? box.Width : box.Height);
            _sortIds.Add(item.Id);
        }
    }

    private static bool InsideAny(Element at, List<Element> lists)
    {
        for (var i = 0; i < lists.Count; i++)
        {
            if (IsUnder(at, lists[i])) return true;
        }
        return false;
    }

    /// <summary>The list an item belongs to: the nearest one above it, which in tree order is the last list
    /// before it that holds it. -1 for an item no list holds.</summary>
    private static int ListOf(IReadOnlyList<SortRegion> regions, int item)
    {
        for (var i = item - 1; i >= 0; i--)
        {
            if (regions[i].Node is Sortable && IsUnder(regions[item].Element, regions[i].Element)) return i;
        }
        return -1;
    }

    private static bool Same(SortDrop a, SortDrop b) => a.To == b.To && a.Index == b.Index && a.Into == b.Into;

    /// <summary>Whether a press lands in something that takes the pointer itself: a field, a code surface or a
    /// spreadsheet inside an item is pressed, not carried.</summary>
    private bool OverField(Point point)
    {
        if (_lastFrame is not { } frame) return false;
        var fields = frame.TextRegions;
        for (var i = 0; i < fields.Count; i++)
        {
            if (Covers(fields, i, fields[i].Bounds, point)) return true;
        }
        var codes = frame.CodeRegions;
        for (var i = 0; i < codes.Count; i++)
        {
            if (Covers(codes, i, codes[i].Bounds, point)) return true;
        }
        var sheets = frame.SheetRegions;
        for (var i = 0; i < sheets.Count; i++)
        {
            if (Covers(sheets, i, sheets[i].Bounds, point)) return true;
        }
        return false;
    }

    // ---- the frame --------------------------------------------------------------------------------------------

    /// <summary>
    /// Before a frame is realized: a finger's hold is counted and picks the item up once it is long enough;
    /// the copy's lean eases toward what the pointer asks, and stands up again once the pointer stopped; and
    /// the scroll view under the pointer scrolls near its edge. Answers whether it scrolled, so the place is
    /// computed again over the frame that shows what the scroll brought in.
    /// </summary>
    private bool TickSort(float timeMs)
    {
        if (_sort is not { ByKeyboard: false } sort) return false;
        if (!sort.Active)
        {
            if (sort.Kind != PointerKind.Touch) return false;
            sort.PressedAtMs ??= timeMs;
            if (timeMs - sort.PressedAtMs < SortPlacement.HoldMs || !LiftSort(sort)) return false;
            PlaceSort(sort);
        }

        var elapsed = sort.TickedAtMs is { } last ? MathF.Max(0, timeMs - last) : 0;
        sort.TickedAtMs = timeMs;
        var lean = ReducedMotion || timeMs - sort.MovedAtMs > SortPlacement.TiltRestMs ? 0 : sort.Lean;
        // The web eases each change of the lean over a tenth of a second; a decay of a twentieth reaches as far.
        sort.Angle += (lean - sort.Angle) * (1 - MathF.Exp(-elapsed / 50f));
        if (MathF.Abs(lean - sort.Angle) < 0.05f) sort.Angle = lean;

        return ScrollUnderSort(sort, elapsed);
    }

    /// <summary>The scroll view nearest the list the item is over scrolls when the pointer is near its
    /// edge, by <see cref="SortPlacement.EdgeScroll"/> a frame, counted in frames of a sixtieth of a second so a
    /// faster display does not scroll faster.</summary>
    private bool ScrollUnderSort(SortGesture sort, float elapsedMs)
    {
        if (_lastFrame is not { } frame) return false;
        var over = sort.Landing?.List ?? sort.Source;
        var scrolls = frame.ScrollRegions;
        for (var i = scrolls.Count - 1; i >= 0; i--)
        {
            var region = scrolls[i];
            if (region.MaxOffset <= 0 || !Scrolls(region.Element, over)) continue;
            var horizontal = region.Axis == ScrollAxis.Horizontal;
            var box = region.Bounds;
            var step = horizontal
                ? SortPlacement.EdgeScroll(sort.Pointer.X, box.Left, box.Right)
                : SortPlacement.EdgeScroll(sort.Pointer.Y, box.Top, box.Bottom);
            if (step == 0) return false;
            var frames = elapsedMs <= 0 ? 1 : Math.Clamp(elapsedMs / (1000f / 60f), 0, 4);
            var offset = ScrollClock.OffsetOf(region.Element) ?? region.Fallback;
            return _scrolls.ScrollTo(region.Element, offset + step * frames, region.MaxOffset);
        }
        return false;
    }

    /// <summary>Whether a scroll view scrolls a list: one around it, or the list's own child, the element the
    /// web marks as the list, which scrolls it as <c>dom/sortable.ts</c> finds its scroller at or above it.</summary>
    private static bool Scrolls(Element scroll, Element list) =>
        ReferenceEquals(scroll.Parent, list) || IsUnder(list, scroll);

    /// <summary>After a frame is realized: the place again when the scroll moved the items, the copy over
    /// everything the frame drew, and a keyboard's dropped item given back its focus.</summary>
    private void PaintSort(DisplayListBuilder builder, bool scrolled)
    {
        if (_sortRefocus is { } id)
        {
            _sortRefocus = null;
            RefocusSorted(id);
        }
        if (_sort is not { Active: true, ByKeyboard: false } sort) return;
        if (scrolled) PlaceSort(sort);
        if (sort.Lift is not { Copy: { } copy } lift) return;
        var dx = (sort.Pointer.X - sort.Start.X) * RenderScale;
        var dy = (sort.Pointer.Y - sort.Start.Y) * RenderScale;
        var center = lift.Center;
        var offset = sort.Angle == 0
            ? Matrix2D.Translation(dx, dy)
            : Matrix2D.Translation(-center.X, -center.Y) * Matrix2D.Rotation(sort.Angle * MathF.PI / 180f)
                * Matrix2D.Translation(center.X + dx, center.Y + dy);
        builder.PushLayer((float)SortPlacement.CopyOpacity);
        builder.DrawPicture(copy, offset);
        builder.PopLayer();
    }

    /// <summary>Whether the carried item still needs frames of its own: the scroll under it is moving, or the
    /// copy is still leaning.</summary>
    private bool SortWantsFrames(bool scrolled) =>
        _sort is { Active: true, ByKeyboard: false } sort && (scrolled || sort.Angle != 0
            || (sort.Lean != 0 && _lastTimeMs - sort.MovedAtMs <= SortPlacement.TiltRestMs));

    /// <summary>When a finger's hold is due, for a frame then even if nothing else asks for one.</summary>
    private float? SortDueMs() =>
        _sort is { Active: false, ByKeyboard: false, Kind: PointerKind.Touch, PressedAtMs: { } pressedAt }
            ? pressedAt + SortPlacement.HoldMs
            : null;

    // ---- the keyboard -----------------------------------------------------------------------------------------

    /// <summary>
    /// A key while an item is carried: the arrows, Space, Enter, Escape and Tab of a keyboard's carry, and
    /// Escape of a pointer's, which is the carry's alone. Answers whether the key was the carry's.
    /// </summary>
    private bool SortKey(string key)
    {
        if (_sort is not { Active: true } sort) return false;
        if (!sort.ByKeyboard)
        {
            if (key != "Escape") return false;
            _sort = null;
            EndLift(sort);
            return true;
        }
        if (sort.KeyList is not { } at) return false;
        var vertical = at.Node.Axis != DragAxis.Horizontal;
        if (key == (vertical ? "ArrowDown" : "ArrowRight")) MoveKeyed(sort, at.List, at.Node, sort.KeyIndex + 1);
        else if (key == (vertical ? "ArrowUp" : "ArrowLeft")) MoveKeyed(sort, at.List, at.Node, sort.KeyIndex - 1);
        else if (key == (vertical ? "ArrowRight" : "ArrowDown") || key == (vertical ? "ArrowLeft" : "ArrowUp"))
        {
            var step = key == (vertical ? "ArrowRight" : "ArrowDown") ? 1 : -1;
            if (Neighbour(sort, at.List, step) is { } next) MoveKeyed(sort, next.List, next.Node, sort.KeyIndex);
        }
        else if (key is " " or "Space" or "Enter") DropKeyed(sort);
        else if (key == "Escape") CancelSort(sort);
        else if (key == "Tab")
        {
            // The focus is leaving: the item goes back, and Tab goes on.
            CancelSort(sort);
            return false;
        }
        else return false;
        NeedsRender = true;
        return true;
    }

    /// <summary>Space on the focused item, or inside it, picks it up for the keyboard, when its list takes
    /// sorting. Answers whether it did.</summary>
    private bool CarryByKeyboard()
    {
        if (_sort is not null || _focusedAt is not { } focused || _textAt is not null
            || _lastFrame is not { SortRegions: { Count: > 0 } regions }) return false;
        for (var i = regions.Count - 1; i >= 0; i--)
        {
            if (regions[i].Node is not SortableItem item) continue;
            var at = regions[i].Element;
            if (!ReferenceEquals(at, focused) && !IsUnder(focused, at)) continue;
            var list = ListOf(regions, i);
            if (!item.Enabled || list < 0 || regions[list].Node is not Sortable { Enabled: true } sortable) return false;
            var sort = new SortGesture(at, item, regions[list].Element, sortable) { ByKeyboard = true };
            if (!LiftSort(sort)) return false;
            _sort = sort;
            sort.KeyList = (regions[list].Element, sortable);
            sort.KeyIndex = IndexIn(regions, list, at);
            Report(sort, (regions[list].Element, sortable, new SortDrop(item.Id, sortable.Id, sortable.Id, sort.KeyIndex)));
            return true;
        }
        return false;
    }

    /// <summary>The place a key asks for, kept inside the list and offered to its guard; a place it refuses
    /// leaves the item where it is.</summary>
    private void MoveKeyed(SortGesture sort, Element list, Sortable node, int index)
    {
        var count = CountIn(list, sort.Item) + 1;
        var place = SortPlacement.Step(index, 0, count);
        var drop = new SortDrop(sort.ItemNode.Id, sort.SourceNode.Id, node.Id, place);
        if (node.Accepts?.Invoke(drop) == false) return;
        sort.KeyList = (list, node);
        sort.KeyIndex = place;
        Report(sort, (list, node, drop));
    }

    private void DropKeyed(SortGesture sort)
    {
        var landed = sort.Landing;
        _sort = null;
        EndLift(sort);
        if (landed is not { } at) return;
        at.Node.OnDropped(at.Drop);
        _sortRefocus = sort.ItemNode.Id;
    }

    private void CancelSort(SortGesture sort)
    {
        _sort = null;
        EndLift(sort);
    }

    /// <summary>The list before or after <paramref name="list"/> among those the item may be carried to: its
    /// group's, in tree order, or its own list alone.</summary>
    private (Element List, Sortable Node)? Neighbour(SortGesture sort, Element list, int step)
    {
        var group = sort.SourceNode.Group;
        if (group.Length == 0 || _lastFrame is not { } frame) return null;
        var regions = frame.SortRegions;
        var lists = new List<(Element, Sortable)>();
        var current = -1;
        for (var i = 0; i < regions.Count; i++)
        {
            if (regions[i].Node is not Sortable { Enabled: true } node || node.Group != group) continue;
            var at = regions[i].Element;
            if (ReferenceEquals(at, sort.Item) || IsUnder(at, sort.Item)) continue;
            if (ReferenceEquals(at, list)) current = lists.Count;
            lists.Add((at, node));
        }
        var next = current + step;
        return current < 0 || next < 0 || next >= lists.Count ? null : lists[next];
    }

    /// <summary>How many items the list holds, the carried one left out.</summary>
    private int CountIn(Element list, Element carried)
    {
        if (_lastFrame is not { } frame) return 0;
        var regions = frame.SortRegions;
        for (var i = 0; i < regions.Count; i++)
        {
            if (!ReferenceEquals(regions[i].Element, list)) continue;
            ItemsOf(regions, i, carried);
            return _sortIds.Count;
        }
        return 0;
    }

    /// <summary>The place the item stands at among its list's own items.</summary>
    private int IndexIn(IReadOnlyList<SortRegion> regions, int list, Element item)
    {
        var owner = regions[list].Element;
        var index = 0;
        _sortNested.Clear();
        for (var i = list + 1; i < regions.Count; i++)
        {
            var at = regions[i].Element;
            if (!IsUnder(at, owner) || InsideAny(at, _sortNested)) continue;
            if (regions[i].Node is Sortable)
            {
                _sortNested.Add(at);
                continue;
            }
            if (ReferenceEquals(at, item)) return index;
            index++;
        }
        return index;
    }

    /// <summary>The item the keyboard dropped, where the app built it: the focus goes to the first stop inside
    /// it, as the web moves it there, so the next arrow carries on from the same item.</summary>
    private void RefocusSorted(string id)
    {
        if (_lastFrame is not { } frame) return;
        var regions = frame.SortRegions;
        for (var i = 0; i < regions.Count; i++)
        {
            if (regions[i].Node is not SortableItem item || item.Id != id) continue;
            var stops = frame.FocusStops;
            for (var s = 0; s < stops.Count; s++)
            {
                if (!ReferenceEquals(stops[s].Element, regions[i].Element) && !IsUnder(stops[s].Element, regions[i].Element))
                    continue;
                FocusOn(stops[s]);
                _focused = stops[s].Pressable;
                _focusVisible = true;
                NeedsRender = true;
                return;
            }
            return;
        }
    }
}
