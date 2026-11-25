namespace eQuantic.UI.Server;

/// <summary>
/// Keeps the uploads the server receives: each one's record (<see cref="StoredUpload"/>) and its bytes,
/// from the moment it starts until it is deleted or expires. The default keeps them in a folder of the
/// instance's disk (<c>EQuantic:Uploads:Folder</c>), which is all a single instance needs. An app that
/// runs several instances registers its own with <c>UseUploads(uploads =&gt; uploads.UseStorage&lt;T&gt;())</c>,
/// over storage every instance reaches (an object store, a blob container, a database, a shared volume):
/// the parts of one upload, and the Server Action that receives it, may each reach another instance.
/// <para>
/// The server asks only for the ids it gives (32 lowercase hexadecimal digits), and the limits and the
/// expiry are read from <see cref="ListAsync"/>, on every creation and every ten minutes.
/// </para>
/// </summary>
public interface IUploadStorage
{
    /// <summary>Keeps a new upload's record, with none of its bytes yet.</summary>
    ValueTask CreateAsync(StoredUpload upload, CancellationToken cancellationToken = default);

    /// <summary>The record of the upload <paramref name="id"/>, or null when none is kept.</summary>
    ValueTask<StoredUpload?> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>How many of the upload's bytes are kept, which is where its next part starts; 0 for an
    /// upload none of whose bytes are kept.</summary>
    ValueTask<long> LengthAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds <paramref name="content"/> to the upload's bytes, only when <paramref name="offset"/> is the
    /// length it keeps, and answers how many bytes it kept. At any other offset it keeps nothing and
    /// answers 0: two instances may each accept a part for the same offset. An append that is cut off may
    /// keep all, some or none of what arrived, in order, as long as <see cref="LengthAsync"/> then answers
    /// what it kept.
    /// </summary>
    ValueTask<long> AppendAsync(string id, long offset, Stream content, CancellationToken cancellationToken = default);

    /// <summary>The upload's bytes, from the first, read as they are kept rather than held in memory.</summary>
    ValueTask<Stream> OpenReadAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Removes the upload's bytes and record; nothing when none is kept.</summary>
    ValueTask DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>The record of every upload kept, finished or not.</summary>
    IAsyncEnumerable<StoredUpload> ListAsync(CancellationToken cancellationToken = default);
}
