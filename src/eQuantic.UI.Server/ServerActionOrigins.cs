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
/// The app's own host is the request's <c>Host</c>, or the first <c>X-Forwarded-Host</c>: a browser
/// sends that header from another site only after a CORS preflight, which the endpoint never answers.
/// Only the host and the port are compared, a scheme's default port left out, since a proxy that ends
/// TLS hands the app an <c>http</c> request for an <c>https</c> page.
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
        if (_allowed.Contains(OriginOf(uri))) return true;
        var host = HostOf(uri);
        return string.Equals(host, OwnHost(request.Host), StringComparison.OrdinalIgnoreCase)
            || ForwardedHost(request) is { } forwarded && string.Equals(host, forwarded, StringComparison.OrdinalIgnoreCase);
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

    private static string HostOf(Uri uri) => uri.IsDefaultPort ? uri.IdnHost : $"{uri.IdnHost}:{uri.Port}";

    private static string OwnHost(HostString host) => host.Port is 80 or 443 ? host.Host : host.Value ?? "";

    private static string? ForwardedHost(HttpRequest request)
    {
        var forwarded = request.Headers["X-Forwarded-Host"].ToString();
        if (forwarded.Length == 0) return null;
        var first = forwarded.Split(',', 2)[0].Trim();
        return first.Length == 0 ? null : OwnHost(new HostString(first));
    }
}
