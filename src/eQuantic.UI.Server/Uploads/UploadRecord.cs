namespace eQuantic.UI.Server;

/// <summary>
/// What the server knows of an upload beyond its bytes, written beside them as
/// <c>&lt;id&gt;.upload.json</c> when it starts and read back when the server starts, so its owner
/// and nobody else may still continue it or receive it after a restart.
/// </summary>
/// <param name="Id">The upload's id, which names its bytes.</param>
/// <param name="Owner">Whose it is (<see cref="UploadOwner"/>).</param>
/// <param name="Policy">The name of the policy it was accepted under.</param>
/// <param name="Name">The file's name, without any folder the device sent.</param>
/// <param name="MimeType">What the device said the file is.</param>
/// <param name="Size">The file's size, in bytes.</param>
/// <param name="Created">When it started.</param>
/// <param name="Expires">When it is removed, finished or not.</param>
internal sealed record UploadRecord(
    string Id, string Owner, string Policy, string Name, string MimeType, long Size, DateTimeOffset Created,
    DateTimeOffset Expires);
