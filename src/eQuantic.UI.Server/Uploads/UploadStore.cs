using System.Collections.Concurrent;
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
    /// Guards what an owner's count of unfinished uploads reads: the records, which of them are
    /// finished, and the places creations hold. One lock over the three, so a record that is written
    /// and the place it held that is given back are one step to every count.
    /// </summary>
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, UploadRecord> _records = new(StringComparer.Ordinal);
    private readonly HashSet<string> _finished = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _reserved = new(StringComparer.Ordinal);
    private readonly Lazy<Task> _loaded;
    private readonly ILogger<UploadStore> _logger;

    public UploadStore(IOptions<UploadsOptions> options, IHostEnvironment environment, ILogger<UploadStore> logger)
    {
        _logger = logger;
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
            if (reservation?.Take() is { } owner) Unreserve(owner);
        }
    }

    /// <summary>Notes that the last byte of an upload arrived, so it no longer counts as unfinished.</summary>
    public async Task FinishAsync(string id)
    {
        await _loaded.Value;
        lock (_gate) _finished.Add(id);
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
    /// Holds a place among <paramref name="owner"/>'s unfinished uploads for one that is being created,
    /// or answers null when the owner already has <paramref name="limit"/> of them, counting the places
    /// other creations hold.
    /// </summary>
    public async Task<UploadReservation?> ReserveAsync(string owner, int limit)
    {
        await _loaded.Value;
        lock (_gate)
        {
            var unfinished = _records.Values.Count(record => record.Owner == owner && !_finished.Contains(record.Id));
            if (unfinished + _reserved.GetValueOrDefault(owner) >= limit) return null;
            _reserved[owner] = _reserved.GetValueOrDefault(owner) + 1;
        }

        return new UploadReservation(this, owner);
    }

    /// <summary>Gives back a place <see cref="ReserveAsync"/> held, for a creation that wrote no record.</summary>
    internal void Release(string owner)
    {
        lock (_gate) Unreserve(owner);
    }

    private void Unreserve(string owner)
    {
        var held = _reserved.GetValueOrDefault(owner) - 1;
        if (held > 0) _reserved[owner] = held;
        else _reserved.Remove(owner);
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
            _records.TryRemove(id, out _);
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
    private static string FolderOf(UploadsOptions options, IHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(options.Folder))
            return Path.GetFullPath(options.Folder, environment.ContentRootPath);
        var invalid = Path.GetInvalidFileNameChars();
        var app = new string(environment.ApplicationName.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        return Path.Combine(Path.GetTempPath(), "equantic-uploads", app.Length > 0 ? app : "app");
    }
}
