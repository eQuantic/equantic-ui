namespace eQuantic.UI.Primitives;

/// <summary>
/// Sending a person's files to the app's own server: the capability a page takes in its constructor,
/// or a component asks its context for, as it takes <see cref="IFilePicker"/>. The files come from the
/// picker or a <see cref="DropTarget"/>, the server takes them under the policies the app accepts
/// (<c>UseUploads</c> on <c>AddUI</c>), and a Server Action receives each one as an
/// <see cref="UploadedFile"/>.
/// <para>
/// Most pages never take this: the <c>FileUpload</c> component is the area, the button, the progress
/// and the controls, over this capability. A page takes it to draw its own.
/// </para>
/// <para>
/// A target with no server to send to answers <see cref="IsAvailable"/> false: server rendering, and a
/// native shell until it gains a transport. There, <see cref="Start"/> answers an upload that has
/// already failed, saying so, rather than throwing.
/// </para>
/// </summary>
public interface IUploads
{
    /// <summary>Whether this target can send a file to the app's server.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Starts sending <paramref name="file"/> under <paramref name="policy"/>, and answers at once with
    /// the upload, which goes on by itself. A file the policy refuses answers an upload that has already
    /// failed, before a byte is sent.
    /// </summary>
    /// <param name="file">What the person chose or dropped.</param>
    /// <param name="policy">One of the policies the app's server accepts.</param>
    Upload Start(PickedFile file, UploadPolicy policy);
}
