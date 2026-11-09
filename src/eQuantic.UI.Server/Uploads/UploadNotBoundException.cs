namespace eQuantic.UI.Server;

/// <summary>
/// A Server Action's <c>UploadedFile</c> that the server did not bind, and so the action does not run:
/// the status the page is answered with and a reason it can show.
/// </summary>
internal sealed class UploadNotBoundException(int statusCode, string reason) : Exception(reason)
{
    /// <summary>The status the request is answered with.</summary>
    public int StatusCode { get; } = statusCode;
}
