namespace eQuantic.UI.Primitives;

/// <summary>
/// How hard a <see cref="LiveRegion"/> interrupts — the one axis every platform's live-region API
/// has, under three spellings of the same two values.
/// </summary>
public enum LiveRegionUrgency : byte
{
    /// <summary>
    /// Wait for a pause. The reader finishes what it is saying and announces this next, which is
    /// what a status, a toast or a progress step wants: the user is told, and not interrupted.
    /// </summary>
    Polite,

    /// <summary>
    /// Say it now, cutting off whatever is in progress. Reserved for what the user must hear before
    /// continuing — an error that invalidates what they were doing. Used for anything less, it makes
    /// a screen reader unusable, which is why <see cref="LiveRegion"/> defaults to
    /// <see cref="Polite"/> and a component has to ASK for this one.
    /// </summary>
    Assertive,
}
