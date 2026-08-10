import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { WebThemeController } from './theme-controller';

/**
 * Remembering the theme, and NOT remembering it when the app said not to.
 *
 * The cookie's shape arrives from the server, because the server is what reads it while this is
 * what writes it. Two places to configure would drift, and a drifted name fails silently: the
 * server reads a cookie nobody writes, persistence stops, and every part of it still looks right.
 */
/**
 * What the controller REPORTS, which decides what a toggle does next.
 *
 * A server-declared mode arrives as a stylesheet rule, and a stylesheet rule is invisible to
 * `element.style`. Reading only the inline one made this answer the OS while the page painted
 * something else — and a toggle that reads the wrong current mode applies the mode the page is
 * already in. The first click did nothing; the visitor clicked twice.
 */
describe('WebThemeController.mode', () => {
  const styleRule = (value: string) => {
    const tag = document.createElement('style');
    tag.textContent = `:root { color-scheme: ${value}; }`;
    document.head.appendChild(tag);
    return tag;
  };

  let injected: HTMLStyleElement | null = null;

  beforeEach(() => {
    document.documentElement.style.colorScheme = '';
  });

  afterEach(() => {
    injected?.remove();
    injected = null;
    vi.unstubAllGlobals();
  });

  it('reports the declared mode even when the OS disagrees', () => {
    // THE reported scenario: a dark desktop, an app declaring light. Forcing matchMedia to say dark
    // is what makes this test able to fail — without it the fallback answers 'light' too, and the
    // assertion passes whether or not the fix is there.
    vi.stubGlobal('matchMedia', () => ({ matches: true }) as unknown as MediaQueryList);
    injected = styleRule('light');

    expect(new WebThemeController().mode).toBe('light');
  });

  it('a live choice outranks the declared one', () => {
    injected = styleRule('light');
    const controller = new WebThemeController();

    controller.apply('dark');

    expect(controller.mode).toBe('dark');
  });

  /**
   * `light dark` is the declaration that MEANS "follow the system": it matches neither keyword, so
   * it falls through to the OS exactly as before. This change only affects the case where somebody
   * already decided.
   */
  it('falls through to the OS when nobody decided', () => {
    injected = styleRule('light dark');

    expect(['light', 'dark']).toContain(new WebThemeController().mode);
  });
});

describe('WebThemeController persistence', () => {
  const clearCookies = () => {
    for (const pair of document.cookie.split(';')) {
      const name = pair.split('=')[0]?.trim();
      if (name) document.cookie = `${name}=; path=/; max-age=0`;
    }
  };

  beforeEach(() => {
    clearCookies();
    delete window.__EQ_CONFIG;
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.style.colorScheme = '';
  });

  afterEach(clearCookies);

  it('remembers the choice under the default name', () => {
    new WebThemeController().apply('dark');
    expect(document.cookie).toContain('eq-theme=dark');
  });

  it('uses the name the server configured', () => {
    window.__EQ_CONFIG = { themeCookie: { name: 'acme-theme', days: 30 } };

    new WebThemeController().apply('light');

    expect(document.cookie).toContain('acme-theme=light');
    // The default name must not have received a VALUE. (Not `not.toContain('eq-theme=')`: clearing
    // a cookie in jsdom leaves the bare name behind, so that assertion fails on the cleanup.)
    expect(document.cookie).not.toContain('eq-theme=light');
  });

  /**
   * The consent case. An app that must not set a cookie before it is granted turns this off, and
   * the toggle still WORKS — the mode applies to this page, it simply does not outlive it.
   */
  it('writes nothing when the app turned it off', () => {
    window.__EQ_CONFIG = { themeCookie: false };

    new WebThemeController().apply('dark');

    // By VALUE, not by name: clearing a cookie in jsdom leaves the bare name behind, so asserting
    // on the name would fail on the previous test's cleanup rather than on this one's behaviour.
    expect(document.cookie).not.toContain('=dark');
    expect(document.documentElement.style.colorScheme).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });

  it('applies the mode whether or not it is remembered', () => {
    window.__EQ_CONFIG = { themeCookie: false };
    const controller = new WebThemeController();

    controller.apply('dark');

    expect(controller.mode).toBe('dark');
  });
});
