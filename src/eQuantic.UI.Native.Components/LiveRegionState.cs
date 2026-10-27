namespace eQuantic.UI.Native.Components;

/// <summary>
/// What a <see cref="Primitives.LiveRegion"/>'s position keeps for the announcer: what the region
/// said the last frame it was drawn, and when it last spoke. Strings and a time ONLY, because a mark's
/// node belongs to a frame that is recycled the moment the next one lands.
/// </summary>
internal sealed class LiveRegionState
{
    /// <summary>Whether a frame has seen the region yet: a region seen for the first time is news.</summary>
    public bool Known;

    /// <summary>What the region last said, as a reader would say it, or the empty text it was emptied to.</summary>
    public string? Said;

    /// <summary>When it last spoke, or null while it never has.</summary>
    public float? SpokeAtMs;

    /// <summary>Whether it changed inside its quiet window, so its text is owed when the window ends.</summary>
    public bool Pending;
}
