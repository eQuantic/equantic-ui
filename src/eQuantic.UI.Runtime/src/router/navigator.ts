/**
 * The web twin of the C# `Navigator` seam (spec S9): PROGRAMMATIC navigation — the routes a
 * component decides to take (a command palette opening its highlighted row, a redirect after a
 * save) rather than the user clicking a link.
 *
 * A running {@link Router} installs itself here, so programmatic navigation gets the same SPA swap
 * a link click does. With no router — a page that never started one, or one already stopped — it
 * falls back to a full document load, which is the honest behavior for a plain multi-page site.
 */

import { RouteValues } from '../shared/route-values';
import { splitCulturePath } from '../utils/culture-routes';

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
  get location(): string {
    return typeof window !== 'undefined' ? splitCulturePath(window.location.pathname)[1] : RouteValues.current.path;
  },
};
