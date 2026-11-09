using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using tusdotnet.Stores;

namespace eQuantic.UI.Server;

/// <summary>
/// The uploads the server keeps, in one folder: each one's bytes, kept by the tus engine's disk store,
/// and its record (<see cref="UploadRecord"/>) beside them as <c>&lt;id&gt;.upload.json</c>. The
/// records are read back the first time the server is asked about an upload, so an upload cut off by a
/// restart or a deploy can still be continued, and received, by its owner and nobody else.
/// <para>
/// The records are held in memory once read, so the store serves ONE instance of the app: several
/// instances behind a balancer each know only the uploads they received, and a part sent to another
/// one is refused as an upload it never saw.
/// </para>
/// </summary>
internal sealed class UploadStore
{
    private const string RecordSuffix = ".upload.json";

    /// <summary>
    /// Guards what the limits read: the records, which of them are finished, the places creations hold,
    /// and the tallies kept of all three. One lock over them, so a record that is written and the place
    /// it held that is given back are one step to every count.
    /// </summary>
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, UploadRecord> _records = new(StringComparer.Ordinal);
    private readonly HashSet<string> _finished = new(StringComparer.Ordinal);
    /// <summary>Each owner's uploads, counted as they come and go: a count of the records under the lock on
    /// every creation grew with every upload the store kept (#1287).</summary>
    private readonly Dictionary<string, Tally> _owners = new(StringComparer.Ordinal);
    private readonly Tally _total = new();
    private readonly UploadsOptions _options;
    private readonly Lazy<Task> _loaded;
    private readonly ILogger<UploadStore> _logger;

    public UploadStore(IOptions<UploadsOptions> options, IHostEnvironment environment, ILogger<UploadStore> logger)
    {
        _logger = logger;
        _options = options.Value;
        Folder = FolderOf(options.Value, environment);
        Directory.CreateDirectory(Folder);
        logger.LogInformation("Uploads are kept in {Folder}", Folder);
        Tus = new TusDiskStore(Folder);
        _loaded = new Lazy<Task>(LoadAsync);
    }

    /// <summary>The folder the uploads are kept in.</summary>
    public string Folder { get; }

    /// <summary>The tus engine's store of the uploads' bytes, in <see cref="Folder"/>.</summary>
    public TusDiskStore Tus { get; }

    /// <summary>The record of the upload <paramref name="id"/>, or null when the server keeps none.</summary>
    public async Task<UploadRecord?> FindAsync(string id)
    {
        await _loaded.Value;
        return _records.GetValueOrDefault(id);
    }

    /// <summary>
    /// Records an upload that started, on disk first, so a record the server answers by is one a
    /// restart reads back, and gives back the place its creation held in the same step.
    /// </summary>
    public async Task AddAsync(UploadRecord record, UploadReservation? reservation, CancellationToken cancellationToken)
    {
        await _loaded.Value;
        await File.WriteAllTextAsync(RecordPath(record.Id), JsonSerializer.Serialize(record), cancellationToken);
        lock (_gate)
        {
            _records[record.Id] = record;
            Count(record, +1, unfinished: true);
            if (reservation is not null && reservation.Take()) Unreserve(reservation);
        }
    }

    /// <summary>Notes that the last byte of an upload arrived, so it no longer counts as unfinished. It
    /// still counts as one of its owner's uploads, and its bytes as theirs, until it expires.</summary>
    public async Task FinishAsync(string id)
    {
        await _loaded.Value;
        lock (_gate)
        {
            if (_finished.Add(id) && _records.TryGetValue(id, out var record)) Count(record, 0, unfinished: true, finishing: true);
        }
    }

    /// <summary>Forgets an upload whose bytes are gone.</summary>
    public async Task ForgetAsync(string id)
    {
        await _loaded.Value;
        Forget(id);
    }

    /// <summary>Whether every byte of the upload has arrived.</summary>
    public async Task<bool> IsCompleteAsync(UploadRecord record, CancellationToken cancellationToken) =>
        await Tus.FileExistAsync(record.Id, cancellationToken)
        && await Tus.GetUploadOffsetAsync(record.Id, cancellationToken) == record.Size;

    /// <summary>
    /// Holds a place for an upload of <paramref name="size"/> bytes that <paramref name="owner"/> is
    /// creating, or answers the limit it would pass: the owner's unfinished uploads, the uploads and the
    /// bytes the owner keeps, finished or not, and the uploads and the bytes the store keeps, everyone's
    /// together. The places other creations hold count as uploads already made. A limit on uploads answers
    /// 429, a limit on bytes 413, each naming the limit.
    /// </summary>
    public async Task<(UploadReservation? Reservation, HttpStatusCode Status, string? Refusal)> ReserveAsync(string owner, long size)
    {
        await _loaded.Value;
        lock (_gate)
        {
            var mine = _owners.GetValueOrDefault(owner) ?? new Tally();
            if (mine.Unfinished + mine.ReservedUploads >= _options.MaxUnfinished)
                return (null, HttpStatusCode.TooManyRequests, $"At most {_options.MaxUnfinished} uploads may be unfinished at a time.");
            if (mine.Uploads + mine.ReservedUploads >= _options.MaxUploadsPerOwner)
                return (null, HttpStatusCode.TooManyRequests, $"At most {_options.MaxUploadsPerOwner} uploads may be kept at a time.");
            if (mine.Bytes + mine.ReservedBytes + size > _options.MaxBytesPerOwner)
                return (null, HttpStatusCode.RequestEntityTooLarge, $"Your uploads may take at most {_options.MaxBytesPerOwner} bytes.");
            if (_total.Uploads + _total.ReservedUploads >= _options.MaxTotalUploads)
                return (null, HttpStatusCode.TooManyRequests, $"The server keeps at most {_options.MaxTotalUploads} uploads at a time: try again later.");
            if (_total.Bytes + _total.ReservedBytes + size > _options.MaxTotalBytes)
                return (null, HttpStatusCode.RequestEntityTooLarge, $"The server's uploads may take at most {_options.MaxTotalBytes} bytes: try again later.");

            foreach (var tally in new[] { TallyOf(owner), _total })
            {
                tally.ReservedUploads++;
                tally.ReservedBytes += size;
            }
        }

        return (new UploadReservation(this, owner, size), HttpStatusCode.Created, null);
    }

