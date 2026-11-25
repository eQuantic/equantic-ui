using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace eQuantic.UI.Server.Tests.Uploads;

/// <summary>
/// What several instances of an app share when their uploads are kept apart from any one of them, as an
/// object store or a database would hold them, and every id each instance asked it for.
/// </summary>
internal sealed class SharedUploads
{
    public readonly object Gate = new();
    public readonly Dictionary<string, StoredUpload> Records = new(StringComparer.Ordinal);
    public readonly Dictionary<string, byte[]> Bytes = new(StringComparer.Ordinal);
    public readonly ConcurrentQueue<string> Asked = new();
}

/// <summary>
/// An app's own <see cref="IUploadStorage"/> over <see cref="SharedUploads"/>, registered in each instance
/// with <c>UseStorage</c>: the instances are separate apps, and only this backing is common to them.
/// </summary>
internal sealed class SharedUploadStorage(SharedUploads shared) : IUploadStorage
{
    public ValueTask CreateAsync(StoredUpload upload, CancellationToken cancellationToken = default)
    {
        shared.Asked.Enqueue(upload.Id);
        lock (shared.Gate)
        {
            shared.Records[upload.Id] = upload;
            shared.Bytes[upload.Id] = [];
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<StoredUpload?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        shared.Asked.Enqueue(id);
        lock (shared.Gate) return ValueTask.FromResult(shared.Records.GetValueOrDefault(id));
    }

    public ValueTask<long> LengthAsync(string id, CancellationToken cancellationToken = default)
    {
        shared.Asked.Enqueue(id);
        lock (shared.Gate) return ValueTask.FromResult(shared.Bytes.TryGetValue(id, out var bytes) ? (long)bytes.Length : 0);
    }

    public async ValueTask<long> AppendAsync(string id, long offset, Stream content, CancellationToken cancellationToken = default)
    {
        shared.Asked.Enqueue(id);
        using var part = new MemoryStream();
        await content.CopyToAsync(part, cancellationToken);
        lock (shared.Gate)
        {
            if (!shared.Bytes.TryGetValue(id, out var bytes) || bytes.Length != offset) return 0;
            shared.Bytes[id] = [.. bytes, .. part.ToArray()];
            return part.Length;
        }
    }

    public ValueTask<Stream> OpenReadAsync(string id, CancellationToken cancellationToken = default)
    {
        shared.Asked.Enqueue(id);
        lock (shared.Gate) return ValueTask.FromResult<Stream>(new MemoryStream(shared.Bytes[id], writable: false));
    }

    public ValueTask DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        shared.Asked.Enqueue(id);
        lock (shared.Gate)
        {
            shared.Records.Remove(id);
            shared.Bytes.Remove(id);
        }

        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<StoredUpload> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<StoredUpload> records;
        lock (shared.Gate) records = [.. shared.Records.Values];
        foreach (var record in records)
        {
            await Task.Yield();
            yield return record;
        }
    }
}
