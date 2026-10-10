namespace eQuantic.UI.Primitives;

/// <summary>One grid column track (spec S4): Fixed dp, Flex weight (the CSS <c>fr</c>), Auto
/// (sized by its widest starting item), or an auto-fill track repeated as often as it fits.</summary>
public readonly record struct GridTrack(SizeKind Kind, float Value)
{
    public static GridTrack Fixed(float dp) => new(SizeKind.Fixed, dp);
    public static GridTrack Flex(float weight = 1) => new(SizeKind.Fill, weight);
    public static GridTrack Auto => new(SizeKind.Hug, 0);

    /// <summary>
    /// As many columns as fit tracks at least <paramref name="min"/> dp wide (1 or more), the grid's gap between
    /// them, sharing what is left by <paramref name="weight"/> — CSS's
    /// <c>repeat(auto-fill, minmax(min, 1fr))</c>, the card grid every handoff draws. It is the
    /// grid's whole column list: a grid refuses it beside another track. On a width the grid sizes
    /// from its content there is nothing to divide, and it is one column.
    /// </summary>
    public static GridTrack AutoFill(float min, float weight = 1)
    {
        // At least a dp: a minimum finer than that is a track count no layout means (a grid a few
        // hundred dp wide would ask Photon for millions of tracks), and CSS rounds it to 0px.
        if (!(min >= 1) || !float.IsFinite(min))
            throw new ArgumentOutOfRangeException(nameof(min), "An auto-fill track needs a finite minimum of at least 1dp.");
        // At least 1: the repeated tracks are equal, so a weight only says that they share what is
        // left, and below 1 CSS leaves part of it unused (a lone `0.5fr` is half a row) where
        // Photon's normalised solver would fill it. Infinite is `Infinityfr` and NaN tracks.
        if (!(weight >= 1) || !float.IsFinite(weight))
            throw new ArgumentOutOfRangeException(nameof(weight), "An auto-fill track needs a finite weight of at least 1.");
        return new(SizeKind.Fill, weight) { Min = min, Repeats = true };
    }

    /// <summary>The narrowest an auto-fill track may be, in dp; 0 for every other track.</summary>
    public float Min { get; init; }

    /// <summary>Whether this track repeats as often as it fits (<see cref="AutoFill"/>).</summary>
    public bool Repeats { get; init; }

    /// <summary>N copies of the same track — <c>GridTrack.Repeat(3, GridTrack.Flex())</c>.</summary>
    public static GridTrack[] Repeat(int count, GridTrack track)
    {
        var tracks = new GridTrack[count];
        Array.Fill(tracks, track);
        return tracks;
    }
}
