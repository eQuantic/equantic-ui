/**
 * The web twin of the C# `Navigator` seam (spec S9): PROGRAMMATIC navigation — the routes a
 * component decides to take (a command palette opening its highlighted row, a redirect after a
 * save) rather than the user clicking a link.
 *
 * A running {@link Router} installs itself here, so programmatic navigation gets the same SPA swap
 * a link click does. With no router — a page that never started one, or one already stopped — it
 * falls back to a full document load, which is the honest behavior for a plain multi-page site.
 */

type NavigationHandler = (href: string) => void;

let handler: NavigationHandler | null = null;

/** Installed by the Router while it is started (null on stop). */
export function setNavigationHandler(next: NavigationHandler | null): void {
  handler = next;
}

export const Navigator = {
  /** Navigates to `href` — through the active router, else a full document load. */
  go(href: string): void {
    if (!href) return;
    if (handler) {
      handler(href);
      return;
    }
    if (typeof window !== 'undefined') window.location.assign(href);
  },
};
