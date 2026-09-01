using eQuantic.UI.Primitives;
using Microsoft.AspNetCore.Http;

namespace eQuantic.UI.Server;

/// <summary>
/// <see cref="IConsent"/> during server rendering: the answer the REQUEST carries. The browser
/// stores the visitor's reply in <c>eq-consent</c>, and reading it here is what lets the first
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
/// </summary>
public static class ConsentCookie
{
    public const string Name = "eq-consent";

    public static ConsentState Resolve(HttpContext? context) =>
        context?.Request.Cookies[Name] switch
        {
            "granted" => ConsentState.Granted,
            "denied" => ConsentState.Denied,
            _ => ConsentState.Unknown,
        };
}
