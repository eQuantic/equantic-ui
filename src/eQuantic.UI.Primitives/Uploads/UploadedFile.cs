namespace eQuantic.UI.Primitives;

/// <summary>
/// A file a person uploaded under an <see cref="UploadPolicy"/>, as a Server Action receives it:
/// <code>
/// [ServerAction]
/// public async Task SaveModel(UploadedFile model)
/// {
///     await using var content = await model.OpenRead();
///     …
/// }
/// </code>
/// <para>
/// The page hands the action the upload's <see cref="Id"/>, and the server answers every other member
/// from what it recorded when the upload started, never from what the page sent. It binds the file
/// only for the person who uploaded it, and only once its last byte has arrived; otherwise the action
/// fails before it runs.
/// </para>
/// <para>
/// The server keeps the upload in a staging folder until its expiry, then removes it, so an app takes
/// the content in its action and stores it where it keeps its data, and may <see cref="Delete"/> it
/// once it has. The content itself never crosses to the browser: <see cref="OpenRead"/> and
/// <see cref="Delete"/> are the server's, and a value the page built answers neither.
/// </para>
/// </summary>
public class UploadedFile
{
    /// <summary>An upload, as the page knows it.</summary>
    /// <param name="id">The upload's id, which the server gave it when it started.</param>
    /// <param name="name">The file's name, as the person's device named it.</param>
    /// <param name="mimeType">What the file is, as the device said: <c>model/stl</c>,
    /// <c>application/pdf</c>.</param>
    /// <param name="size">The file's size, in bytes.</param>
    /// <param name="policy">The name of the <see cref="UploadPolicy"/> it was uploaded under.</param>
    /// <exception cref="ArgumentException">The id is empty or white space.</exception>
    /// <exception cref="ArgumentNullException">The id, the name, the type or the policy is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is negative.</exception>
    public UploadedFile(string id, string name, string mimeType, long size, string policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(mimeType);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentNullException.ThrowIfNull(policy);
        Id = id;
        Name = name;
        MimeType = mimeType;
        Size = size;
        Policy = policy;
    }

    /// <summary>The upload's id.</summary>
    public string Id { get; }

    /// <summary>The file's name.</summary>
    public string Name { get; }

    /// <summary>What the file is.</summary>
    public string MimeType { get; }

    /// <summary>The file's size, in bytes.</summary>
    public long Size { get; }

    /// <summary>The name of the policy it was uploaded under.</summary>
    public string Policy { get; }

    /// <summary>
    /// The file's content, read from where the server keeps it, a part at a time: never the whole file
    /// in memory. The caller disposes the stream.
    /// </summary>
    /// <exception cref="InvalidOperationException">This value is not a Server Action's parameter, so it
    /// holds no content.</exception>
    [ServerOnly]
    public virtual Task<Stream> OpenRead(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(HoldsNoContent);

    /// <summary>
    /// Removes the upload from the server's staging folder, its bytes and its record, once the app has
    /// stored the content where it keeps its data. An upload nobody removes is removed at its expiry.
    /// </summary>
    /// <exception cref="InvalidOperationException">This value is not a Server Action's parameter, so it
    /// holds no content.</exception>
    [ServerOnly]
    public virtual Task Delete(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(HoldsNoContent);

    private const string HoldsNoContent =
        "Only an UploadedFile a Server Action received as its parameter holds the file's content.";
}
