import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { applyPageState, fetchPageState, NAVIGATION_HEADER } from './page-state';

/**
 * The client half of a navigation's page state, the half ClientNavigationStateTests cannot reach: what
 * the router does with the server's answer.
 */
describe('a navigation reads the page state the server marks as its answer', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function answer(status: number, marked: boolean, body: unknown): void {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        new Response(JSON.stringify(body), {
          status,
          headers: marked ? { [NAVIGATION_HEADER]: '1', 'Content-Type': 'application/json' } : {},
        }),
      ),
    );
  }

  it('reads a marked 404, whose page says what it is', async () => {
    answer(404, true, { title: 'Nothing Here', head: '', state: { 'Gone#0': { found: false } } });

    const payload = await fetchPageState('/gone');

    // Dropped for its status, the payload left the previous page's title and head standing.
    expect(payload?.title).toBe('Nothing Here');
    expect(payload?.state).toEqual({ 'Gone#0': { found: false } });
  });

  it('takes an unmarked failure for no payload', async () => {
    answer(502, false, { title: 'Bad Gateway' });

    expect(await fetchPageState('/behind-a-proxy')).toBeNull();
  });

  it('reads a marked 401 or 403 as a REFUSAL, never as a page to render (#673)', async () => {
    answer(401, true, {});
    expect(await fetchPageState('/backoffice')).toEqual({ refused: 401 });

    answer(403, true, {});
    expect(await fetchPageState('/backoffice')).toEqual({ refused: 403 });
  });

  it('asks with the header the server answers by', async () => {
    answer(200, true, {});

    await fetchPageState('/asked');

    const init = (fetch as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0][1];
    expect((init.headers as Record<string, string>)[NAVIGATION_HEADER]).toBe('1');
  });
});

describe("a navigation replaces the head's metadata as a set", () => {
  beforeEach(() => {
    document.head.innerHTML =
      '<meta name="description" content="The old page" data-eq-meta>' +
      '<link rel="canonical" href="https://example.test/old" data-eq-meta>' +
      '<link rel="alternate" hreflang="pt-BR" href="https://example.test/pt-BR/old" data-eq-meta>' +
      '<meta name="viewport" content="width=device-width">';
    document.title = 'The Old Page';
  });

  it("removes every marked tag the previous page left, and writes the next page's", () => {
    applyPageState({
      title: 'The New Page',
      head: '<meta name="description" content="The new page" data-eq-meta>',
    });

    expect(document.title).toBe('The New Page');
    const marked = Array.from(document.head.querySelectorAll('[data-eq-meta]'));
    expect(marked.map((tag) => tag.outerHTML)).toEqual([
      '<meta name="description" content="The new page" data-eq-meta="">',
    ]);
    // What the metadata did not write is not the metadata's to remove.
    expect(document.head.querySelector('meta[name="viewport"]')).not.toBeNull();
  });

  it('removes them too when the next page writes none', () => {
    applyPageState({ title: 'Bare', head: '' });

    expect(document.head.querySelectorAll('[data-eq-meta]')).toHaveLength(0);
  });

  it("replaces the hydration state, with nothing when the page sends none", () => {
    const w = window as unknown as { __INITIAL_STATE__?: unknown };
    applyPageState({ state: { 'A#0': { n: 1 } } });
    expect(w.__INITIAL_STATE__).toEqual({ 'A#0': { n: 1 } });

    applyPageState({ head: '' });
    expect(w.__INITIAL_STATE__).toBeUndefined();
  });
});
