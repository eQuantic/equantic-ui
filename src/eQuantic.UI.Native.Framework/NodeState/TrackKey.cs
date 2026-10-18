namespace eQuantic.UI.Native.Framework;

/// <summary>
/// Which of a position's transition tracks: what it glides and, for a member of a list (a custom
/// shadow), its index. A value rather than the string a track was found by before, the node's path and
/// a channel formed again every frame, about twenty-five strings a frame for a box that glides
/// everything, settled or not.
/// </summary>
/// <param name="Channel">What the track glides.</param>
/// <param name="Index">The member of a list it belongs to; zero for everything else.</param>
public readonly record struct TrackKey(GlideChannel Channel, int Index = 0)
{
    /// <summary>A channel that is not a member of a list is its own key.</summary>
    public static implicit operator TrackKey(GlideChannel channel) => new(channel);
}
