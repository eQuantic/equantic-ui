import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { matchRoute, matchPattern, type RouteEntry } from './route-table';
import { Router, type RouteMatch } from '../index';

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

  it('returns null for unknown / wrong-arity paths', () => {
    expect(matchRoute(routes, '/missing')).toBeNull();
    expect(matchRoute(routes, '/users/1/extra')).toBeNull();
    expect(matchPattern('/a/{x}', '/a')).toBeNull();
  });
});

describe('Router (happy-dom)', () => {
  let onNavigate: ReturnType<typeof vi.fn>;
  let router: Router;
  const routes: RouteEntry[] = [
    { pattern: '/', page: 'Home' },
    { pattern: '/counter', page: 'Counter' },
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

  it('ignores modified clicks (ctrl/meta/shift/alt, non-left button)', () => {
    const a = anchor({ href: '/counter' });
    const e = new window.MouseEvent('click', { bubbles: true, cancelable: true, button: 0, metaKey: true });
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

  it('stop() detaches the listeners', () => {
    router.stop();
    click(anchor({ href: '/counter' }));
    expect(onNavigate).not.toHaveBeenCalled();
  });
});
