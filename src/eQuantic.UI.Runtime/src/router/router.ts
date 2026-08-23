import { setNavigationHandler } from './navigator';
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

/**
 * Called to warm a matched route ahead of navigation (hover/focus prefetch). The host loads the page
 * bundle so a later click resolves from cache. Best-effort: failures are swallowed by the router.
 */
export type PrefetchHandler = (match: RouteMatch, url: URL) => void | Promise<void>;

/** The navigation a guard is being asked to approve. */
export interface PendingNavigation {
  match: RouteMatch;
  url: URL;
}

/**
 * What a guard decides for a pending navigation:
 *  - `true`/`undefined` → allow (continue to the next guard, then navigate);
 *  - `false` → cancel (stay put — the URL is left unchanged);
 *  - a string → redirect to that href instead.
 */
export type GuardResult = boolean | string | void;

/**
 * A navigation guard runs BEFORE a forward navigation commits (before `pushState`/render), so a cancel
 * leaves the URL untouched. Receives the pending navigation and the current location. May be async (e.g.
 * to check an auth token). Guards run in registration order; the first to cancel or redirect wins.
 */
export type NavigationGuard = (
  to: PendingNavigation,
  from: URL,
) => GuardResult | Promise<GuardResult>;

export interface RouterOptions {
  routes: readonly RouteEntry[];
  onNavigate: NavigateHandler;
  /**
   * Optional: warm a route's bundle on hover/focus of an eligible link marked `data-prefetch`. Without
   * it, prefetch is simply disabled (navigation still works).
   */
  onPrefetch?: PrefetchHandler;
  /** Navigation guards consulted before a forward navigation commits (can cancel or redirect). */
  guards?: readonly NavigationGuard[];
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
  private readonly onPrefetch?: PrefetchHandler;
  private readonly win: Window;
  private readonly clickListener: (e: MouseEvent) => void;
  private readonly popListener: (e: PopStateEvent) => void;
  private readonly prefetchListener: (e: Event) => void;
  private started = false;
  /** Monotonic navigation id — the host's handler compares against it to ignore superseded loads. */
  private navToken = 0;
  /** Hrefs already handed to the prefetch handler, so repeated hovers don't reload the same bundle. */
  private readonly prefetched = new Set<string>();
  /** Guards consulted before each forward navigation commits. */
  private readonly guards: NavigationGuard[];

  constructor(options: RouterOptions) {
    this.routes = options.routes;
    this.onNavigate = options.onNavigate;
    this.onPrefetch = options.onPrefetch;
    this.guards = options.guards ? [...options.guards] : [];
    this.win = options.win ?? window;
    this.clickListener = (e) => this.onClick(e);
    this.popListener = (e) => void this.dispatch(this.win.location.href, e.state);
    this.prefetchListener = (e) => this.onPrefetchEvent(e);
  }

  /** Begins intercepting navigation. Idempotent. */
  start(): void {
    if (this.started) return;
    this.started = true;
    // Programmatic navigation (the C# Navigator seam) routes through THIS router while it runs, so
    // a component that navigates by logic gets the same SPA swap a link click does.
    setNavigationHandler((href) => void this.navigate(href));
    // Own scroll restoration so it stays in sync with our async page swaps (default 'auto' restores
    // before the new page has rendered).
    if ('scrollRestoration' in this.win.history) {
      this.win.history.scrollRestoration = 'manual';
    }
    this.win.document.addEventListener('click', this.clickListener, true);
    this.win.addEventListener('popstate', this.popListener);
    // Hover/focus prefetch (only when a handler was provided). pointerover/focusin both bubble, so a
    // single delegated listener covers every link, including SSR-rendered ones, with no per-element wiring.
    if (this.onPrefetch) {
      this.win.document.addEventListener('pointerover', this.prefetchListener, true);
      this.win.document.addEventListener('focusin', this.prefetchListener, true);
    }
  }

  /** Stops intercepting navigation. */
  stop(): void {
    if (!this.started) return;
    this.started = false;
    setNavigationHandler(null);
    this.win.document.removeEventListener('click', this.clickListener, true);
    this.win.removeEventListener('popstate', this.popListener);
    this.win.document.removeEventListener('pointerover', this.prefetchListener, true);
    this.win.document.removeEventListener('focusin', this.prefetchListener, true);
  }

  /**
   * Warm a route's page bundle ahead of navigation. Resolves the href against the route table and, for a
   * not-yet-prefetched same-origin match that isn't the current page, hands it to the prefetch handler
   * exactly once. Safe to call repeatedly; unmatched/external hrefs are ignored.
   */
  prefetch(href: string): void {
    if (!this.onPrefetch) return;
    const url = new URL(href, this.win.location.href);
    if (url.origin !== this.win.location.origin) return;
    const key = url.pathname + url.search;
    if (this.prefetched.has(key)) return;
    if (url.pathname === this.win.location.pathname) return; // already here
    const match = matchRoute(this.routes, url.pathname);
    if (!match) return;
    this.prefetched.add(key);
    void Promise.resolve(this.onPrefetch(match, url)).catch(() => this.prefetched.delete(key));
  }

