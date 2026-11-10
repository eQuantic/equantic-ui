namespace eQuantic.UI.Server;

/// <summary>
/// A place among an owner's uploads, and among the store's, held from the moment a creation is accepted
/// until its record is written or the request ends: one upload and its declared size, counted against
/// every limit. Without it, two creations sent at once each counted the uploads before the other's record
/// existed, and both passed the last place.
/// </summary>
internal sealed class UploadReservation(UploadStore store, string owner, long size) : IDisposable
{
    private int _released;

    /// <summary>The owner whose place this is.</summary>
    public string Owner => owner;

    /// <summary>The size the creation declared, in bytes.</summary>
    public long Size => size;

    /// <summary>
    /// Whether this call takes the place back, for the store to give it back itself in the step that
    /// writes the record: false when it was already given back. Either this or <see cref="Dispose"/> gives
    /// it back, never both.
    /// </summary>
    public bool Take() => Interlocked.Exchange(ref _released, 1) == 0;

    /// <summary>Gives the place back, for a creation that wrote no record; after <see cref="Take"/>, or a
    /// second time, it does nothing.</summary>
    public void Dispose()
    {
        if (Take()) store.Release(this);
    }
}
