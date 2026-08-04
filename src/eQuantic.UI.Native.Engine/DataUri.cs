namespace eQuantic.UI.Native.Engine;

/// <summary>
/// Bytes carried inside a URI. An image the user just picked has no path worth handing around — it
/// may never have been a file — so the source string carries the bytes themselves, which every
/// target already understands: the browser natively, the native loaders through here.
/// </summary>
public static class DataUri
{
    private const string Prefix = "data:";
    private const string Base64Marker = ";base64,";

    /// <summary>
    /// The bytes a <c>data:…;base64,…</c> source carries. False for anything else — including a
    /// perfectly good file path, which is the common case and must stay cheap to reject.
    /// </summary>
    public static bool TryDecode(string source, out byte[]? bytes)
    {
        bytes = null;
        if (!source.StartsWith(Prefix, StringComparison.Ordinal)) return false;

        var marker = source.IndexOf(Base64Marker, StringComparison.Ordinal);
        if (marker < 0) return false;    // a percent-encoded data URI: not an image, not our business

        try
        {
            bytes = Convert.FromBase64String(source[(marker + Base64Marker.Length)..]);
            return true;
        }
        catch (FormatException)
        {
            return false;                // truncated or mangled — the caller sees "no image"
        }
    }
}