  private onPrefetchEvent(e: Event): void {
    const anchor = (e.target as Element | null)?.closest?.('a') as HTMLAnchorElement | null;
    if (!anchor || !anchor.hasAttribute('data-prefetch') || !this.isEligible(anchor)) return;
    this.prefetch(anchor.href);
  }

  /**
   * Programmatic navigation: if `href` matches a route, push a History entry and render it (SPA);
   * otherwise fall back to a full browser navigation. Returns whether it was handled in-SPA.
   */
  async navigate(href: string): Promise<boolean> {
    const url = new URL(href, this.win.location.href);

    // ANOTHER ORIGIN is another site, whatever its path happens to say. The click path refuses one
    // (see onClick); this one matched on the PATHNAME alone, so `Navigator.Go("https://
    // ui.equantic.tech/docs")` found the local /docs route and rendered it — the reader stayed on
    // this site looking at the wrong page, with the address bar agreeing. A link to a documentation
    // site is exactly the shape that hits it.
    if (url.origin !== this.win.location.origin) {
      this.win.location.assign(href);
      return false;
    }

    const match = matchRoute(this.routes, url.pathname);
    if (!match) {
      this.win.location.assign(href);
      return false;
    }
    await this.goForward(match, url);
    return true;
  }

  /** Register a navigation guard at runtime (consulted before each forward navigation). */
  addGuard(guard: NavigationGuard): void {
    this.guards.push(guard);
  }

  /**
   * A forward navigation. With no guards registered this stays synchronous up to `pushState` (so the URL
   * updates immediately, as callers expect). When guards exist they're consulted first — asynchronously,
   * since a guard may be async — and a cancel leaves the URL untouched while a redirect navigates elsewhere.
   */
  private goForward(match: RouteMatch, url: URL, keepPosition = false): Promise<void> {
    if (this.guards.length === 0) {
      return this.commitForward(match, url, keepPosition);
    }
    return this.goForwardGuarded(match, url, keepPosition);
  }

  private async goForwardGuarded(
    match: RouteMatch,
    url: URL,
    keepPosition: boolean,
  ): Promise<void> {
    const decision = await this.runGuards(match, url);
    if (decision === false) return; // cancelled — leave the URL untouched
    if (typeof decision === 'string') {
      await this.navigate(decision); // redirect
      return;
    }
    return this.commitForward(match, url, keepPosition); // decision === true → allowed
  }

  /**
   * Commit an approved forward navigation: save outgoing scroll, push the entry, render, and start
   * the new page at its top — unless the link asked to KEEP THE READER'S POSITION.
   *
   * Arriving at the top is right for a link that takes you somewhere else, and wrong for one that
   * swaps a panel beside a list you were half-way down. A documentation sidebar is the case that
   * names it: the shell is preserved, a couple of hundred nodes are patched, and then everything
   * jumps to the top — which is indistinguishable, to a reader, from the page having reloaded.
   */
  private commitForward(match: RouteMatch, url: URL, keepPosition = false): Promise<void> {
    const stay: ScrollTarget = { x: this.win.scrollX, y: this.win.scrollY };
    this.saveScroll();
    this.win.history.pushState(null, '', url.pathname + url.search + url.hash);
    return this.activate(match, url, keepPosition ? stay : 'top');
  }

  /**
   * Run guards in order against a pending navigation. Returns `true` (allow), `false` (cancel), or a
   * redirect href string. The first guard to cancel or redirect short-circuits the rest.
   */
  private async runGuards(match: RouteMatch, url: URL): Promise<true | false | string> {
    if (this.guards.length === 0) return true;
    const to: PendingNavigation = { match, url };
    const from = new URL(this.win.location.href);
    for (const guard of this.guards) {
      const result = await Promise.resolve(guard(to, from));
      if (result === false) return false;
      if (typeof result === 'string') return result;
    }
    return true;
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
    // The one navigation signal the DOCUMENT carries — the extension point for anything that is
    // not the framework (analytics installers, most obviously: a SPA navigation is a page view no
    // tag manager can see on its own). Fired only for the navigation that WON: a stale token is a
    // navigation that was overtaken mid-flight, and it was never a page view.
    if (token === this.navToken) {
      this.win.document.dispatchEvent(
        new CustomEvent('eq:navigate', {
          detail: { path: url.pathname, search: url.search, title: this.win.document.title },
        }),
      );
    }
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
    // The LINK decides where the new page starts (Link.KeepsPosition → data-eq-keep-position).
    void this.goForward(match, url, anchor.hasAttribute('data-eq-keep-position'));
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
