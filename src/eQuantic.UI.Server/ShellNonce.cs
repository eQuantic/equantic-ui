using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>
/// The nonce of one request: 128 random bits, made the first time the request asks and kept for the
/// rest of it, stamped on every script and style the shell writes so a Content-Security-Policy can
/// admit exactly those (#1266).
/// <para>
/// Per request, and not a hash of each script, because the shell carries what a hash cannot: a
/// parser-inserted <c>&lt;script src&gt;</c> (an app's <c>AddScript</c>, a chart library from its
/// CDN), which a hash admits only beside an integrity attribute and not in every browser, and scripts
/// and styles that change with every response anyway (the page's state, its assets, its atomic
/// sheet). A response is never shared between visitors, since it carries the visitor's own state, so
/// a nonce is never served twice by the SDK. The measurement is in the change's design.
/// </para>
/// </summary>
internal static class ShellNonce
{
    private static readonly object Key = new();

    /// <summary>The request's nonce, made on first use.</summary>
    public static string Of(HttpContext context)
    {
        if (context.Items.TryGetValue(Key, out var kept) && kept is string nonce) return nonce;
        var made = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[Key] = made;
        return made;
    }

    /// <summary>The attribute as an element writes it, leading space included, or nothing.</summary>
    public static string Attribute(string? nonce) =>
        nonce is null ? string.Empty : $" nonce=\"{WebUtility.HtmlEncode(nonce)}\"";
}
