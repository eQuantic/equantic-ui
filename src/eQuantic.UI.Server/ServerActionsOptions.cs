namespace eQuantic.UI.Server;

/// <summary>
/// What the Server Action endpoint accepts beyond the app's own pages, read from
/// <c>EQuantic:ServerActions</c> in configuration and added to with
/// <see cref="UIOptions.AllowServerActionOrigins"/>.
/// </summary>
public sealed class ServerActionsOptions
{
    /// <summary>The configuration section the options are read from.</summary>
    public const string SectionName = "EQuantic:ServerActions";

    /// <summary>
    /// The origins whose pages may call the app's actions besides its own host, for a page served from
    /// another domain than its actions. Each is a bare <c>scheme://host[:port]</c>, with no path.
    /// </summary>
    public IList<string> AllowedOrigins { get; } = new List<string>();

    /// <summary>
    /// The largest request body an action reads, in bytes: 1 MB unless the app says otherwise. A body past
    /// it is refused with 413, whether or not an earlier middleware already started reading the request
    /// (#1291). A file goes up through the app's uploads, which have their own limits, not as an argument.
    /// </summary>
    public long MaxRequestBodySize { get; set; } = 1_048_576;
}
