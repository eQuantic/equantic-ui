namespace eQuantic.UI.Web;

/// <summary>
/// Which destinations an href may carry: one with no scheme (a path, relative or rooted, a query, a
/// fragment, a protocol-relative address) or one whose scheme is <c>http</c>, <c>https</c>,
/// <c>mailto</c> or <c>tel</c>. Any other scheme is dropped (#1286). The scheme is read as a browser's
/// URL parser reads it, with its letter case and the characters that parser ignores folded away.
/// <para>
/// Its TypeScript twin is <c>allowsScheme</c> in <c>utils/link-scheme.ts</c>, and the parity case
/// <c>links-with-schemes</c> holds the two to the same answer.
/// </para>
/// </summary>
internal static class LinkScheme
{
    /// <summary>Whether an href may carry <paramref name="destination"/>.</summary>
    public static bool Allows(string destination) => SchemeOf(destination) switch
    {
        null or "http" or "https" or "mailto" or "tel" => true,
        _ => false,
    };

    /// <summary>The scheme a browser reads in <paramref name="destination"/>, lower-cased, or null when it
    /// reads none.</summary>
    private static string? SchemeOf(string destination)
    {
        var start = 0;
        var end = destination.Length;
        while (start < end && destination[start] <= ' ') start++;
        while (end > start && destination[end - 1] <= ' ') end--;
        var scheme = new System.Text.StringBuilder();
        for (var i = start; i < end; i++)
        {
            var c = destination[i];
            if (c is '\t' or '\n' or '\r') continue;
            if (c == ':') return scheme.Length > 0 ? scheme.ToString() : null;
            var letter = c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
            if (scheme.Length == 0 ? !letter : !(letter || c is >= '0' and <= '9' or '+' or '-' or '.')) return null;
            scheme.Append(char.ToLowerInvariant(c));
        }
        return null;
    }
}
