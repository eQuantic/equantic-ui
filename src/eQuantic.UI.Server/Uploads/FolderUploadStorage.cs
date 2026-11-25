using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eQuantic.UI.Server;

/// <summary>
/// The <see cref="IUploadStorage"/> an app has until it registers its own: each upload's bytes in a file
/// named by its id, and its record beside them as <c>&lt;id&gt;.upload.json</c>, in the folder the options
/// name. The records are read back the first time the storage is asked, so an upload cut off by a restart
/// or a deploy can still be continued, and received, by its owner and nobody else.
/// <para>
/// Once read, the records are held in memory, since this storage is the only writer of its folder: it
/// serves ONE instance of the app. An app that runs several registers a storage they all reach.
/// </para>
/// </summary>
internal sealed class FolderUploadStorage : IUploadStorage
{
    private const string RecordSuffix = ".upload.json";

    /// <summary>The size of the buffer a file is written and read through.</summary>
    private const int BufferSize = 81920;

    private readonly ConcurrentDictionary<string, StoredUpload> _records = new(StringComparer.Ordinal);
    private readonly Lazy<Task> _loaded;
    private readonly ILogger<FolderUploadStorage> _logger;

    public FolderUploadStorage(IOptions<UploadsOptions> options, IHostEnvironment environment, ILogger<FolderUploadStorage> logger)
    {
        _logger = logger;
        Folder = FolderOf(options.Value, environment);
        Directory.CreateDirectory(Folder);
        logger.LogInformation("Uploads are kept in {Folder}", Folder);
        _loaded = new Lazy<Task>(LoadAsync);
    }

    /// <summary>The folder the uploads are kept in.</summary>
    public string Folder { get; }

    public async ValueTask CreateAsync(StoredUpload upload, CancellationToken cancellationToken = default)
    {
        await _loaded.Value;
        // The bytes first, empty, so a record the storage answers by always has a file to append to.
        await using (new FileStream(BytesPath(upload.Id), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
        }

        await File.WriteAllTextAsync(RecordPath(upload.Id), JsonSerializer.Serialize(upload), cancellationToken);
        _records[upload.Id] = upload;
    }

    public async ValueTask<StoredUpload?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        await _loaded.Value;
        return _records.GetValueOrDefault(id);
    }

    public ValueTask<long> LengthAsync(string id, CancellationToken cancellationToken = default)
    {
        var file = new FileInfo(BytesPath(id));
        return ValueTask.FromResult(file.Exists ? file.Length : 0);
    }

    public async ValueTask<long> AppendAsync(string id, long offset, Stream content, CancellationToken cancellationToken = default)
    {
        // Opened alone, so the length read is still the length when the bytes are written after it.
        await using var file = new FileStream(BytesPath(id), FileMode.Open, FileAccess.Write, FileShare.None, BufferSize,
            FileOptions.Asynchronous);
        if (file.Length != offset) return 0;
        file.Position = offset;
        try
        {
            await content.CopyToAsync(file, BufferSize, cancellationToken);
        }
        finally
        {
            // What arrived before a cut is kept, as far as it was written: the page asks the length and
            // sends the rest from there.
            await file.FlushAsync(CancellationToken.None);
        }

        return file.Position - offset;
    }

    public ValueTask<Stream> OpenReadAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = BytesPath(id);
        if (!File.Exists(path))
            throw new FileNotFoundException("The upload's content is gone: it was deleted, or it expired.", id);
        // Shared for deleting, so a Server Action that deletes the file while a stream of it is open is
        // not refused where the platform locks an open file.
        return ValueTask.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan));
    }

    public async ValueTask DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _loaded.Value;
        _records.TryRemove(id, out _);
        File.Delete(BytesPath(id));
        File.Delete(RecordPath(id));
    }

    public async IAsyncEnumerable<StoredUpload> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _loaded.Value;
        foreach (var record in _records.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return record;
        }
    }

    private async Task LoadAsync()
    {
        foreach (var path in Directory.EnumerateFiles(Folder, "*" + RecordSuffix))
        {
            var id = Path.GetFileName(path)[..^RecordSuffix.Length];
            try
            {
                var record = JsonSerializer.Deserialize<StoredUpload>(await File.ReadAllTextAsync(path));
                // A record names the bytes of its own file name and no other, and a record whose bytes
                // are gone (removed while the server was down) is forgotten.
                if (record is not null && record.Id == id && UploadStore.IsId(id) && File.Exists(BytesPath(id)))
                    _records[id] = record;
                else
                    File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
            {
                _logger.LogWarning(exception, "Skipped the unreadable upload record {File}", path);
            }
        }
    }

    private string BytesPath(string id) => Path.Combine(Folder, id);

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
