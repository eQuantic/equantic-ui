/**
 * The web realization of C# `ICultureController` — the app's hand on the LANGUAGE, the shape the
 * theme controller already has (a service a component asks for by interface name, never learning
 * which target answered).
 *
 * Two effects per switch, and both are needed:
 *
 * 1. `setCulture` swaps the catalog and re-renders THIS page — no reload, D6.
 * 2. A cookie tells the SERVER, so the next request (a full navigation, a refresh, a shared link)
 *    arrives already in the chosen language instead of being corrected after hydration.
 *
 * The cookie is ASP.NET Core's OWN — name and `c=…|uic=…` format come from
 * `CookieRequestCultureProvider`, which `UseRequestLocalization` reads out of the box. That is not
 * us inventing a transport (D5 forbids that): it is us writing the one the platform already reads,
 * exactly as the theme controller writes the one the shell already reads.
 */

import { setCulture, activeCulture } from '../../utils/culture';

const CULTURE_COOKIE = '.AspNetCore.Culture';
const DEFAULT_DAYS = 365;

export class WebCultureController {
  /**
   * The UI culture in force — what resources resolve against.
   *
   * `uICulture`, not `uiCulture`: this is the camel-cased spelling of the C# `UICulture` the twin
   * calls, and the C# keeps .NET's own capitalization (`CultureInfo.CurrentUICulture`) because a
   * .NET developer reads that name every day. The odd letter lives on this side, where the only
   * reader is the transpiled call.
   */
  get uICulture(): string {
    return activeCulture().ui;
  }

  /** The format culture in force — what numbers and dates resolve against (the D13 pair). */
  get formatCulture(): string {
    return activeCulture().format;
  }

  /**
   * Applies the pair. Returns nothing on purpose: C# calls this through an interface that cannot
   * await, so the swap is fire-and-forget and the re-render lands when the catalog does. A culture
   * already in memory (the one the page booted in, or one switched to before) re-renders in the
   * same tick.
   */
  apply(uiCulture: string, formatCulture?: string): void {
    const format = formatCulture && formatCulture.length > 0 ? formatCulture : uiCulture;
    this.remember(uiCulture, format);
    void setCulture(uiCulture, format);
  }

  private remember(uiCulture: string, formatCulture: string): void {
    if (typeof document === 'undefined') return;
    try {
      const value = encodeURIComponent(`c=${formatCulture}|uic=${uiCulture}`);
      document.cookie =
        `${CULTURE_COOKIE}=${value}; path=/; max-age=${DEFAULT_DAYS * 86_400}; samesite=lax`;
    } catch {
      /* cookies blocked — the page still switched; only the NEXT request forgets */
    }
  }
}
