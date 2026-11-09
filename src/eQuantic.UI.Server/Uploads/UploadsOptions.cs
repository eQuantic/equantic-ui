namespace eQuantic.UI.Server;

/// <summary>
/// Where the server keeps the uploads it receives and for how long, bound from the
/// <c>EQuantic:Uploads</c> section of the app's configuration and overridable in
/// <c>UseUploads(uploads =&gt; uploads.Configure(…))</c>.
/// <para>
/// The folder is a STAGING area: an upload, finished or not, that is still there past its
/// <see cref="Expiry"/> is removed, its bytes and its record. An app takes the content in the Server
/// Action that receives the <c>UploadedFile</c> and stores it where it keeps its data.
/// </para>
/// </summary>
public sealed class UploadsOptions
{
    /// <summary>The configuration section the options are bound from.</summary>
    public const string SectionName = "EQuantic:Uploads";

    /// <summary>
    /// The folder the uploads are kept in, a relative one under the app's content root. By default a
    /// folder of the app's own under the system's temporary folder; the server logs the one it uses
    /// when it starts.
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>How many unfinished uploads one person may have at a time; one more is refused, so
    /// nobody fills the disk by starting many. Five by default.</summary>
    public int MaxUnfinished { get; set; } = 5;

    /// <summary>How long an upload is kept from the moment it starts, finished or not. A day by
    /// default.</summary>
    public TimeSpan Expiry { get; set; } = TimeSpan.FromHours(24);
}
