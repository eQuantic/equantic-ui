import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { matchRoute, matchPattern, type RouteEntry } from './route-table';
import { Router, getCurrentRoute, type RouteMatch, type NavigationGuard } from '../index';

describe('matchPattern / matchRoute', () => {
  const routes: RouteEntry[] = [
    { pattern: '/', page: 'Home' },
    { pattern: '/counter', page: 'Counter' },
    { pattern: '/users/new', page: 'NewUser' },
    { pattern: '/users/{id:int}', page: 'User' },
  ];

  it('matches static routes', () => {
    expect(matchRoute(routes, '/')).toEqual({ page: 'Home', params: {} });
    expect(matchRoute(routes, '/counter')).toEqual({ page: 'Counter', params: {} });
  });

  it('matches literals case-insensitively', () => {
    expect(matchRoute(routes, '/Counter')?.page).toBe('Counter');
  });

  it('captures route parameters (decoded)', () => {
    expect(matchRoute(routes, '/users/42')).toEqual({ page: 'User', params: { id: '42' } });
    expect(matchPattern('/users/{id}', '/users/a%20b')).toEqual({ id: 'a b' });
  });

  it('prefers a static route over a parameterised one', () => {
    expect(matchRoute(routes, '/users/new')?.page).toBe('NewUser');
  });

  /**
   * A path segment is user input, and `decodeURIComponent` throws on a malformed escape. `/users/%`
   * raised a URIError from inside the click handler and the popstate handler alike — a URL nobody
   * can decode now matches no route, which is the answer an unknown path already gets.
   */
  it('treats a malformed escape as no match rather than throwing', () => {
    expect(() => matchPattern('/users/{id}', '/users/%')).not.toThrow();
    expect(matchPattern('/users/{id}', '/users/%')).toBeNull();
    expect(matchRoute(routes, '/users/%')).toBeNull();
    expect(matchRoute(routes, '/users/%E0%A4%A')).toBeNull();
    // A well-formed escape still decodes, so the guard cannot be "give up on percent signs".
    expect(matchPattern('/users/{id}', '/users/a%20b')).toEqual({ id: 'a b' });
  });

  it('returns null for unknown / wrong-arity paths', () => {
    expect(matchRoute(routes, '/missing')).toBeNull();
    expect(matchRoute(routes, '/users/1/extra')).toBeNull();
    expect(matchPattern('/a/{x}', '/a')).toBeNull();
  });

  it('honors inline constraints (:int rejects non-numeric, :guid the shape)', () => {
    const intRoutes: RouteEntry[] = [{ pattern: '/users/{id:int}', page: 'User' }];
    expect(matchRoute(intRoutes, '/users/42')?.params).toEqual({ id: '42' });
    expect(matchRoute(intRoutes, '/users/-7')?.params).toEqual({ id: '-7' });
    expect(matchRoute(intRoutes, '/users/abc')).toBeNull();
    const guidRoutes: RouteEntry[] = [{ pattern: '/t/{id:guid}', page: 'T' }];
    expect(matchRoute(guidRoutes, '/t/123')).toBeNull();
    expect(matchRoute(guidRoutes, '/t/3f2504e0-4f89-41d3-9a0c-0305e82c3301')?.params.id).toBe(
      '3f2504e0-4f89-41d3-9a0c-0305e82c3301',
    );
  });

  it('accepts unknown constraints (server validates)', () => {
    const r: RouteEntry[] = [{ pattern: '/p/{slug:minlength(2)}', page: 'P' }];
    expect(matchRoute(r, '/p/hello')?.params).toEqual({ slug: 'hello' });
  });
});

