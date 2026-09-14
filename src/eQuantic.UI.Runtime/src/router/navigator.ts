/**
 * The web twin of the C# `Navigator` seam (spec S9): PROGRAMMATIC navigation — the routes a
 * component decides to take (a command palette opening its highlighted row, a redirect after a
 * save) rather than the user clicking a link.
 *
 * A running {@link Router} installs itself here, so programmatic navigation gets the same SPA swap
 * a link click does. With no router — a page that never started one, or one already stopped — it
 * falls back to a full document load, which is the honest behavior for a plain multi-page site.
 */

type NavigationHandler = (destination: string) => void;

let handler: NavigationHandler | null = null;

/** Installed by the Router while it is started (null on stop). */
export function setNavigationHandler(next: NavigationHandler | null): void {
  handler = next;
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
};
