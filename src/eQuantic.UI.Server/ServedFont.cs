using eQuantic.UI.Web;

namespace eQuantic.UI.Server;

/// <summary>
/// One font file the web serves: the resource it was read from, its bytes, and the name it is served
/// under, addressed by those bytes so the browser keeps it for as long as it lasts. A file belongs to no
/// family here: the same file may set two, and one a browser asks for by its identity names none.
/// </summary>
/// <param name="Resource">The manifest resource it was read from, which says what kind of font it is.</param>
/// <param name="Name">The name it is served under: a hash of its bytes and its extension.</param>
/// <param name="Bytes">The file's bytes.</param>
/// <param name="Brotli">The same bytes compressed with Brotli, once: an OpenType file is served raw, and
/// it compresses to about half, which is most of what WOFF2 would have saved.</param>
internal sealed record ServedFont(string Resource, string Name, byte[] Bytes, byte[] Brotli)
{
    /// <summary>Where a page fetches it.</summary>
    public string Url => "/_equantic/fonts/" + Name;

    /// <summary>The media type it is served as.</summary>
    public string ContentType => FontFaceRules.ContentType(Resource);
}
