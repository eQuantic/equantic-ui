import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { WebThemeController } from './theme-controller';

/**
 * Remembering the theme, and NOT remembering it when the app said not to.
 *
 * The cookie's shape arrives from the server, because the server is what reads it while this is
 * what writes it. Two places to configure would drift, and a drifted name fails silently: the
 * server reads a cookie nobody writes, persistence stops, and every part of it still looks right.
 */
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
