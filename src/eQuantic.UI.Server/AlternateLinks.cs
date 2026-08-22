using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>
/// What the app answers when the SDK asks "where does THIS page live in that language?". One
/// delegate rather than a setting, because the answer is a URL policy and every site spells it
/// differently: a path segment, a query, a subdomain, or a slug that is simply another page.
/// </summary>
/// <param name="Culture">The BCP-47 name being asked about — one of the app's supported cultures.</param>
/// <param name="Request">The request being rendered, for its path, query, scheme and host.</param>
public readonly record struct AlternateRequest(string Culture, HttpRequest Request);

/// <summary>
/// The URL shapes a localized site usually takes, so an app that uses one of them writes a name
/// instead of a lambda. Each returns a delegate for <see cref="UIOptions.UseAlternateLinks"/>.
/// <para>
/// All of them answer an ABSOLUTE url, because a relative <c>hreflang</c> is the one mistake in
/// this area that looks fine in the markup and is dropped silently by every crawler.
/// </para>
/// </summary>
public static class AlternateUrls
{
    /// <summary>
    /// The culture as the FIRST path segment — <c>/pt-BR/pricing</c>. The shape Google recommends
    /// and the one an app gets by wiring <c>RequestLocalization</c> with a route-value provider.
    /// A segment that already names a culture is REPLACED rather than stacked, so the link is
    /// right whichever translation the visitor is currently reading.
    /// </summary>
    /// <param name="cultures">The app's languages, so a segment is a culture because it was
    /// DECLARED rather than because it looks like one — a page called <c>pt</c> is a page.</param>
    /// <param name="defaultCulture">The language served with NO prefix, when the site keeps its
    /// original URLs and prefixes only the translations. Its alternate is the bare path, and it is
    /// the natural <c>x-default</c>. Omit it and every language is prefixed.</param>
    public static Func<AlternateRequest, string?> PathPrefix(
        IEnumerable<string>? cultures = null, string? defaultCulture = null)
    {
        var known = cultures?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return request =>
        {
            var path = request.Request.Path.HasValue ? request.Request.Path.Value! : "/";
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (segments.Count > 0 && LooksLikeCulture(segments[0], known)) segments.RemoveAt(0);
            if (!string.Equals(request.Culture, defaultCulture, StringComparison.OrdinalIgnoreCase))
                segments.Insert(0, request.Culture);
            return Absolute(request.Request, "/" + string.Join('/', segments));
        };
    }

    /// <summary>
    /// The culture as a query parameter — <c>/pricing?culture=pt-BR</c>. What an app gets from the
    /// stock <c>QueryStringRequestCultureProvider</c>, and the cheapest thing to wire; a path
    /// segment reads better to a person and to a crawler.
    /// </summary>
    public static Func<AlternateRequest, string?> QueryString(string name = "culture") =>
        request =>
        {
            var query = request.Request.Query
                .Where(pair => !string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value.ToString())}")
                .Append($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(request.Culture)}");
            var path = request.Request.Path.HasValue ? request.Request.Path.Value! : "/";
            return Absolute(request.Request, $"{path}?{string.Join('&', query)}");
        };

    /// <summary>
    /// A path or a whole URL, made absolute against the request. An app's own lambda can return
    /// either and get the same treatment the built-ins give themselves.
    /// </summary>
    public static string Absolute(HttpRequest request, string pathOrUrl)
    {
        // A ROOTED PATH first, and by its own shape: `Uri.TryCreate("/es/pricing", Absolute, …)`
        // answers true and hands back `file:///es/pricing`, which is a URL, just not one the site
        // is served from. Asking about the scheme is what tells a path from an address.
        if (!pathOrUrl.StartsWith('/')
            && Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var already)
            && already.Scheme is "http" or "https")
            return already.ToString();
        var separator = pathOrUrl.StartsWith('/') ? "" : "/";
        return $"{request.Scheme}://{request.Host}{separator}{pathOrUrl}";
    }

    /// <summary>
    /// Whether a path segment is the culture rather than the page. The app's own list decides when
    /// it was given; otherwise the SHAPE does (`pt`, `pt-BR`), which is what tells a language
    /// segment from a page called `pricing` without hardcoding either.
    /// </summary>
    private static bool LooksLikeCulture(string segment, HashSet<string>? known)
    {
        if (known is not null) return known.Contains(segment);
        var parts = segment.Split('-');
        if (parts.Length is < 1 or > 3) return false;
        if (parts[0].Length is < 2 or > 3 || !parts[0].All(char.IsLetter)) return false;
        return parts.Skip(1).All(part => part.Length is 2 or 4 && part.All(char.IsLetterOrDigit));
    }
}
