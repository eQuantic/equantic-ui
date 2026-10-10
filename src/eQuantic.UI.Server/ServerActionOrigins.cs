using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>
/// Which requests the Server Action endpoint takes, by where the browser says they came from (#678).
/// <list type="number">
/// <item>With an <c>Origin</c>, the request runs when the origin's host and port are the app's own
/// host, or the origin is one the app allows. Any other origin is refused, the opaque <c>null</c>
/// included, which names no site an app could allow.</item>
/// <item>Without one, <c>Sec-Fetch-Site</c> decides: <c>cross-site</c> and <c>same-site</c> are
/// refused.</item>
/// <item>With neither, the request is not a browser's, since every browser sends <c>Origin</c> on a
/// POST, and it runs.</item>
/// </list>
/// The app's own host is the request's <c>Host</c>. Behind a proxy that rewrites it, ASP.NET Core's
/// <c>UseForwardedHeaders</c> restores it from the proxies the app trusts; the raw
/// <c>X-Forwarded-Host</c> is never read here, since a site the app's CORS policy lets through could
/// write its own host into it. The host and the port are compared, a <c>Host</c> without a port standing
/// for the origin scheme's default one, since a proxy that ends TLS hands the app an <c>http</c> request
/// for an <c>https</c> page and keeps the <c>Host</c> the browser sent.
/// </summary>
internal sealed class ServerActionOrigins(IEnumerable<string> allowed)
{
    /// <summary>The origins the app allows besides its own host, each as <see cref="Normalize"/> writes it.</summary>
    private readonly HashSet<string> _allowed =
        new(allowed.Select(origin => Normalize(origin) ?? origin), StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the action runs for <paramref name="request"/>.</summary>
    public bool Allows(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        if (origin.Length == 0)
            return request.Headers["Sec-Fetch-Site"].ToString() is not ("cross-site" or "same-site");

        if (!TryParse(origin, out var uri)) return false;
        return _allowed.Contains(OriginOf(uri)) || IsOwnHost(uri, request.Host);
    }

    /// <summary>
    /// An origin as the app writes it, <c>scheme://host[:port]</c> with its default port left out, or
    /// null for a value that is not one: a scheme other than <c>http</c> and <c>https</c>, a path, a
    /// query, a fragment or user information.
    /// </summary>
    public static string? Normalize(string origin) =>
        TryParse(origin, out var uri) && uri.AbsolutePath == "/" && uri.Query.Length == 0
            && uri.Fragment.Length == 0 && uri.UserInfo.Length == 0
            ? OriginOf(uri)
            : null;

    private static bool TryParse(string text, [NotNullWhen(true)] out Uri? uri) =>
        Uri.TryCreate(text, UriKind.Absolute, out uri) && uri.Scheme is "http" or "https";

    private static string OriginOf(Uri uri) => $"{uri.Scheme}://{HostOf(uri)}";

    private static string HostOf(Uri uri) => uri.IsDefaultPort ? Literal(uri) : $"{Literal(uri)}:{uri.Port}";

    /// <summary>
    /// Whether the origin is the request's <c>Host</c>: the same host, read by the same parser so the
    /// two sides compare in one form, and the same port. A <c>Host</c> that names a port is compared with
    /// the origin's own, and one without stands for the origin scheme's default. A host that does not
    /// parse is no host of the app's.
    /// </summary>
    private static bool IsOwnHost(Uri origin, HostString host)
    {
        if (!host.HasValue || !Uri.TryCreate($"http://{host.Host}", UriKind.Absolute, out var own))
            return false;
        if (!string.Equals(Literal(origin), Literal(own), StringComparison.OrdinalIgnoreCase))
            return false;
        return host.Port is { } port ? port == origin.Port : origin.IsDefaultPort;
    }

    /// <summary>
    /// A host in one form: an IPv6 literal compressed and in its brackets, as <see cref="Uri.Host"/>
    /// writes it, and any other host in punycode. <see cref="Uri.IdnHost"/> drops an IPv6 literal's
    /// brackets, and <see cref="HostString"/> keeps whatever form the request wrote.
    /// </summary>
    private static string Literal(Uri uri) => uri.HostNameType == UriHostNameType.IPv6 ? uri.Host : uri.IdnHost;
}
