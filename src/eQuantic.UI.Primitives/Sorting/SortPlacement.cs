namespace eQuantic.UI.Primitives;

/// <summary>
/// The arithmetic of a sort, written ONCE for every realizer (#1535, #1536, #1537): where a carried item
/// lands among the other items of a list, how far its copy leans for the pointer's speed, and where a key
/// moves it. The browser's controller runs its transpiled twin and the Photon host runs this, so the two
/// targets cannot place an item differently. What a realizer does on its own is only what it alone can: read
/// the items' boxes and the pointer.
/// </summary>
public static class SortPlacement
{
    /// <summary>Degrees the copy leans per pixel a millisecond across, eQuantic Space's board's figure.</summary>
    public const float TiltPerSpeed = 8;

    /// <summary>The most the copy leans, either way, in degrees.</summary>
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

    /// <summary>The opacity of an item while it is carried, in its place.</summary>
    public const double LiftedOpacity = 0.4;

    /// <summary>The opacity of the copy that follows the pointer.</summary>
    public const double CopyOpacity = 0.92;

    /// <summary>The copy's shadow: how far down it falls and how far it blurs, in pixels, and how dark it is.</summary>
    public const float CopyShadowOffsetY = 12;

    /// <inheritdoc cref="CopyShadowOffsetY"/>
    public const float CopyShadowBlur = 28;

    /// <inheritdoc cref="CopyShadowOffsetY"/>
    public const double CopyShadowAlpha = 0.28;

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
    /// <see cref="TiltPerSpeed"/> degrees per pixel a millisecond, never past <see cref="TiltMax"/>. Null when no
    /// time passed, which says nothing about a speed.
    /// </summary>
    public static float? Tilt(float across, float milliseconds)
    {
        if (milliseconds <= 0) return null;
        var angle = -(across / milliseconds) * TiltPerSpeed;
        if (angle > TiltMax) return TiltMax;
        if (angle < -TiltMax) return -TiltMax;
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
}
