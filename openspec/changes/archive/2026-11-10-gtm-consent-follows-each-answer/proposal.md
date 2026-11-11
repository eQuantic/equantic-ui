## Why

Closes #1294, a Bug the security review (#1265) found in the consent the GTM installer gates on, low in
each of its parts:

- with `WithConsent()`, the page listened only for a grant, so a denial after a grant left full consent
  in place until a reload;
- one grant turned on all four Google Consent Mode signals, with no way for an app to say less;
- the answer's cookie had no `Secure` and no `__Host-` prefix, so a sibling subdomain or a plain-HTTP hop
  could write it, and the server read it as the visitor's answer;
- the installer's scripts quoted configured names by hand, which a line break or U+2028 breaks;
- and, left to the owner: a plain `UseGtm` wrote the container before any consent.

## What Changes

- A plain `UseGtm` waits for the visitor's consent, every Consent Mode signal `denied` until then (the
  owner's decision). An app whose visitors no consent rule covers says so with `GtmOptions.WithoutConsent()`.
- Every answer updates the Consent Mode signals: a denial turns them all back to `denied` on the page.
- A grant turns on `analytics_storage` alone (the owner's decision). The three advertising signals are
  granted only when the app names them with `GtmOptions.WithConsentGrants(…)`, so its card asks for them.
- Over HTTPS the answer is kept in `__Host-eq-consent` (`ConsentCookie.SecureName`), with `Secure`, and
  the browser's `IConsent`, the server's `SsrConsent` and the installer's script read only that one there.
  A page served over HTTP keeps `eq-consent`. Behind a proxy that ends TLS, ASP.NET Core's
  `UseForwardedHeaders` tells the server the request came over HTTPS.
- The scripts write configured names as JSON string literals, by `JsonEncodedText`.
- Breaking, written in the wiki's Upgrading page: a plain `UseGtm` waits for consent; a grant no longer
  turns on the advertising signals unless the app names them; `GtmOptions.WithConsent()` is gone, since
  consent is the default. A visitor whose answer an HTTPS app kept in `eq-consent` is asked again once.

## Capabilities

### New Capabilities

- `consent`: the visitor's answer to non-essential tracking, where it is kept and what the GTM installer
  does with it.

### Modified Capabilities

(none)
