/**
 * The web realization of C# `IConsent` — the visitor's answer to non-essential tracking, held in
 * ONE cookie (`eq-consent`) that every side reads: this class in the browser, `SsrConsent` on the
 * server (so the first paint already knows, and no banner flashes for a visitor who answered last
 * week), and the GTM installer's head script, which loads the container only on a granted answer.
 *
 * Changing the answer announces it on the document as `eq:consent` — the same shape the router
 * uses for `eq:navigate` — so a collector installed before the runtime booted can wait for it
 * without knowing this class exists.
 */

/** The cookie name the C# side (`ConsentCookie.Name`) and the GTM installer share. */
export const CONSENT_COOKIE = 'eq-consent';

/** The C# `ConsentState` as it crosses the boundary: enum members travel as camelCase names. */
export type ConsentStateValue = 'unknown' | 'granted' | 'denied';

const ONE_YEAR_SECONDS = 365 * 86_400;

export class WebConsent {
  /** The C# `State` twin, read from the cookie every time — another tab may have answered. */
  get state(): ConsentStateValue {
    if (typeof document === 'undefined') return 'unknown';
    const match = document.cookie.match(new RegExp(`(?:^|; )${CONSENT_COOKIE}=([^;]*)`));
    if (!match) return 'unknown';
    let value: string;
    try {
      value = decodeURIComponent(match[1]);
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
    document.cookie = `${CONSENT_COOKIE}=${state}; path=/; max-age=${ONE_YEAR_SECONDS}; samesite=lax`;
    document.dispatchEvent(new CustomEvent('eq:consent', { detail: { state } }));
  }
}