describe('Router (happy-dom)', () => {
  let onNavigate: ReturnType<typeof vi.fn>;
  let router: Router;
  const routes: RouteEntry[] = [
    { pattern: '/', page: 'Home' },
    { pattern: '/counter', page: 'Counter', title: 'Counter — App' },
    { pattern: '/users/{id}', page: 'User' },
  ];

  beforeEach(() => {
    // Fully reset the happy-dom URL between tests (a prior SPA nav leaves it on another path).
    const hd = (window as unknown as { happyDOM?: { setURL?: (u: string) => void } }).happyDOM;
    if (hd?.setURL) hd.setURL('http://localhost:3000/');
    else window.history.replaceState(null, '', '/');
    document.body.innerHTML = '';
    onNavigate = vi.fn();
    router = new Router({ routes, onNavigate, win: window });
    router.start();
  });

  afterEach(() => router.stop());

  describe('navigate(href) and another origin', () => {
    // `location.assign` is the WINDOW's, shared by every test in this worker — replacing it and
    // walking away makes some later test fail for a reason it has nothing to do with. The whole
    // DESCRIPTOR goes back, not just the value: defineProperty defaults the fields you leave out,
    // so putting back `{ value, configurable }` would pin `writable` and `enumerable` to false for
    // good. Where there was no own descriptor, the override is deleted and the prototype's shows
    // through again.
    const originalAssign = Object.getOwnPropertyDescriptor(window.location, 'assign');
    afterEach(() => {
      if (originalAssign) Object.defineProperty(window.location, 'assign', originalAssign);
      else delete (window.location as unknown as Record<string, unknown>).assign;
    });

    /** The window's own `location.assign`, watched — the router calls it to leave the SPA. */
    function watchAssign() {
      const assign = vi.fn();
      Object.defineProperty(window.location, 'assign', { value: assign, configurable: true });
      return assign;
    }

    it('leaves the site when the href names another origin, even if the PATH matches a route', async () => {
      // The shape the site hit: a documentation link whose path happens to be one of ours. Matching
      // on pathname alone rendered the LOCAL page and kept the reader here, quietly.
      const assign = watchAssign();

      const handled = await router.navigate('https://ui.equantic.tech/counter');

      expect(handled).toBe(false);
      expect(assign).toHaveBeenCalledWith('https://ui.equantic.tech/counter');
      expect(onNavigate).not.toHaveBeenCalled();
    });

    it('still navigates in-SPA for the same origin, spelled absolutely', async () => {
      const assign = watchAssign();

      const handled = await router.navigate('http://localhost:3000/counter');

      expect(handled).toBe(true);
      expect(assign).not.toHaveBeenCalled();
      expect(onNavigate).toHaveBeenCalled();
    });

    it('still navigates in-SPA for a relative href', async () => {
      const assign = watchAssign();

      const handled = await router.navigate('/counter');

      expect(handled).toBe(true);
      expect(assign).not.toHaveBeenCalled();
    });

    it('leaves the site for another origin whose path matches nothing either', async () => {
      const assign = watchAssign();

      await router.navigate('https://example.com/nowhere');

      expect(assign).toHaveBeenCalledWith('https://example.com/nowhere');
    });
  });

  function anchor(attrs: Record<string, string>): HTMLAnchorElement {
    const a = document.createElement('a');
    for (const [k, v] of Object.entries(attrs)) a.setAttribute(k, v);
    a.textContent = 'link';
    document.body.appendChild(a);
    return a;
  }

  function click(a: HTMLAnchorElement): MouseEvent {
    const e = new window.MouseEvent('click', { bubbles: true, cancelable: true, button: 0 });
    a.dispatchEvent(e);
    return e;
  }

  it('announces a committed navigation on the document — the analytics extension point', async () => {
    const seen: Array<{ path: string; title: string }> = [];
    const listen = (e: Event) => {
      const detail = (e as CustomEvent<{ path: string; search: string; title: string }>).detail;
      seen.push({ path: detail.path, title: detail.title });
    };
    document.addEventListener('eq:navigate', listen);

    await router.navigate('/counter');

    document.removeEventListener('eq:navigate', listen);
    expect(seen).toEqual([{ path: '/counter', title: 'Counter — App' }]);
  });

  it('intercepts an internal matched link → SPA nav (no reload), pushes history', () => {
    const e = click(anchor({ href: '/counter' }));
    expect(e.defaultPrevented).toBe(true);
    expect(onNavigate).toHaveBeenCalledTimes(1);
    const [match] = onNavigate.mock.calls[0] as [RouteMatch, URL];
    expect(match.page).toBe('Counter');
    expect(window.location.pathname).toBe('/counter');
  });

  it('passes captured params to the navigation handler', () => {
    click(anchor({ href: '/users/7' }));
    const [match] = onNavigate.mock.calls[0] as [RouteMatch, URL];
    expect(match.page).toBe('User');
    expect(match.params).toEqual({ id: '7' });
  });

  it('ignores unknown routes (lets the browser/server handle it)', () => {
    const e = click(anchor({ href: '/not-a-route' }));
    expect(e.defaultPrevented).toBe(false);
    expect(onNavigate).not.toHaveBeenCalled();
  });

  it('ignores SPA-ineligible links (target, download, rel=external, data-native)', () => {
    expect(click(anchor({ href: '/counter', target: '_blank' })).defaultPrevented).toBe(false);
    expect(click(anchor({ href: '/counter', download: '' })).defaultPrevented).toBe(false);
    expect(click(anchor({ href: '/counter', rel: 'external' })).defaultPrevented).toBe(false);
    expect(click(anchor({ href: '/counter', 'data-native': '' })).defaultPrevented).toBe(false);
    expect(onNavigate).not.toHaveBeenCalled();
  });

  it('ignores hash-only / same-path links (lets the browser scroll, no re-mount)', () => {
    const hd = (window as unknown as { happyDOM?: { setURL?: (u: string) => void } }).happyDOM;
    if (hd?.setURL) hd.setURL('http://localhost:3000/counter');
    else window.history.replaceState(null, '', '/counter');
    // A pure hash link and a same-path link with a hash, while already on /counter.
    expect(click(anchor({ href: '#section' })).defaultPrevented).toBe(false);
    expect(click(anchor({ href: '/counter#top' })).defaultPrevented).toBe(false);
    expect(onNavigate).not.toHaveBeenCalled();
  });

  it('ignores modified clicks (ctrl/meta/shift/alt, non-left button)', () => {
    const a = anchor({ href: '/counter' });
    const e = new window.MouseEvent('click', {
      bubbles: true,
      cancelable: true,
      button: 0,
      metaKey: true,
    });
    a.dispatchEvent(e);
    expect(e.defaultPrevented).toBe(false);
    expect(onNavigate).not.toHaveBeenCalled();
  });

  it('programmatic navigate() pushes history and renders a matched route', async () => {
    const handled = await router.navigate('/users/9');
    expect(handled).toBe(true);
    expect(window.location.pathname).toBe('/users/9');
    expect(onNavigate).toHaveBeenCalledTimes(1);
  });

  it('re-renders the current URL on popstate (back/forward)', async () => {
    window.history.pushState(null, '', '/counter');
    onNavigate.mockClear();
    window.dispatchEvent(new window.PopStateEvent('popstate'));
    // microtask: the handler awaits onNavigate
    await Promise.resolve();
    expect(onNavigate).toHaveBeenCalledTimes(1);
    const [match] = onNavigate.mock.calls[0] as [RouteMatch, URL];
    expect(match.page).toBe('Counter');
  });

  it('updates document.title from the matched route', async () => {
    await router.navigate('/counter');
    expect(document.title).toBe('Counter — App');
  });

  it('race-guard: a superseded navigation is told it is no longer current', () => {
    const seen: Array<{ page: string; isCurrent: () => boolean }> = [];
    const r = new Router({
      routes,
      win: window,
      onNavigate: (m, _u, isCurrent) => {
        // never resolves — both navigations stay in flight
        seen.push({ page: m.page, isCurrent });
        return new Promise<void>(() => {});
      },
    });
    r.start();
    void r.navigate('/counter'); // first (slower)
    void r.navigate('/users/5'); // second (newer) supersedes it
    r.stop();
    expect(seen).toHaveLength(2);
    expect(seen[0].isCurrent()).toBe(false); // first is stale
    expect(seen[1].isCurrent()).toBe(true); // second is current
  });

  it('publishes route params and query to the current route (context.Route)', async () => {
    await router.navigate('/users/9?tab=info');
    expect(getCurrentRoute().param('id')).toBe('9');
    expect(getCurrentRoute().query('tab')).toBe('info');
    expect(getCurrentRoute().param('missing')).toBeUndefined();
  });

  it('resets scroll to top on a forward navigation', async () => {
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
    await router.navigate('/counter');
    expect(scrollTo).toHaveBeenCalledWith(0, 0);
    scrollTo.mockRestore();
  });

  /**
   * A link may ask to KEEP THE READER'S POSITION. Arriving at the top is right for a link that takes
   * you somewhere else, and wrong for one that swaps a panel beside a list you were half-way down —
   * a documentation sidebar being the case that names it. The shell survives the navigation either
   * way; what changes is whether the reader loses their place in it.
   */
  it('keeps the position when the link asked to', async () => {
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
    Object.defineProperty(window, 'scrollY', { value: 640, configurable: true });

    const a = anchor({ href: '/counter', 'data-eq-keep-position': '' });
    a.click();
    await Promise.resolve();
    await Promise.resolve();

    expect(scrollTo).toHaveBeenCalledWith(0, 640);
    expect(scrollTo).not.toHaveBeenCalledWith(0, 0);
    scrollTo.mockRestore();
  });

  /**
   * THE DEFECT THAT MADE `Bookmark` USELESS on a hydrated page. Chrome fires `popstate` for a
   * same-document fragment navigation, so the `#section` click the router deliberately steps aside
   * for comes back through `dispatch` a moment later. That entry was pushed by the BROWSER, so it
   * carries none of our saved position, and the old default sent the page to the top — undoing the
   * in-page scroll the router had just declined to intercept.
   */
  it('honours the fragment on a popstate the browser pushed, instead of going to the top', async () => {
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
    const target = document.createElement('div');
    target.id = 'rights';
    const scrollIntoView = vi.fn();
    target.scrollIntoView = scrollIntoView;
    document.body.appendChild(target);

    window.history.pushState(null, '', '/counter#rights');
    window.dispatchEvent(new window.PopStateEvent('popstate'));
    await Promise.resolve();
    await Promise.resolve();

    expect(scrollIntoView).toHaveBeenCalled();
    expect(scrollTo).not.toHaveBeenCalledWith(0, 0);
    scrollTo.mockRestore();
    target.remove();
  });

  /** A real back/forward returns the reader where they were, hash or no hash. */
  it('prefers a saved position over the fragment on a genuine traversal', async () => {
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
    const target = document.createElement('div');
    target.id = 'rights';
    const scrollIntoView = vi.fn();
    target.scrollIntoView = scrollIntoView;
    document.body.appendChild(target);

    window.history.pushState({ eqScroll: { x: 0, y: 900 } }, '', '/counter#rights');
    // The listener reads the EVENT's state, which is what a browser hands it on a traversal.
    window.dispatchEvent(
      new window.PopStateEvent('popstate', { state: { eqScroll: { x: 0, y: 900 } } }),
    );
    await Promise.resolve();
    await Promise.resolve();

    expect(scrollTo).toHaveBeenCalledWith(0, 900);
    expect(scrollIntoView).not.toHaveBeenCalled();
    scrollTo.mockRestore();
    target.remove();
  });

  /** And the forward half, which was wrong the same way: `/guide#install` ignored its fragment. */
  it('honours the fragment on a forward navigation to another page', async () => {
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
    const target = document.createElement('div');
    target.id = 'install';
    const scrollIntoView = vi.fn();
    target.scrollIntoView = scrollIntoView;
    document.body.appendChild(target);

    await router.navigate('/counter#install');

    expect(scrollIntoView).toHaveBeenCalled();
    expect(scrollTo).not.toHaveBeenCalledWith(0, 0);
    scrollTo.mockRestore();
    target.remove();
  });

  /**
   * Arriving at ANOTHER page still starts at the top when its fragment names nothing — a new page
   * does, and keeping the last one's offset would be a position belonging to something the reader
   * is no longer looking at. The opposite of the traversal rule below, and the two are not the same
   * question: one changed the document, the other did not.
   */
  it('starts a forward navigation at the top when its fragment names no element', async () => {
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});

    await router.navigate('/counter#nothing-here');

    expect(scrollTo).toHaveBeenCalledWith(0, 0);
    scrollTo.mockRestore();
  });

  /** A fragment that names nothing leaves the page alone on a TRAVERSAL, rather than jumping it to
   * the top: the document has not changed, and jumping is the defect this fixes. */
  it('leaves the position when the fragment names no element', async () => {
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {});

    window.history.pushState(null, '', '/counter#nothing-here');
    window.dispatchEvent(new window.PopStateEvent('popstate'));
    await Promise.resolve();
    await Promise.resolve();

    expect(scrollTo).not.toHaveBeenCalled();
    scrollTo.mockRestore();
  });

  /**
   * A traversal to a URL the router cannot match leaves the reader on the previous page under the
   * new address — the browser moved the URL before telling us, and returning silently means the
   * document and the address bar disagree with nothing on screen to say so. Measured live on
   * `/careers/%`: the title was still the previous document's. The click path already defers to the
   * browser for what it cannot route, and the server has a 404 page; a traversal asks for the same.
   */
  it('lets the server answer a traversal it cannot route, instead of leaving a stale page', async () => {
    const reload = vi.spyOn(window.location, 'reload').mockImplementation(() => {});
    try {
      window.history.pushState(null, '', '/no-such-route');
      window.dispatchEvent(new window.PopStateEvent('popstate'));
      await Promise.resolve();
      await Promise.resolve();

      expect(reload).toHaveBeenCalled();
      expect(onNavigate).not.toHaveBeenCalled();
    } finally {
      reload.mockRestore();
      window.history.replaceState(null, '', '/');
    }
  });

  it('sets manual scroll restoration when supported', () => {
    if ('scrollRestoration' in window.history) {
      expect(window.history.scrollRestoration).toBe('manual');
    }
  });

  it('stop() detaches the listeners', () => {
    router.stop();
    click(anchor({ href: '/counter' }));
    expect(onNavigate).not.toHaveBeenCalled();
  });
});

