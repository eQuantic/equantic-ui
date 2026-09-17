namespace eQuantic.UI.Primitives;

/// <summary>One grid column track (spec S4): Fixed dp, Flex weight (the CSS <c>fr</c>), or Auto
/// (sized by its widest starting item).</summary>
public readonly record struct GridTrack(SizeKind Kind, float Value)
{
    public static GridTrack Fixed(float dp) => new(SizeKind.Fixed, dp);
    public static GridTrack Flex(float weight = 1) => new(SizeKind.Fill, weight);
    public static GridTrack Auto => new(SizeKind.Hug, 0);

    /// <summary>N copies of the same track — <c>GridTrack.Repeat(3, GridTrack.Flex())</c>.</summary>
    public static GridTrack[] Repeat(int count, GridTrack track)
    {
        var tracks = new GridTrack[count];
        Array.Fill(tracks, track);
        return tracks;
    }
}
