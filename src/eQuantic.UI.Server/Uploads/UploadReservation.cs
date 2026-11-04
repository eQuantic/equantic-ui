namespace eQuantic.UI.Server;

/// <summary>
/// A place among an owner's unfinished uploads, held from the moment a creation is accepted until its
/// record is written or the request ends. Without it, two creations sent at once each counted the
/// uploads before the other's record existed, and both passed the last place.
/// </summary>
internal sealed class UploadReservation(UploadStore store, string owner) : IDisposable
{
    private int _released;

    /// <summary>
    /// The owner whose place this is, for the store to give back itself, in the step that writes the
    /// record; null when the place was already given back. Either this or <see cref="Dispose"/> gives
    /// it back, never both.
    /// </summary>
    public string? Take() => Interlocked.Exchange(ref _released, 1) == 0 ? owner : null;

    /// <summary>Gives the place back, for a creation that wrote no record; after <see cref="Take"/>, or a
    /// second time, it does nothing.</summary>
    public void Dispose()
    {
        if (Take() is { } held) store.Release(held);
    }
}
