namespace eQuantic.UI.Primitives;

/// <summary>
/// One file on its way to the app's server, under an <see cref="UploadPolicy"/>: what
/// <see cref="IUploads.Start"/> answers with, and what a page holds to show the file's progress and to
/// pause, resume or cancel it.
/// <code>
/// var upload = uploads.Start(file, Uploads.Models);
/// _subscription = upload.Subscribe(_ =&gt; SetState(() =&gt; { }));
/// …
/// if (upload.State == UploadState.Completed) await SaveModel(upload.Result!);
/// </code>
/// <para>
/// The file travels in parts, and an upload whose connection drops goes on from the last part the
/// server holds, by itself. One the person started on an earlier visit goes on too: starting the same
/// file again under the same policy asks the server how much of it already arrived.
/// </para>
/// <para>
/// A file the policy does not accept is refused before a byte is sent, with the reason the server would
/// have given (<see cref="UploadPolicy.RefusalOf"/>): the upload starts <see cref="UploadState.Failed"/>.
/// </para>
/// <para>
/// Its changes are heard through <see cref="Subscribe"/>, which answers an <see cref="IDisposable"/>
/// rather than a .NET event, as <see cref="INetworkStatus"/> does: a component ends its subscription
/// where it ends, in <c>OnUnmount</c>. Each target realizes it over its own transport, and a page never
/// constructs one.
/// </para>
/// </summary>
public abstract class Upload
{
    /// <summary>For a target's realization: the file it sends and the policy it sends it under.</summary>
    protected Upload(PickedFile file, UploadPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(policy);
        File = file;
        Policy = policy;
    }

    /// <summary>The file being sent.</summary>
    public PickedFile File { get; }

    /// <summary>The policy it is sent under.</summary>
    public UploadPolicy Policy { get; }

    /// <summary>Where the upload stands.</summary>
    public abstract UploadState State { get; }

    /// <summary>How many of the file's bytes the server holds, counting the part in flight as it goes.</summary>
    public abstract long Sent { get; }

    /// <summary>How many bytes the file holds: the end <see cref="Sent"/> reaches.</summary>
    public long Size => File.Size;

    /// <summary>
    /// Why the upload <see cref="UploadState.Failed"/>, in English: the server's reason as it gave it
    /// ("Only .stl, .obj files are accepted here."), the policy's when the file was refused before it was
    /// sent, or the connection's. Null while it has not failed.
    /// </summary>
    public abstract string? Error { get; }

    /// <summary>The file as a Server Action receives it, once <see cref="UploadState.Completed"/>; null
    /// before.</summary>
    public abstract UploadedFile? Result { get; }

    /// <summary>Stops sending. The server keeps what arrived. Does nothing unless the upload is waiting
    /// or sending.</summary>
    public abstract void Pause();

    /// <summary>Goes on from where the server stands. Does nothing unless the upload is paused.</summary>
    public abstract void Resume();

    /// <summary>Stops for good, and asks the server to remove what arrived. Does nothing once the upload
    /// completed or was cancelled.</summary>
    public abstract void Cancel();

    /// <summary>
    /// Listens to the upload: <paramref name="onChanged"/> is called with it on every change from now on,
    /// its state and its progress, not for where it stands now. Disposing stops it; disposing twice is
    /// fine.
    /// </summary>
    public abstract IDisposable Subscribe(Action<Upload> onChanged);
}
