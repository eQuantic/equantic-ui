using System.Net;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// The uploads, as the endpoint, the Server Actions and the cleaner see them: kept by the app's
/// <see cref="IUploadStorage"/>, which this asks for every answer instead of holding any in memory, so
/// several instances over one storage answer as one. It hands the storage only the ids the server gives,
/// counts the limits from what the storage keeps, and holds, in this instance's memory, the places the
/// creations it accepted hold until their records are written.
/// </summary>
internal sealed class UploadStore
{
    /// <summary>The places held, read and changed under it.</summary>
    private readonly object _gate = new();
    private readonly List<UploadReservation> _held = [];

    /// <summary>
    /// One creation weighed at a time on this instance: the listing, the check and the place it takes are
    /// one step, so twelve creations sent at once cannot each count the uploads before the others' places.
    /// </summary>
    private readonly SemaphoreSlim _creations = new(1, 1);

    private readonly UploadsOptions _options;

    public UploadStore(IUploadStorage storage, IOptions<UploadsOptions> options)
    {
        Storage = storage;
        _options = options.Value;
    }

    /// <summary>Where the uploads are kept.</summary>
    public IUploadStorage Storage { get; }

    /// <summary>
    /// Whether <paramref name="id"/> is one the server gives: 32 lowercase hexadecimal digits. Any other is
    /// answered as an upload that does not exist, without asking the storage, so a request cannot lead a
    /// storage over a folder or a key space anywhere with a path.
    /// </summary>
    internal static bool IsId(string? id) =>
        id is { Length: 32 } && id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>A new upload's id.</summary>
    public static string NewId() => Guid.NewGuid().ToString("N");

    /// <summary>The record of the upload <paramref name="id"/>, or null when the storage keeps none.</summary>
    public async Task<StoredUpload?> FindAsync(string id, CancellationToken cancellationToken = default) =>
        IsId(id) ? await Storage.FindAsync(id, cancellationToken) : null;

    /// <summary>How many of the upload's bytes the storage keeps.</summary>
    public async Task<long> LengthAsync(string id, CancellationToken cancellationToken = default) =>
        IsId(id) ? await Storage.LengthAsync(id, cancellationToken) : 0;

    /// <summary>Whether every byte of the upload has arrived.</summary>
    public async Task<bool> IsCompleteAsync(StoredUpload record, CancellationToken cancellationToken) =>
        await Storage.LengthAsync(record.Id, cancellationToken) == record.Size;

    /// <summary>
    /// Keeps the record of an upload that started, and gives back the place its creation held once the
    /// storage holds it: until then the place counts it, and after, the record does.
    /// </summary>
    public async Task AddAsync(StoredUpload record, UploadReservation? reservation, CancellationToken cancellationToken)
    {
        // The place knows its upload's id before the record is written, so a creation weighed in between
        // counts the two as the one upload they are.
        if (reservation is not null)
            lock (_gate) reservation.Id = record.Id;
        await Storage.CreateAsync(record, cancellationToken);
        if (reservation is not null && reservation.Take()) Release(reservation);
    }

