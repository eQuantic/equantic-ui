import { afterEach, describe, expect, it, vi } from 'vitest';
import { PAGE_AGREEMENT_DOCS, routerDisagrees } from './page-agreement';

/**
 * The browser's router and the server choose one page for a URL (#1778): a real site rewrote `/` to its
 * catalogue on the server, the router resolved `/` to the home page, and a language change drew the
 * home's empty texts with nothing said.
 */
describe('the router and the server choose the same page', () => {
  afterEach(() => vi.restoreAllMocks());

  it('says, in development, which page each chose for the path, and where the docs explain it', () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});

    expect(routerDisagrees('/', 'CataloguePage', 'HomePage', true)).toBe(true);

    expect(error).toHaveBeenCalledTimes(1);
    const said = String(error.mock.calls[0][0]);
    expect(said).toContain('the server drew CataloguePage, the router chose HomePage for /');
    expect(said).toContain(PAGE_AGREEMENT_DOCS);
  });

  it('says nothing in production, and still answers that they disagree', () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});

    expect(routerDisagrees('/', 'CataloguePage', 'HomePage', false)).toBe(true);
    expect(error).not.toHaveBeenCalled();
  });

  it('agrees when both chose one page, and when the router chose none (a not-found page)', () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});

    expect(routerDisagrees('/about', 'AboutPage', 'AboutPage', true)).toBe(false);
    expect(routerDisagrees('/nowhere', 'NotFoundPage', null, true)).toBe(false);
    expect(routerDisagrees('/about', undefined, 'AboutPage', true)).toBe(false);
    expect(error).not.toHaveBeenCalled();
  });
});
