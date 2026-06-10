import { type RouteEntry, type RouteMatch, matchRoute } from './route-table';

/**
 * Called when the router activates a route — load the page's bundle and render it into the app root.
 * Provided by the host (`boot.ts`), which knows the module path and mount mechanics; keeping it injected
 * leaves the {@link Router} itself free of framework/DOM-loading concerns and easy to test.
 *
 * `isCurrent()` lets the (async) handler bail out before it renders if a newer navigation has started in
 * the meantime — guarding against a slow load clobbering a more recent one (out-of-order navigations).
 */
export type NavigateHandler = (
  match: RouteMatch,
  url: URL,
  isCurrent: () => boolean,
) => void | Promise<void>;

export interface RouterOptions {
  routes: readonly RouteEntry[];
  onNavigate: NavigateHandler;
  /** The window to bind to — injectable so tests can drive a happy-dom window. Defaults to `window`. */
  win?: Window;
}

/**
 * Client-side router (Phase 2, M0). Turns internal navigation into SPA navigation: it intercepts
 * eligible same-origin `<a>` clicks, pushes a History entry, and asks the host to render the matched
 * page — no full reload — and re-renders on `popstate` (back/forward). Links that don't match a known
 * route, or that aren't SPA-eligible (external, `target=_blank`, modified click, `download`, `rel=external`,
 * `data-native`, hash-only), are left to the browser, so server-rendered/non-SPA pages keep working.
 */
export class Router {
  private readonly routes: readonly RouteEntry[];
  private readonly onNavigate: NavigateHandler;
  private readonly win: Window;
  private readonly clickListener: (e: MouseEvent) => void;
  private readonly popListener: () => void;
  private started = false;
  /** Monotonic navigation id — the host's handler compares against it to ignore superseded loads. */
  private navToken = 0;

  constructor(options: RouterOptions) {
    this.routes = options.routes;
    this.onNavigate = options.onNavigate;
    this.win = options.win ?? window;
    this.clickListener = (e) => this.onClick(e);
    this.popListener = () => void this.dispatch(this.win.location.href);
  }

  /** Begins intercepting navigation. Idempotent. */
  start(): void {
    if (this.started) return;
    this.started = true;
    this.win.document.addEventListener('click', this.clickListener, true);
    this.win.addEventListener('popstate', this.popListener);
  }

  /** Stops intercepting navigation. */
  stop(): void {
    if (!this.started) return;
    this.started = false;
    this.win.document.removeEventListener('click', this.clickListener, true);
    this.win.removeEventListener('popstate', this.popListener);
  }

  /**
   * Programmatic navigation: if `href` matches a route, push a History entry and render it (SPA);
   * otherwise fall back to a full browser navigation. Returns whether it was handled in-SPA.
   */
  async navigate(href: string): Promise<boolean> {
    const url = new URL(href, this.win.location.href);
    const match = matchRoute(this.routes, url.pathname);
    if (!match) {
      this.win.location.assign(href);
      return false;
    }
    this.win.history.pushState(null, '', url.pathname + url.search + url.hash);
    await this.activate(match, url);
    return true;
  }

  /** Renders whatever the current URL resolves to (used on `popstate`). */
  private async dispatch(href: string): Promise<void> {
    const url = new URL(href, this.win.location.href);
    const match = matchRoute(this.routes, url.pathname);
    if (match) await this.activate(match, url);
  }

  /**
   * Begins rendering a matched route: stamps a fresh navigation id (so a later navigation supersedes
   * this one), updates the document title, and hands off to the host handler with a staleness check.
   */
  private activate(match: RouteMatch, url: URL): Promise<void> {
    const token = ++this.navToken;
    if (match.title) this.win.document.title = match.title;
    return Promise.resolve(this.onNavigate(match, url, () => token === this.navToken));
  }

  private onClick(e: MouseEvent): void {
    if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) {
      return;
    }
    const anchor = (e.target as Element | null)?.closest?.('a') as HTMLAnchorElement | null;
    if (!anchor || !this.isEligible(anchor)) return;

    const url = new URL(anchor.href, this.win.location.href);
    if (url.origin !== this.win.location.origin) return; // external

    // Hash-only / same-path link (e.g. "#section", "/here#top" while already on /here): let the browser
    // do the in-page scroll — re-mounting the page would lose it. Only intercept real path changes.
    const current = new URL(this.win.location.href);
    if (url.pathname === current.pathname && url.search === current.search) return;

    const match = matchRoute(this.routes, url.pathname);
    if (!match) return; // unknown route → let the browser navigate (server fallthrough)

    e.preventDefault();
    this.win.history.pushState(null, '', url.pathname + url.search + url.hash);
    void this.activate(match, url);
  }

  /** Whether an anchor opts into (rather than out of) SPA interception. */
  private isEligible(a: HTMLAnchorElement): boolean {
    if (!a.getAttribute('href')) return false;
    const target = a.getAttribute('target');
    if (target && target !== '_self') return false;
    if (a.hasAttribute('download')) return false;
    if (a.hasAttribute('data-native')) return false;
    const rel = a.getAttribute('rel');
    if (rel && /(^|\s)external(\s|$)/.test(rel)) return false;
    return true;
  }
}