    /// <summary>Gives back a place <see cref="ReserveAsync"/> held, for a creation that wrote no record.</summary>
    internal void Release(UploadReservation reservation)
    {
        lock (_gate) Unreserve(reservation);
    }

    private void Unreserve(UploadReservation reservation)
    {
        foreach (var tally in new[] { TallyOf(reservation.Owner), _total })
        {
            tally.ReservedUploads--;
            tally.ReservedBytes -= reservation.Size;
        }
        DropIfEmpty(reservation.Owner);
    }

    /// <summary>Counts a record in or out (<paramref name="sign"/>), or, <paramref name="finishing"/>, counts
    /// it out of the unfinished only. Called under the lock.</summary>
    private void Count(UploadRecord record, int sign, bool unfinished, bool finishing = false)
    {
        foreach (var tally in new[] { TallyOf(record.Owner), _total })
        {
            if (finishing)
            {
                tally.Unfinished--;
                continue;
            }
            tally.Uploads += sign;
            tally.Bytes += sign * record.Size;
            if (unfinished) tally.Unfinished += sign;
        }
        DropIfEmpty(record.Owner);
    }

    private Tally TallyOf(string owner)
    {
        if (!_owners.TryGetValue(owner, out var tally)) _owners[owner] = tally = new Tally();
        return tally;
    }

    private void DropIfEmpty(string owner)
    {
        if (_owners.TryGetValue(owner, out var tally) && tally is { Uploads: 0, ReservedUploads: 0 }) _owners.Remove(owner);
    }

    /// <summary>What an owner, or the whole store, keeps and holds places for.</summary>
    private sealed class Tally
    {
        public int Uploads;
        public long Bytes;
        public int Unfinished;
        public int ReservedUploads;
        public long ReservedBytes;
    }

    /// <summary>The upload's content, read from the disk a part at a time.</summary>
    public async Task<Stream> OpenReadAsync(string id, CancellationToken cancellationToken)
    {
        var file = await Tus.GetFileAsync(id, cancellationToken)
                   ?? throw new FileNotFoundException("The upload's content is gone: it was deleted, or it expired.", id);
        return await file.GetContentAsync(cancellationToken);
    }

    /// <summary>Removes an upload, its bytes and its record.</summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _loaded.Value;
        await Tus.DeleteFileAsync(id, cancellationToken);
        Forget(id);
    }

    /// <summary>
    /// Removes every upload past its expiry at <paramref name="now"/>, finished or not, its bytes and its
    /// record; then the bytes the tus engine's own expiry removes, an upload whose creation stopped
    /// before its record was written among them; then the records whose bytes are gone. Answers how
    /// many uploads were removed.
    /// </summary>
    public async Task<int> RemoveExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await _loaded.Value;
        var removed = 0;
        foreach (var record in _records.Values.Where(record => record.Expires <= now).ToList())
        {
            await DeleteAsync(record.Id, cancellationToken);
            removed++;
        }

        removed += await Tus.RemoveExpiredFilesAsync(cancellationToken);
        foreach (var record in _records.Values.ToList())
            if (!await Tus.FileExistAsync(record.Id, cancellationToken)) Forget(record.Id);
        return removed;
    }

    private void Forget(string id)
    {
        lock (_gate)
        {
            if (_records.TryRemove(id, out var record)) Count(record, -1, unfinished: !_finished.Contains(id));
            _finished.Remove(id);
        }

        File.Delete(RecordPath(id));
    }

    private async Task LoadAsync()
    {
        foreach (var path in Directory.EnumerateFiles(Folder, "*" + RecordSuffix))
        {
            var id = Path.GetFileName(path)[..^RecordSuffix.Length];
            try
            {
                var record = JsonSerializer.Deserialize<UploadRecord>(await File.ReadAllTextAsync(path));
                // A record names the bytes of its own file name and no other, and a record whose bytes
                // are gone (removed while the server was down) is forgotten.
                if (record is not null && record.Id == id && await Tus.FileExistAsync(id, CancellationToken.None))
                {
                    var finished = await IsCompleteAsync(record, CancellationToken.None);
                    lock (_gate)
                    {
                        _records[id] = record;
                        if (finished) _finished.Add(id);
                        Count(record, +1, unfinished: !finished);
                    }
                }
                else
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
            {
                _logger.LogWarning(exception, "Skipped the unreadable upload record {File}", path);
            }
        }
    }

    private string RecordPath(string id) => Path.Combine(Folder, id + RecordSuffix);

    /// <summary>
    /// The folder the options name, under the app's content root when relative, or else one of the
    /// app's own under the system's temporary folder: two apps on one machine never share uploads they
    /// did not choose to share.
    /// </summary>
    internal static string FolderOf(UploadsOptions options, IHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(options.Folder))
            return Path.GetFullPath(options.Folder, environment.ContentRootPath);
        var invalid = Path.GetInvalidFileNameChars();
        var app = new string(environment.ApplicationName.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        return Path.Combine(Path.GetTempPath(), "equantic-uploads", app.Length > 0 ? app : "app");
    }
}
