using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>
/// <see cref="IConsent"/> during server rendering: the answer the REQUEST carries. The browser
/// stores the visitor's reply in the consent cookie (<see cref="ConsentCookie"/>), and reading it here is what lets the first
/// paint agree with the browser — a visitor who answered last week gets a page with no banner in
/// it, instead of one that flashes and disappears on hydration.
/// <para>
/// The mutations are no-ops on purpose: consent belongs to the visitor's browser, and the reply
/// travels back on the next request. This is knowledge, not simulation — the same distinction
/// <c>AbsentCapabilities</c> draws for storage, which the server genuinely does not have.
/// </para>
/// </summary>
public sealed class SsrConsent(IHttpContextAccessor? requests = null) : IConsent
{
    public ConsentState State => ConsentCookie.Resolve(requests?.HttpContext);

    public void Grant()
    {
    }

    public void Deny()
    {
    }
}

/// <summary>
/// The consent cookie every side agrees on — the browser's <c>WebConsent</c> writes it, this reads
/// it for SSR, and the GTM installer's head script reads it to decide whether a tag manager is
/// downloaded at all. Unrecognized values read as unanswered, never as consent.
/// <para>
/// A page served over HTTPS keeps the answer in <see cref="SecureName"/>, which a browser accepts only
/// with <c>Secure</c>, from the page's own host and with no <c>Domain</c>, so a sibling subdomain or a
/// plain-HTTP hop cannot write it (#1294). A page served over HTTP, as in development, keeps it in
/// <see cref="Name"/>.
/// </para>
/// </summary>
public static class ConsentCookie
{
    /// <summary>The cookie a page served over HTTP keeps the answer in.</summary>
    public const string Name = "eq-consent";

    /// <summary>The cookie a page served over HTTPS keeps the answer in.</summary>
    public const string SecureName = "__Host-eq-consent";

    /// <summary>
    /// The answer the request carries: <see cref="SecureName"/> when the request has one, and
    /// <see cref="Name"/> only for a request that did not come over HTTPS, so a cookie another host
    /// planted never answers for an HTTPS page. Behind a proxy that ends TLS, ASP.NET Core's
    /// <c>UseForwardedHeaders</c> is what tells the request it came over HTTPS.
    /// </summary>
    public static ConsentState Resolve(HttpContext? context)
    {
        if (context is null) return ConsentState.Unknown;
        var cookies = context.Request.Cookies;
        var answer = cookies[SecureName] ?? (context.Request.IsHttps ? null : cookies[Name]);
        return answer switch
        {
            "granted" => ConsentState.Granted,
            "denied" => ConsentState.Denied,
            _ => ConsentState.Unknown,
        };
    }
}
