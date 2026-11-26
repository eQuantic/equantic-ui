using tusdotnet.Interfaces;
using tusdotnet.Models;

namespace eQuantic.UI.Server;

/// <summary>
/// The tus engine's store over the app's <see cref="IUploadStorage"/>, through <see cref="UploadStore"/>,
/// which hands the storage only the ids the server gives. The engine speaks the protocol (the parts, the
/// offset, the expiry); every answer it needs about an upload is read from the storage, so any instance
/// over one storage answers it.
/// <para>
/// A creation only gives the upload its id here: its record, and the empty content beside it, are kept
/// when the creation completes (<see cref="UploadProtocol"/>), with the owner the request names. The
/// extensions that would act before that (a creation that carries data) or that the storage does not
/// keep (a checksum) are not offered.
/// </para>
/// </summary>
internal sealed class TusStorageAdapter(UploadStore store, TimeProvider time)
    : ITusStore, ITusCreationStore, ITusTerminationStore, ITusExpirationStore
{
    public Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken) =>
        Task.FromResult(UploadStore.NewId());

    /// <summary>The metadata is read when the creation is weighed and kept as the record; the endpoint
    /// does not echo it.</summary>
    public Task<string> GetUploadMetadataAsync(string fileId, CancellationToken cancellationToken) => Task.FromResult<string>(null!);

    public async Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken) =>
        await store.FindAsync(fileId, cancellationToken) is not null;

    public async Task<long?> GetUploadLengthAsync(string fileId, CancellationToken cancellationToken) =>
        (await store.FindAsync(fileId, cancellationToken))?.Size;

    public Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken) =>
        store.LengthAsync(fileId, cancellationToken);

    public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
    {
        var record = await store.FindAsync(fileId, cancellationToken)
                     ?? throw new TusStoreException("There is no upload with this id: it was never made, or it expired.");
        var offset = await store.LengthAsync(fileId, cancellationToken);
        // The storage is handed no byte past the size the upload declared.
        return await store.Storage.AppendAsync(fileId, offset, new DeclaredLength(stream, record.Size - offset), cancellationToken);
    }

    public Task DeleteFileAsync(string fileId, CancellationToken cancellationToken) => store.DeleteAsync(fileId, cancellationToken);

    /// <summary>The expiry is the record's, set when it is kept.</summary>
    public Task SetExpirationAsync(string fileId, DateTimeOffset expires, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<DateTimeOffset?> GetExpirationAsync(string fileId, CancellationToken cancellationToken) =>
        (await store.FindAsync(fileId, cancellationToken))?.Expires;

    public async Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken cancellationToken) =>
        await store.ExpiredAsync(time.GetUtcNow(), cancellationToken);

    public Task<int> RemoveExpiredFilesAsync(CancellationToken cancellationToken) =>
        store.RemoveExpiredAsync(time.GetUtcNow(), cancellationToken);

    /// <summary>
    /// A part's body, read up to what the upload still lacks: a byte past it fails the part, as the
    /// engine's own disk store did, instead of reaching the storage.
    /// </summary>
    private sealed class DeclaredLength(Stream inner, long remaining) : Stream
    {
        private long _remaining = remaining;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0) return 0;
            if (_remaining == 0)
            {
                // One byte more is asked for only to learn whether the part carries more than it may.
                var beyond = await inner.ReadAsync(new byte[1], cancellationToken);
                return beyond == 0
                    ? 0
                    : throw new TusStoreException("Request contains more data than the file's upload length.");
            }

            var read = await inner.ReadAsync(buffer[..(int)Math.Min(buffer.Length, _remaining)], cancellationToken);
            _remaining -= read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
