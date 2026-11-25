namespace eQuantic.UI.Native.Components;

/// <summary>
/// What a <see cref="Primitives.LiveRegion"/>'s position keeps for the announcer: what the region
/// said the last frame it was drawn, and when it last spoke. Strings and a time ONLY, because a mark's
/// node belongs to a frame that is recycled the moment the next one lands.
/// <para>
/// A position the HOST speaks through keeps it too, with the words the host has it say
/// (<see cref="Words"/>): a <see cref="Primitives.Sortable"/> saying its keyboard's steps (#1564). The two
/// halves share one state because a position keeps one, and the announcer reads this kind on every region.
/// </para>
/// </summary>
internal sealed class LiveRegionState
{
    /// <summary>What the host has this position say, for a region whose words are the host's rather than its
    /// node's: the last step of a sortable list's keyboard carry. Null for every other region.</summary>
    public string? Words;

    /// <summary>Whether a frame has seen the region yet: a region seen for the first time is news.</summary>
    public bool Known;

    /// <summary>What the region last said, as a reader would say it, or the empty text it was emptied to.</summary>
    public string? Said;

    /// <summary>When it last spoke, or null while it never has.</summary>
    public float? SpokeAtMs;

    /// <summary>Whether it changed inside its quiet window, so its text is owed when the window ends.</summary>
    public bool Pending;
}
