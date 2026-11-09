using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server;

/// <summary>
/// The <see cref="UploadedFile"/> a Server Action receives: every member answered from the server's
/// record, and the content read from where the server keeps it.
/// </summary>
internal sealed class ReceivedUploadedFile(UploadRecord record, UploadStore store)
    : UploadedFile(record.Id, record.Name, record.MimeType, record.Size, record.Policy)
{
    public override Task<Stream> OpenRead(CancellationToken cancellationToken = default) =>
        store.OpenReadAsync(record.Id, cancellationToken);

    public override Task Delete(CancellationToken cancellationToken = default) =>
        store.DeleteAsync(record.Id, cancellationToken);
}
