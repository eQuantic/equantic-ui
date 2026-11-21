namespace eQuantic.UI.Primitives;

/// <summary>
/// The arithmetic of a sort, written ONCE for every realizer (#1535, #1536, #1537): where a carried item
/// lands among the other items of a list, how far its copy leans for the pointer's speed, where a key
/// moves it, and in a list whose items have depths the depth it takes there and where a reader hears it is (#1594). The browser's controller runs its transpiled twin and the Photon host runs this, so the two
/// targets cannot place an item differently. What a realizer does on its own is only what it alone can: read
/// the items' boxes and the pointer.
/// </summary>
public static class SortPlacement
{
    /// <summary>Degrees the copy leans per pixel a millisecond across, eQuantic Space's board's figure.</summary>
    public const float TiltPerSpeed = 8;

    /// <summary>The most the copy leans, either way, in degrees, unless a theme or a list says otherwise
    /// (<see cref="SortableTheme.MaxLean"/>).</summary>
    public const float TiltMax = 12;

    /// <summary>How long a pointer that stopped keeps the copy leaning, in milliseconds.</summary>
    public const float TiltRestMs = 80;

    /// <summary>The travel, in pixels, that picks an item up under a mouse or a pen: the Space board's own
    /// threshold, short of a press's slop, so a press that does not travel still presses the item.</summary>
    public const float PickUpTravel = 5;

    /// <summary>How long a finger holds an item still to pick it up, in milliseconds: a finger that travels
    /// first is scrolling the list.</summary>
    public const float HoldMs = 250;

    /// <summary>How near a scroller's edge, in pixels, a carried item scrolls it.</summary>
    public const float EdgeZone = 32;

    /// <summary>The fastest a scroller under a carried item scrolls, in pixels a frame, at its very edge.</summary>
    public const float EdgeStep = 14;

    /// <summary>The opacity of an item while it is carried, in its place, unless a theme or a list says otherwise
    /// (<see cref="SortableTheme.LiftedOpacity"/>).</summary>
    public const double LiftedOpacity = 0.4;

    /// <summary>The opacity of the copy that follows the pointer, unless a theme or a list says otherwise
    /// (<see cref="SortableTheme.CopyOpacity"/>).</summary>
    public const double CopyOpacity = 0.92;

    /// <summary>The copy's shadow, a level of the theme's elevation scale, unless a theme or a list says otherwise
    /// (<see cref="SortableTheme.CopyElevation"/>): the level whose offset and blur, 12 and 28, the copy's shadow
    /// had before a theme could set it (#1602).</summary>
    public const int CopyElevation = 4;

    /// <summary>
    /// The look of an item carried from a list (#1602): the <paramref name="theme"/>'s record with the list's
    /// <paramref name="own"/> laid over it, member by member, and the defaults above where neither sets one. Every
    /// member of the answer is set, so a realizer reads it as it is, and each opacity is kept between 0 and 1, which
    /// the browser does by itself and Photon's layers do not.
    /// </summary>
    public static SortableTheme Look(SortableTheme? theme, SortableTheme? own)
    {
        var look = SortableTheme.Merge(theme, own);
        return new SortableTheme
        {
            LiftedOpacity = Opacity(look?.LiftedOpacity ?? (float)LiftedOpacity),
            CopyOpacity = Opacity(look?.CopyOpacity ?? (float)CopyOpacity),
            CopyElevation = look?.CopyElevation ?? CopyElevation,
            MaxLean = look?.MaxLean ?? TiltMax,
        };
    }

    /// <summary>An opacity kept between none and all.</summary>
    private static float Opacity(float value) => Math.Min(1f, Math.Max(0f, value));

