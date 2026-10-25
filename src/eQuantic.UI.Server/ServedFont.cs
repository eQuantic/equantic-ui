using eQuantic.UI.Primitives;

namespace eQuantic.UI.Server;

/// <summary>
/// One file of a face the web serves: the family it belongs to, the file as declared, its bytes as
/// read from the assembly it is embedded in, and the name it is served under, addressed by those
/// bytes so the browser keeps it for as long as it lasts.
/// </summary>
/// <param name="Family">The family the file belongs to.</param>
/// <param name="File">The file as the family declares it.</param>
/// <param name="Name">The name it is served under: a hash of its bytes and its extension.</param>
/// <param name="Bytes">The file's bytes.</param>
/// <param name="Brotli">The same bytes compressed with Brotli, once: an OpenType file is served raw, and
/// it compresses to about half, which is most of what WOFF2 would have saved.</param>
internal sealed record ServedFont(FontFamily Family, FontFile File, string Name, byte[] Bytes, byte[] Brotli)
{
    /// <summary>Where a page fetches it.</summary>
    public string Url => "/_equantic/fonts/" + Name;
}
