/**
 * The web twin of the C# `Navigator` seam (spec S9): PROGRAMMATIC navigation — the routes a
 * component decides to take (a command palette opening its highlighted row, a redirect after a
 * save) rather than the user clicking a link.
 *
 * A running {@link Router} installs itself here, so programmatic navigation gets the same SPA swap
 * a link click does. With no router — a page that never started one, or one already stopped — it
 * falls back to a full document load, which is the honest behavior for a plain multi-page site.
 */

import { getCurrentRoute } from './current-route';
import { splitCulturePath } from '../utils/culture-routes';
import { exception } from '../utils/exceptions';

type NavigationHandler = (destination: string) => void;

let handler: NavigationHandler | null = null;

/** Installed by the Router while it is started (null on stop). */
export function setNavigationHandler(next: NavigationHandler | null): void {
  handler = next;
}

/** What `Navigator.of(context)` answers: the C# `Navigator` instance a component navigates through. */
export interface ContextNavigator {
  go(destination: string): void;
  /**
   * The path of the page being shown (C# `Navigator.Location`), as the app writes it: the address's
   * without the language segment (`/pt-BR/charts` is `/charts`), so a layout compares it with the
   * href it wrote. The router updates the address (`pushState`) before anything builds for a
   * navigation, so a layout building around the new page reads the new path. With no document, the
   * route's.
   */
  readonly location: string;
  /**
   * C# `Navigator.OpenAsync`: the address in a new tab, beside the app (#1787). A browser lets a page
   * open one for a few seconds after a person's press, so a handler that awaited an action still may;
   * past that it blocks the tab, and the answer is false.
   */
  openAsync(address: string): Promise<boolean>;
  /** C# `Navigator.DownloadAsync`: the file at the address, downloaded under the name. */
  downloadAsync(address: string, fileName: string): Promise<boolean>;
}

/**
 * The address resolved against the page, or null for one the browser must not be handed: only `http`
 * and `https` open, so an address a server or a person wrote can never run script in a tab the page
 * opened (`javascript:`, `data:`).
 */
function opened(address: string): URL | null {
  if (!address) throw exception('System.ArgumentException', "The value cannot be an empty string. (Parameter 'address')");
  if (typeof window === 'undefined' || typeof document === 'undefined') return null;
  let url: URL;
  try {
    url = new URL(address, document.baseURI);
  } catch {
    return null;
  }
  if (url.protocol !== 'http:' && url.protocol !== 'https:') {
    console.warn(`[eQuantic] '${address}' is not opened: a page opens only http and https addresses.`);
    return null;
  }
  return url;
}

/** The new tab, cut from the page that opened it, so it cannot reach back into it (`window.opener`). */
function openTab(url: URL): boolean {
  const tab = window.open(url.href, '_blank');
  if (!tab) return false;
  try {
    tab.opener = null;
  } catch {
    // A tab of another origin may refuse the write; it is already cut from this page then.
  }
  return true;
}

export const Navigator = {
  /** Navigates to `destination` — through the active router, else a full document load. The
   * parameter is named the way the vocabulary names it; `location.assign` takes an href because
   * that is what a browser's own API calls it, which is the one place the word belongs. */
  go(destination: string): void {
    if (!destination) return;
    if (handler) {
      handler(destination);
      return;
    }
    if (typeof window !== 'undefined') window.location.assign(destination);
  },

  /**
   * The navigator for a context (the C# `Navigator.Of(context)`): the host found from the position.
   * The browser has one host per document, the router this module holds, so every context answers
   * the same navigator, which goes where `go` does.
   */
  of(_context: unknown): ContextNavigator {
    return contextNavigator;
  },
};

const contextNavigator: ContextNavigator = {
  go: (destination: string) => Navigator.go(destination),
  openAsync(address: string): Promise<boolean> {
    try {
      const url = opened(address);
      return Promise.resolve(url !== null && openTab(url));
    } catch (error) {
      return Promise.reject(error);
    }
  },
  downloadAsync(address: string, fileName: string): Promise<boolean> {
    try {
      if (!fileName) throw exception('System.ArgumentException', "The value cannot be an empty string. (Parameter 'fileName')");
      const url = opened(address);
      if (url === null) return Promise.resolve(false);
      // A browser honours the name of a file of the page's own origin and ignores another's, where it
      // would navigate the page away: that one opens in a tab instead.
      if (url.origin !== window.location.origin) return Promise.resolve(openTab(url));
      const anchor = document.createElement('a');
      anchor.href = url.href;
      anchor.download = fileName;
      anchor.style.display = 'none';
      document.body.appendChild(anchor);
      try {
        anchor.click();
      } finally {
        anchor.remove();
      }
      return Promise.resolve(true);
    } catch (error) {
      return Promise.reject(error);
    }
  },
  get location(): string {
    return typeof window !== 'undefined' ? splitCulturePath(window.location.pathname)[1] : getCurrentRoute().path;
  },
};
