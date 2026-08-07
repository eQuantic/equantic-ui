/**
 * The ScrollView's web out-channels — the half a WINDOWED LIST lives on. The native realizer has
 * reported (offset, viewport) all along; these specs conduct the DOM side: the scroll event feeds
 * `onScrolled`, the after-pass sweep measures the viewport and adopts the initial offset ONCE
 * (the browser owns the position after that — re-applying would fight the user).
 */

import { beforeEach, describe, expect, it } from 'vitest';
import { lowerVisualNode, type LoweringContext } from './lowering';
import type { ScrollViewNode } from './nodes';
import { commitScrollViewports } from './scroll-viewports';
import { photonTheme } from './design-system.generated';

function context(): LoweringContext {
  return { textPrimary: photonTheme.textPrimary } as LoweringContext;
}

function scrollNode(overrides: Partial<ScrollViewNode>): ScrollViewNode {
  return {
    nodeKind: 'scrollView',
    child: { nodeKind: 'column', children: [] } as never,
    axis: 'vertical',
    ...overrides,
  } as ScrollViewNode;
}

describe('ScrollView web out-channels', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
  });

  it('feeds onScrolled from the scroll event', () => {
    const seen: number[] = [];
    const lowered = lowerVisualNode(scrollNode({ onScrolled: (o) => seen.push(o) }), context());

    const handler = (lowered as { events: Record<string, (e: Event) => void> }).events['scroll'];
    expect(handler).toBeDefined();

    const el = document.createElement('div');
    Object.defineProperty(el, 'scrollTop', { value: 240 });
    handler({ target: el } as unknown as Event);

    expect(seen).toEqual([240]);
  });

  it('reports the measured viewport after the pass, only when it changes', () => {
    const seen: number[] = [];
    const lowered = lowerVisualNode(
      scrollNode({ onViewportChanged: (h) => seen.push(h) }),
      context(),
    );
    const path = (lowered as { attributes: Record<string, string> }).attributes['data-eq-scroll'];
    expect(path).toBeDefined();

    const el = document.createElement('div');
    el.setAttribute('data-eq-scroll', path);
    Object.defineProperty(el, 'clientHeight', { value: 400, configurable: true });
    document.body.append(el);

    commitScrollViewports();
    expect(seen).toEqual([400]);

    // The next pass re-declares; an unchanged viewport stays silent.
    lowerVisualNode(scrollNode({ onViewportChanged: (h) => seen.push(h) }), context());
    commitScrollViewports();
    expect(seen).toEqual([400]);

    // A real change (the window resized) reports again.
    lowerVisualNode(scrollNode({ onViewportChanged: (h) => seen.push(h) }), context());
    Object.defineProperty(el, 'clientHeight', { value: 300, configurable: true });
    commitScrollViewports();
    expect(seen).toEqual([400, 300]);
  });

  it('adopts the initial offset ONCE — the browser owns the position after', () => {
    const lowered = lowerVisualNode(scrollNode({ offset: 800 }), context());
    const path = (lowered as { attributes: Record<string, string> }).attributes['data-eq-scroll'];

    const el = document.createElement('div');
    el.setAttribute('data-eq-scroll', path);
    document.body.append(el);

    commitScrollViewports();
    expect(el.scrollTop).toBe(800);

    // The user scrolled; the next pass must NOT drag them back.
    el.scrollTop = 120;
    lowerVisualNode(scrollNode({ offset: 800 }), context());
    commitScrollViewports();
    expect(el.scrollTop).toBe(120);
  });

  it('a plain scroll view declares nothing and carries no marker', () => {
    const lowered = lowerVisualNode(scrollNode({}), context());
    const attrs = (lowered as { attributes: Record<string, string> }).attributes;
    expect(attrs['data-eq-scroll']).toBeUndefined();
  });
});
