/**
 * The web realization of C# `IThemeController` — the app's hand on the light/dark switch.
 *
 * The whole web palette is authored as `light-dark()` pairs under `color-scheme: light dark`
 * (see C# TokenCss), so the browser already owns the swap: forcing a mode is ONE declaration on
 * the root element, and every token — custom property, atomic class, SSR-baked value — resolves
 * to the other half in the same paint. No re-render, no theme rebuild.
 */
/**
 * The cookie the SERVER reads to paint the right theme on the very first byte.
 *
 * A cookie rather than localStorage, and the distinction is the whole point: the requirement is not
 * "remember" but "tell the server". localStorage remembers perfectly and the server cannot see a
 * word of it, so the page would arrive in the default mode and be corrected at hydration — the
 * flash this exists to remove.
 *
 * Written from the BROWSER with document.cookie, which costs nothing. A round trip per toggle would
 * be the obvious design and is not needed: the client writes it, the next request carries it.
 *
 * SameSite=Lax and no Secure flag: this is a display preference, it must survive a link from
 * another site, and it has to work on http://localhost during development.
 */
const THEME_COOKIE = 'eq-theme';
const DEFAULT_DAYS = 365;

export class WebThemeController {
  /** 'light' | 'dark' — the forced mode if one is set, else what the OS prefers. */
  get mode(): string {
    const forced = document.documentElement.style.colorScheme;
    if (forced === 'dark' || forced === 'light') return forced;
    return typeof window.matchMedia === 'function' &&
      window.matchMedia('(prefers-color-scheme: dark)').matches
      ? 'dark'
      : 'light';
  }

  apply(mode: string): void {
    const resolved = mode === 'dark' ? 'dark' : 'light';
    document.documentElement.style.colorScheme = resolved;
    // …and the flag anything CSS cannot express through color-scheme reads: a themed image PAIR
    // (Image.DarkSource) picks its artwork from this, because no CSS function switches a URL.
    document.documentElement.setAttribute('data-theme', resolved);

    // Remembering is this controller's job, not every app's. Without it each app reimplements the
    // same cookie, and the ones that do not have a site that forgets on every reload.
    const cookie = window.__EQ_CONFIG?.themeCookie;
    // `false` is the app having said not to — a consent banner, or a policy that forbids a cookie
    // before it is granted. The mode still applies to THIS page; it simply does not outlive it.
    if (cookie === false) return;

    const name = cookie?.name ?? THEME_COOKIE;
    const maxAge = (cookie?.days ?? DEFAULT_DAYS) * 86_400;
    try {
      document.cookie = `${name}=${resolved}; path=/; max-age=${maxAge}; samesite=lax`;
    } catch {
      /* cookies blocked by the browser — same outcome, and not ours to report */
    }
  }
}