    /// <summary>
    /// Holds a place for an upload of <paramref name="size"/> bytes that <paramref name="owner"/> is
    /// creating, or answers the limit it would pass: the owner's unfinished uploads, the uploads and the
    /// bytes the owner keeps, finished or not, and the uploads and the bytes the storage keeps, everyone's
    /// together. The places other creations hold count as uploads already made. A limit on uploads answers
    /// 429, a limit on bytes 413, each naming the limit; the owner's unfinished limit is the one that frees
    /// itself, as those uploads finish.
    /// </summary>
    public async Task<UploadPlace> ReserveAsync(string owner, long size, CancellationToken cancellationToken = default)
    {
        await _creations.WaitAsync(cancellationToken);
        try
        {
            List<(string Owner, long Size, string? Id)> held;
            lock (_gate) held = _held.Select(place => (place.Owner, place.Size, place.Id)).ToList();
            var heldIds = held.Where(place => place.Id is not null).Select(place => place.Id!).ToHashSet(StringComparer.Ordinal);

            var mine = new Tally();
            var all = new Tally();
            await foreach (var upload in Storage.ListAsync(cancellationToken))
            {
                if (heldIds.Contains(upload.Id)) continue;
                all.Add(upload.Size, unfinished: false);
                if (string.Equals(upload.Owner, owner, StringComparison.Ordinal))
                    mine.Add(upload.Size, unfinished: await Storage.LengthAsync(upload.Id, cancellationToken) < upload.Size);
            }

            foreach (var place in held)
            {
                all.Add(place.Size, unfinished: true);
                if (string.Equals(place.Owner, owner, StringComparison.Ordinal)) mine.Add(place.Size, unfinished: true);
            }

            if (mine.Unfinished >= _options.MaxUnfinished)
                return UploadPlace.Refused(HttpStatusCode.TooManyRequests,
                    $"At most {_options.MaxUnfinished} uploads may be unfinished at a time.", freesItself: true);
            if (mine.Uploads >= _options.MaxUploadsPerOwner)
                return UploadPlace.Refused(HttpStatusCode.TooManyRequests, $"At most {_options.MaxUploadsPerOwner} uploads may be kept at a time.");
            if (mine.Bytes + size > _options.MaxBytesPerOwner)
                return UploadPlace.Refused(HttpStatusCode.RequestEntityTooLarge, $"Your uploads may take at most {_options.MaxBytesPerOwner} bytes.");
            if (all.Uploads >= _options.MaxTotalUploads)
                return UploadPlace.Refused(HttpStatusCode.TooManyRequests,
                    $"The server keeps at most {_options.MaxTotalUploads} uploads at a time: try again later.");
            if (all.Bytes + size > _options.MaxTotalBytes)
                return UploadPlace.Refused(HttpStatusCode.RequestEntityTooLarge,
                    $"The server's uploads may take at most {_options.MaxTotalBytes} bytes: try again later.");

            var reservation = new UploadReservation(this, owner, size);
            lock (_gate) _held.Add(reservation);
            return new UploadPlace(reservation, HttpStatusCode.Created, null, false);
        }
        finally
        {
            _creations.Release();
        }
    }

    /// <summary>Gives back a place <see cref="ReserveAsync"/> held.</summary>
    internal void Release(UploadReservation reservation)
    {
        lock (_gate) _held.Remove(reservation);
    }

    /// <summary>The upload's content, read from where it is kept.</summary>
    public async Task<Stream> OpenReadAsync(string id, CancellationToken cancellationToken) =>
        IsId(id)
            ? await Storage.OpenReadAsync(id, cancellationToken)
            : throw new FileNotFoundException("The upload's content is gone: it was deleted, or it expired.", id);

    /// <summary>Removes an upload, its bytes and its record.</summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        if (IsId(id)) await Storage.DeleteAsync(id, cancellationToken);
    }

    /// <summary>The ids of the uploads past their expiry at <paramref name="now"/>, finished or not.</summary>
    public async Task<IReadOnlyList<string>> ExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expired = new List<string>();
        await foreach (var upload in Storage.ListAsync(cancellationToken))
            if (upload.Expires <= now) expired.Add(upload.Id);
        return expired;
    }

    /// <summary>
    /// Removes every upload past its expiry at <paramref name="now"/>, finished or not, its bytes and its
    /// record, and answers how many were removed.
    /// </summary>
    public async Task<int> RemoveExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Listed whole before anything is removed: a storage need not let its listing change under it.
        var expired = await ExpiredAsync(now, cancellationToken);
        foreach (var id in expired) await DeleteAsync(id, cancellationToken);
        return expired.Count;
    }

    /// <summary>What an owner, or the whole storage, keeps and holds places for.</summary>
    private sealed class Tally
    {
        public int Uploads;
        public long Bytes;
        public int Unfinished;

        public void Add(long size, bool unfinished)
        {
            Uploads++;
            Bytes += size;
            if (unfinished) Unfinished++;
        }
    }
}
