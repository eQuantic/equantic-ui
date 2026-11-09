## Why

Closes #1294, a Bug the security review (#1265) found in the consent the GTM installer gates on, low in
each of its parts:

- with `WithConsent()`, the page listened only for a grant, so a denial after a grant left full consent
  in place until a reload;
- one grant turned on all four Google Consent Mode signals, with no way for an app to say less;
- the answer's cookie had no `Secure` and no `__Host-` prefix, so a sibling subdomain or a plain-HTTP hop
  could write it, and the server read it as the visitor's answer;
- the installer's scripts quoted configured names by hand, which a line break or U+2028 breaks.

## What Changes

- Every answer updates the Consent Mode signals: a denial turns them all back to `denied` on the page.
- `GtmOptions.ConsentGrants` (`WithConsent(signals)`) names the signals a grant turns on; the rest stay
  `denied`. All four by default, since one question asks for them: the app's consent card says what it
  covers.
- Over HTTPS the answer is kept in `__Host-eq-consent` (`ConsentCookie.SecureName`), with `Secure`, and
  the browser's `IConsent`, the server's `SsrConsent` and the installer's script read only that one there.
  A page served over HTTP keeps `eq-consent`. Behind a proxy that ends TLS, ASP.NET Core's
  `UseForwardedHeaders` tells the server the request came over HTTPS.
- The scripts write configured names as JSON string literals, by `JsonEncodedText`.
- Recorded, not changed: a plain `UseGtm("GTM-…")` writes the container before any consent, and gating
  stays opt-in. Whether the default should be gated is the owner's decision.
- Breaking: `GtmOptions.WithConsent()` takes an optional `GtmConsentSignals`. A visitor whose answer an
  HTTPS app kept in `eq-consent` is asked again once.

## Capabilities

### New Capabilities

- `consent`: the visitor's answer to non-essential tracking, where it is kept and what the GTM installer
  does with it.

### Modified Capabilities

(none)