    /// <summary>
    /// Where a carried item lands among the OTHER items of a list: before the first whose middle the pointer
    /// has not passed along the list's axis, so the index counts the list without the item, and in a list
    /// that <paramref name="nests"/>, into the item whose middle half the pointer is over.
    /// </summary>
    /// <param name="starts">Each other item's start along the axis, in order.</param>
    /// <param name="extents">Each other item's extent along the axis.</param>
    /// <param name="at">The pointer's position along the axis.</param>
    /// <param name="nests">Whether the list takes an item into another.</param>
    /// <returns>The index it lands at, and the index of the item it lands inside, or -1.</returns>
    public static (int Index, int Into) Place(IReadOnlyList<float> starts, IReadOnlyList<float> extents, float at, bool nests)
    {
        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i];
            var size = extents[i];
            if (nests && at >= start + size / 4 && at <= start + size * 3 / 4) return (i, i);
            if (at < start + size / 2) return (i, -1);
        }
        return (starts.Count, -1);
    }

    /// <summary>
    /// How far the copy leans for the pointer's speed across: against the direction it moves, at
    /// <see cref="TiltPerSpeed"/> degrees per pixel a millisecond, never past <paramref name="most"/> either way
    /// (the look's <see cref="SortableTheme.MaxLean"/>, 0 keeping it upright). Null when no time passed, which says
    /// nothing about a speed.
    /// </summary>
    public static float? Tilt(float across, float milliseconds, float most)
    {
        if (milliseconds <= 0) return null;
        // A copy whose most is nothing stays upright, at 0 and never at the -0 a clamp to -most would give.
        if (most <= 0) return 0;
        var angle = -(across / milliseconds) * TiltPerSpeed;
        if (angle > most) return most;
        if (angle < -most) return -most;
        return angle == 0 ? 0 : angle;
    }

    /// <summary>
    /// How far a scroller under a carried item scrolls this frame along one axis, from where the pointer is
    /// between its <paramref name="start"/> and <paramref name="end"/>: back near the start, on near the
    /// end, faster the nearer the edge, up to <see cref="EdgeStep"/>, and not at all in between.
    /// </summary>
    public static float EdgeScroll(float at, float start, float end)
    {
        if (at < start + EdgeZone) return -MathF.Ceiling((start + EdgeZone - at) / EdgeZone * EdgeStep);
        if (at > end - EdgeZone) return MathF.Ceiling((at - (end - EdgeZone)) / EdgeZone * EdgeStep);
        return 0;
    }

    /// <summary>Where a key moves an item in a list of <paramref name="count"/> places (the item's own
    /// included): one step, kept inside the list.</summary>
    public static int Step(int index, int step, int count) => Math.Max(0, Math.Min(index + step, count - 1));

    /// <summary>
    /// Where a key moves a carried item among the <paramref name="others"/> items of its list (the carried one left
    /// out), from the place it holds: the index it lands at, and the item it lands inside or -1, as
    /// <see cref="Place"/> answers a pointer. In a list that <paramref name="nests"/> the places run before the
    /// first item, inside it, before the second, inside it, and on to after the last (2n + 1 places for n others),
    /// and a step takes the next or the previous; in one that does not, a step moves one index, as
    /// <see cref="Step"/> does. Kept inside the list.
    /// </summary>
    public static (int Index, int Into) KeyStep(int index, int into, int step, int others, bool nests)
    {
        if (!nests) return (Step(index, step, others + 1), -1);
        var place = into >= 0 ? into * 2 + 1 : index * 2;
        place = Math.Max(0, Math.Min(place + step, others * 2));
        return place % 2 == 1 ? ((place - 1) / 2, (place - 1) / 2) : (place / 2, -1);
    }

    // ---- a list whose items have depths (#1594) ------------------------------------------------------------

    /// <summary>Whether a list whose <see cref="Sortable.Indent"/> is <paramref name="indent"/> has items with depths: a
    /// finite indent above 0. Every realizer asks this one rule, so an indent that is not a number, or is infinite,
    /// gives no depths on any target.</summary>
    public static bool HasDepths(float indent) => indent > 0 && float.IsFinite(indent);

    /// <summary>
    /// The depth a pointer asks for in a list whose items have depths: the carried item's own
    /// <paramref name="depth"/>, one deeper for each <paramref name="indent"/> it <paramref name="travelled"/> across the
    /// axis toward its end since it picked the item up (one shallower for each back), rounded to the nearest, as
    /// dnd-kit's sortable tree projects it. Read from the travel rather than from where the pointer is, it needs no
    /// knowledge of where depth 0 starts across the list. A list with no indent asks for the item's own depth.
    /// </summary>
    public static int Wanted(int depth, float travelled, float indent) =>
        HasDepths(indent) ? depth + (int)MathF.Floor(travelled / indent + 0.5f) : depth;

    /// <summary>
    /// The depths a carried item may take at a place of a list whose items have depths, in the order a realizer offers
    /// them to the list's guard. The place lies between the item before it, at depth <paramref name="above"/>, and the
    /// item after it, at depth <paramref name="below"/>, either null at an end of the list: coming before the one after,
    /// the carried item is at least as deep; it may follow the one before or any of its ancestors down to that depth, or
    /// be its child, one deeper. First the depth it <paramref name="wants"/>, kept in that range, then each shallower
    /// one, then each deeper one, so a refused depth gives way to the nearest the place allows, shallower first.
    /// </summary>
    public static IReadOnlyList<int> Depths(int wants, int? above, int? below)
    {
        var (shallowest, deepest) = DepthRange(above, below);
        var first = Math.Max(shallowest, Math.Min(wants, deepest));
        var depths = new List<int> { first };
        for (var depth = first - 1; depth >= shallowest; depth--) depths.Add(depth);
        for (var depth = first + 1; depth <= deepest; depth++) depths.Add(depth);
        return depths;
    }

    /// <summary>The depth a cross arrow asks for at a place of a list whose items have depths: one deeper or one
    /// shallower than <paramref name="depth"/> (<paramref name="step"/> 1 or -1), or <paramref name="depth"/> itself
    /// when the place allows none further that way (the range <see cref="Depths"/> keeps to).</summary>
    public static int KeyDepth(int depth, int step, int? above, int? below)
    {
        var (shallowest, deepest) = DepthRange(above, below);
        var next = depth + step;
        return next < shallowest || next > deepest ? depth : next;
    }

    /// <summary>The shallowest and the deepest depth a place between items at <paramref name="above"/> and
    /// <paramref name="below"/> allows.</summary>
    private static (int Shallowest, int Deepest) DepthRange(int? above, int? below)
    {
        var shallowest = below ?? 0;
        return (shallowest, above is { } before ? Math.Max(before + 1, shallowest) : shallowest);
    }

    /// <summary>
    /// Where a place at a <paramref name="depth"/> stands among the items of a list whose items have depths, for what a
    /// reader hears: <paramref name="depths"/> are the items' depths with the carried item and what it carries left out,
    /// and the place lies before the item at <paramref name="index"/>. The holder is the nearest item before the place
    /// one depth shallower (-1 at depth 0, where the list holds it); the position, from one, and the count, the carried
    /// item included, are among the items at that depth under the same holder. A list whose items are all at depth 0
    /// answers no holder, the index plus one and every item plus one, which is what a list without depths says.
    /// </summary>
    public static (int Holder, int Position, int Count) Siblings(IReadOnlyList<int> depths, int index, int depth)
    {
        var holder = -1;
        var position = 1;
        for (var i = index - 1; i >= 0; i--)
        {
            if (depths[i] < depth)
            {
                if (depths[i] == depth - 1) holder = i;
                break;
            }
            if (depths[i] == depth) position++;
        }
        var count = position;
        for (var i = index; i < depths.Count && depths[i] >= depth; i++)
        {
            if (depths[i] == depth) count++;
        }
        return (holder, position, count);
    }
}