describe('Router prefetch (happy-dom)', () => {
  let onPrefetch: ReturnType<typeof vi.fn>;
  let router: Router;
  const routes: RouteEntry[] = [
    { pattern: '/', page: 'Home' },
    { pattern: '/counter', page: 'Counter' },
  ];

  beforeEach(() => {
    const hd = (window as unknown as { happyDOM?: { setURL?: (u: string) => void } }).happyDOM;
    if (hd?.setURL) hd.setURL('http://localhost:3000/');
    else window.history.replaceState(null, '', '/');
    document.body.innerHTML = '';
    onPrefetch = vi.fn();
    router = new Router({ routes, onNavigate: vi.fn(), onPrefetch, win: window });
    router.start();
  });

  afterEach(() => router.stop());

  function link(href: string, attrs: Record<string, string> = {}): HTMLAnchorElement {
    const a = document.createElement('a');
    a.setAttribute('href', href);
    for (const [k, v] of Object.entries(attrs)) a.setAttribute(k, v);
    a.textContent = 'link';
    document.body.appendChild(a);
    return a;
  }

  const hover = (a: HTMLAnchorElement) =>
    a.dispatchEvent(new window.Event('pointerover', { bubbles: true }));

  it('prefetches a matched data-prefetch link on hover (once, then deduped)', () => {
    const a = link('/counter', { 'data-prefetch': 'true' });
    hover(a);
    hover(a);
    expect(onPrefetch).toHaveBeenCalledTimes(1);
    const [match] = onPrefetch.mock.calls[0] as [RouteMatch, URL];
    expect(match.page).toBe('Counter');
  });

  it('ignores links without data-prefetch', () => {
    hover(link('/counter'));
    expect(onPrefetch).not.toHaveBeenCalled();
  });

  it('ignores unknown routes, the current page, and external origins', () => {
    hover(link('/missing', { 'data-prefetch': 'true' }));
    hover(link('/', { 'data-prefetch': 'true' })); // already here
    hover(link('https://example.com/counter', { 'data-prefetch': 'true' }));
    expect(onPrefetch).not.toHaveBeenCalled();
  });

  it('prefetch() is also callable programmatically and respects dedup', () => {
    router.prefetch('/counter');
    router.prefetch('/counter');
    expect(onPrefetch).toHaveBeenCalledTimes(1);
  });

  it('does not prefetch after stop()', () => {
    router.stop();
    hover(link('/counter', { 'data-prefetch': 'true' }));
    expect(onPrefetch).not.toHaveBeenCalled();
  });
});

