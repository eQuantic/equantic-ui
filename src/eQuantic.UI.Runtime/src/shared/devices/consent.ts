/**
 * The web realization of C# `IConsent` — the visitor's answer to non-essential tracking, held in
 * `__Host-eq-consent` on a page served over HTTPS and in `eq-consent` over HTTP, the one cookie every
 * side reads for that page: this class in the browser, `SsrConsent` on the
 * server (so the first paint already knows, and no banner flashes for a visitor who answered last
 * week), and the GTM installer's head script, which loads the container only on a granted answer.
 *
 * Changing the answer announces it on the document as `eq:consent` — the same shape the router
 * uses for `eq:navigate` — so a collector installed before the runtime booted can wait for it
 * without knowing this class exists.
 */

/** The cookie a page served over HTTP keeps the answer in (C# `ConsentCookie.Name`). */
export const CONSENT_COOKIE = 'eq-consent';

/**
 * The cookie a page served over HTTPS keeps the answer in (C# `ConsentCookie.SecureName`): a browser accepts
 * it only with `Secure`, from the page's own host and with no `Domain`, so a sibling subdomain or a plain-HTTP
 * hop cannot write the answer for it (#1294).
 */
export const SECURE_CONSENT_COOKIE = '__Host-eq-consent';

/** The cookie this page keeps the answer in, and whether it is the secure one. */
function consentCookie(): { name: string; secure: boolean } {
  const secure = typeof location !== 'undefined' && location.protocol === 'https:';
  return { name: secure ? SECURE_CONSENT_COOKIE : CONSENT_COOKIE, secure };
}

/** The C# `ConsentState` as it crosses the boundary: enum members travel as camelCase names. */
export type ConsentStateValue = 'unknown' | 'granted' | 'denied';

const ONE_YEAR_SECONDS = 365 * 86_400;

export class WebConsent {
  /** The C# `State` twin, read from the cookie every time — another tab may have answered. */
  get state(): ConsentStateValue {
    if (typeof document === 'undefined') return 'unknown';
    const { name } = consentCookie();
    const match = document.cookie
      .split('; ')
      .find((cookie) => cookie.startsWith(`${name}=`))
      ?.slice(name.length + 1);
    if (match === undefined) return 'unknown';
    let value: string;
    try {
      value = decodeURIComponent(match);
    } catch {
      // A malformed escape is a tampered or foreign cookie — it reads as unanswered, never as
      // consent, and never as an exception in the middle of a render.
      return 'unknown';
    }
    return value === 'granted' || value === 'denied' ? value : 'unknown';
  }

  grant(): void {
    this.answer('granted');
  }

  deny(): void {
    this.answer('denied');
  }

  private answer(state: 'granted' | 'denied'): void {
    if (typeof document === 'undefined') return;
    const { name, secure } = consentCookie();
    document.cookie = `${name}=${state}; path=/; max-age=${ONE_YEAR_SECONDS}; samesite=lax${secure ? '; secure' : ''}`;
    document.dispatchEvent(new CustomEvent('eq:consent', { detail: { state } }));
  }
}
