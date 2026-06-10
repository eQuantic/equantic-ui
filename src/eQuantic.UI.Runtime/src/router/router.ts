import { type RouteEntry, type RouteMatch, matchRoute } from './route-table';
import { setCurrentRouteFrom } from './current-route';

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

/** Where to scroll after a navigation renders: the top of the page, or a restored position (back/forward). */
type ScrollTarget = 'top' | { x: number; y: number };

interface HistoryScrollState {
  eqScroll?: { x: number; y: number };
}

/**
 * Client-side router (Phase 2). Turns internal navigation into SPA navigation: it intercepts eligible
 * same-origin `<a>` clicks, pushes a History entry, and asks the host to render the matched page — no
 * full reload — and re-renders on `popstate` (back/forward). It also publishes the active route's
 * params/query (so `context.Route` works), updates `document.title`, and manages scroll position
 * (reset to top on a forward navigation, restored on back/forward). Links that don't match a known
 * route, or that aren't SPA-eligible (external, `target=_blank`, modified click, `download`,
 * `rel=external`, `data-native`, hash-only), are left to the browser, so server-rendered/non-SPA pages
 * keep working.
 */
export class Router {
  private readonly routes: readonly RouteEntry[];
  private readonly onNavigate: NavigateHandler;
  private readonly win: Window;
  private readonly clickListener: (e: MouseEvent) => void;
  private readonly popListener: (e: PopStateEvent) => void;
  private started = false;
  /** Monotonic navigation id — the host's handler compares against it to ignore superseded loads. */
  private navToken = 0;

  constructor(options: RouterOptions) {
    this.routes = options.routes;
    this.onNavigate = options.onNavigate;
    this.win = options.win ?? window;
    this.clickListener = (e) => this.onClick(e);
    this.popListener = (e) => void this.dispatch(this.win.location.href, e.state);
  }

  /** Begins intercepting navigation. Idempotent. */
  start(): void {
    if (this.started) return;
    this.started = true;
    // Own scroll restoration so it stays in sync with our async page swaps (default 'auto' restores
    // before the new page has rendered).
    if ('scrollRestoration' in this.win.history) {
      this.win.history.scrollRestoration = 'manual';
    }
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
    await this.goForward(match, url);
    return true;
  }

  /** A forward navigation: save the outgoing scroll, push the new entry, render, then scroll to top. */
  private goForward(match: RouteMatch, url: URL): Promise<void> {
    this.saveScroll();
    this.win.history.pushState(null, '', url.pathname + url.search + url.hash);
    return this.activate(match, url, 'top');
  }

  /** Renders whatever the current URL resolves to (used on `popstate`), restoring its saved scroll. */
  private async dispatch(href: string, state: unknown): Promise<void> {
    const url = new URL(href, this.win.location.href);
    const match = matchRoute(this.routes, url.pathname);
    if (!match) return;
    const saved = (state as HistoryScrollState | null)?.eqScroll;
    await this.activate(match, url, saved ?? 'top');
  }

  /**
   * Begins rendering a matched route: stamps a fresh navigation id (so a later navigation supersedes
   * this one), publishes the route data, updates the document title, hands off to the host handler with
   * a staleness check, and applies the scroll once it renders (if still current).
   */
  private async activate(match: RouteMatch, url: URL, scroll: ScrollTarget): Promise<void> {
    const token = ++this.navToken;
    setCurrentRouteFrom(match, url);
    if (match.title) this.win.document.title = match.title;
    await Promise.resolve(this.onNavigate(match, url, () => token === this.navToken));
    if (token === this.navToken) this.applyScroll(scroll);
  }

  /** Stores the current scroll position on the current History entry, for restoration on back/forward. */
  private saveScroll(): void {
    const state = (this.win.history.state as HistoryScrollState | null) ?? {};
    this.win.history.replaceState(
      { ...state, eqScroll: { x: this.win.scrollX, y: this.win.scrollY } },
      '',
    );
  }

  private applyScroll(scroll: ScrollTarget): void {
    if (scroll === 'top') this.win.scrollTo(0, 0);
    else this.win.scrollTo(scroll.x, scroll.y);
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
    void this.goForward(match, url);
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
