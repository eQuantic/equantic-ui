using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>
/// Whose an upload is: the signed-in person, by the identifier their sign-in gives
/// (<see cref="ClaimTypes.NameIdentifier"/>, else <c>sub</c>), or else the browser that sent it, by a
/// cookie made when it starts its first upload. Only the owner continues an upload, asks how far it
/// has come, deletes it or receives it in a Server Action.
/// <para>
/// The cookie is strictly necessary for the upload the person asked for, so it needs no consent: it
/// holds a random id and nothing else, is never read by script, and is sent only to the app's own
/// site. It is written again by each upload the browser starts, with the uploads' expiry as its
/// lifetime, so it outlives every upload it names: a lifetime counted from the FIRST upload would end
/// in the middle of a later one, which then belongs to nobody.
/// </para>
/// </summary>
internal static class UploadOwner
{
    /// <summary>The cookie that names a visitor's uploads.</summary>
    public const string Cookie = "eq-uploads";

    private const string UserPrefix = "user:";
    private const string VisitorPrefix = "visitor:";

    /// <summary>The visitor id this request made, kept so every event of one creation names one owner.</summary>
    private static readonly object MadeVisitor = new();

    /// <summary>The owner the request speaks for, or null when it has neither a sign-in nor the cookie.</summary>
    public static string? Of(HttpContext context)
    {
        if (UserId(context.User) is { } user) return UserPrefix + user;
        return VisitorOf(context) is { } visitor ? VisitorPrefix + visitor : null;
    }

    private static string? VisitorOf(HttpContext context) =>
        context.Items[MadeVisitor] as string ?? VisitorId(context.Request);

    /// <summary>
    /// The owner the request speaks for, making a visitor id when it has neither a sign-in nor the
    /// cookie. The id is written into the cookie by <see cref="Remember"/>, once the upload is made.
    /// </summary>
    public static string Resolve(HttpContext context)
    {
        if (Of(context) is { } owner) return owner;
        var made = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        context.Items[MadeVisitor] = made;
        return VisitorPrefix + made;
    }

    /// <summary>Whether <paramref name="owner"/> is a signed-in person rather than a browser.</summary>
    /// <summary>
    /// Whether the request speaks for an upload's owner: as the signed-in person, or as the browser
    /// that started it, whose cookie it still carries. Both count, because a person who uploads and
    /// then signs in to go on (choose a file, then sign in to order) is the same person in the same
    /// browser, and the upload they started must not become someone else's.
    /// </summary>
    public static bool Owns(HttpContext context, string owner) =>
        (UserId(context.User) is { } user && owner == UserPrefix + user)
        || (VisitorOf(context) is { } visitor && owner == VisitorPrefix + visitor);

    public static bool IsSignedIn(string owner) => owner.StartsWith(UserPrefix, StringComparison.Ordinal);

    /// <summary>Writes the visitor's cookie, for <paramref name="lifetime"/> from now; a signed-in owner
    /// needs none.</summary>
    public static void Remember(HttpContext context, TimeSpan lifetime)
    {
        var owner = Resolve(context);
        if (!owner.StartsWith(VisitorPrefix, StringComparison.Ordinal)) return;
        context.Response.Cookies.Append(Cookie, owner[VisitorPrefix.Length..], new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            // Secure but for plain HTTP to this machine, a developer's: a request that reached the app as
            // HTTP from a proxy that ended TLS, with no forwarded headers to say so, wrote it without, and the
            // cookie that names the uploads went out in clear on the next plain request (#1293).
            Secure = context.Request.IsHttps || !IsLoopback(context.Request.Host.Host),
            IsEssential = true,
            MaxAge = lifetime,
            Path = "/",
        });
    }

    private static bool IsLoopback(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
        || (System.Net.IPAddress.TryParse(host.Trim('[', ']'), out var address) && System.Net.IPAddress.IsLoopback(address));

    private static string? UserId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
        && (user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub"))?.Value is { Length: > 0 } id
            ? id
            : null;

    /// <summary>The cookie's id, when it is one this server could have made: 32 lowercase hex digits.</summary>
    private static string? VisitorId(HttpRequest request) =>
        request.Cookies.TryGetValue(Cookie, out var id) && id is { Length: 32 } && id.All(char.IsAsciiHexDigitLower)
            ? id
            : null;
}