describe('Router guards (happy-dom)', () => {
  let onNavigate: ReturnType<typeof vi.fn>;
  const routes: RouteEntry[] = [
    { pattern: '/', page: 'Home' },
    { pattern: '/admin', page: 'Admin' },
    { pattern: '/login', page: 'Login' },
  ];

  function makeRouter(guards: NavigationGuard[]): Router {
    const r = new Router({ routes, onNavigate, guards, win: window });
    r.start();
    return r;
  }

  beforeEach(() => {
    const hd = (window as unknown as { happyDOM?: { setURL?: (u: string) => void } }).happyDOM;
    if (hd?.setURL) hd.setURL('http://localhost:3000/');
    else window.history.replaceState(null, '', '/');
    document.body.innerHTML = '';
    onNavigate = vi.fn();
  });

  it('allows navigation when guards return true/undefined', async () => {
    const r = makeRouter([() => true, () => undefined]);
    await r.navigate('/admin');
    expect(onNavigate).toHaveBeenCalledTimes(1);
    expect(window.location.pathname).toBe('/admin');
    r.stop();
  });

  it('cancels navigation (URL unchanged, page not rendered) when a guard returns false', async () => {
    const r = makeRouter([() => false]);
    await r.navigate('/admin');
    expect(onNavigate).not.toHaveBeenCalled();
    expect(window.location.pathname).toBe('/'); // stayed put
    r.stop();
  });

  it('redirects when a guard returns an href, landing on the redirect target', async () => {
    const seen: string[] = [];
    const r = makeRouter([
      (to) => {
        seen.push(to.match.page);
        return to.match.page === 'Admin' ? '/login' : true;
      },
    ]);
    await r.navigate('/admin');
    expect(window.location.pathname).toBe('/login');
    const pages = onNavigate.mock.calls.map((c) => (c[0] as RouteMatch).page);
    expect(pages).toEqual(['Login']); // Admin was blocked; only Login rendered
    expect(seen).toEqual(['Admin', 'Login']); // guard ran for both, allowed Login
    r.stop();
  });

  it('passes the pending target and current location to the guard', async () => {
    const calls: Array<{ to: string; from: string }> = [];
    const r = makeRouter([
      (to, from) => {
        calls.push({ to: to.url.pathname, from: from.pathname });
        return true;
      },
    ]);
    await r.navigate('/admin');
    expect(calls).toEqual([{ to: '/admin', from: '/' }]);
    r.stop();
  });

  it('addGuard registers a guard at runtime', async () => {
    const r = makeRouter([]);
    r.addGuard(() => false);
    await r.navigate('/admin');
    expect(onNavigate).not.toHaveBeenCalled();
    r.stop();
  });

  it('blocks a guarded link click (first guard to cancel wins)', async () => {
    const r = makeRouter([() => true, () => false]);
    const a = document.createElement('a');
    a.setAttribute('href', '/admin');
    document.body.appendChild(a);
    a.dispatchEvent(new window.MouseEvent('click', { bubbles: true, cancelable: true, button: 0 }));
    await Promise.resolve();
    await Promise.resolve();
    expect(onNavigate).not.toHaveBeenCalled();
    expect(window.location.pathname).toBe('/');
    r.stop();
  });
});
