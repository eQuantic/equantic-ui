/**
 * The web realization of C# `IAppStorage` — `localStorage`, which is the browser's own answer to
 * "keep this between visits" and the one its devtools, its site-data controls and its clear-data
 * button already know about.
 *
 * Every call is guarded, because `localStorage` is the DOM API most likely to throw at you rather
 * than fail quietly: Safari's private mode has historically thrown on write, a browser at its quota
 * throws `QuotaExceededError`, and reading it at all throws outright when the origin has storage
 * blocked (third-party frame, `Block all cookies`). A preference failing to save must not take a
 * page down with it, so a write that cannot happen simply does not, and a read that cannot happen
 * answers null — which is the same answer the caller already has to handle for a key never set.
 *
 * Deliberately NOT sessionStorage: this contract says "between launches", and the native stores it
 * stands in for (NSUserDefaults, SharedPreferences) all outlive the process.
 *
 * There is no `WebSecretStore` beside this on purpose — see ISecretStore. The browser has no vault,
 * and a store every script on the origin can read is not one.
 */
export class WebAppStorage {
  get(key: string): string | null {
    try {
      return localStorage.getItem(key);
    } catch {
      // Storage blocked for this origin — indistinguishable, to a caller, from an unset key.
      return null;
    }
  }

  set(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      /* full, or blocked — the preference does not persist, and the page carries on */
    }
  }

  remove(key: string): void {
    try {
      localStorage.removeItem(key);
    } catch {
      /* nothing to do and nothing to report: the value is already unreachable */
    }
  }
}
