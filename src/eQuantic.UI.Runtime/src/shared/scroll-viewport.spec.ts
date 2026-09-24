/**
 * The ScrollView's web out-channels — the half a WINDOWED LIST lives on. The native realizer has
 * reported (offset, viewport) all along; these specs conduct the DOM side: the scroll event feeds
 * `onScrolled`, the after-pass sweep measures the viewport and adopts the initial offset ONCE
 * (the browser owns the position after that — re-applying would fight the user).
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { lowerVisualNode, type LoweringContext } from './lowering';
import type { ScrollViewNode } from './nodes';
import { commitScrollViewports, scheduleScrollViewportCommit } from './scroll-viewports';
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

  /**
   * The pass ends while its tree is still a value, and the render manager writes it after. The
   * commit ran at the end of the pass and measured the tree BEFORE, so a scroll view seen for the
   * first time was measured by the pass after it, and a pass that resized a viewport reported the
   * old size.
   */
  it('measures the viewport the pass WROTE, not the one before it', async () => {
    const seen: number[] = [];
    const lowered = lowerVisualNode(
      scrollNode({ onViewportChanged: (h) => seen.push(h) }),
      context(),
    );
    const path = (lowered as { attributes: Record<string, string> }).attributes['data-eq-scroll'];
    scheduleScrollViewportCommit();

    // The element arrives the way a rendered one does: after the pass returned.
    const el = document.createElement('div');
    el.setAttribute('data-eq-scroll', path);
    Object.defineProperty(el, 'clientHeight', { value: 400, configurable: true });
    document.body.append(el);
    expect(seen).toEqual([]);

    await Promise.resolve();

    expect(seen).toEqual([400]);
  });

  describe('a viewport that changes with no pass', () => {
    /** The observers the commit attached, each delivering by hand. */
    const observers: { target?: Element; deliver: () => void }[] = [];

    beforeEach(() => {
      observers.length = 0;
      vi.stubGlobal(
        'ResizeObserver',
        class {
          private readonly entry: { target?: Element; deliver: () => void };
          constructor(callback: ResizeObserverCallback) {
            this.entry = { deliver: () => callback([], this as unknown as ResizeObserver) };
            observers.push(this.entry);
          }
          observe(target: Element) {
            this.entry.target = target;
          }
          disconnect() {
            this.entry.target = undefined;
          }
        },
      );
    });

    afterEach(() => {
      vi.unstubAllGlobals();
    });

    /**
     * A window resized, or a splitter dragged: nothing re-renders, so nothing measured, and a code
     * editor filling an IDE's pane went on building the rows it had built for the old height.
     */
    it('reports its new size, watched from the pass that first measured it', () => {
      const seen: number[] = [];
      const lowered = lowerVisualNode(
        scrollNode({ onViewportChanged: (h) => seen.push(h) }),
        context(),
      );
      const path = (lowered as { attributes: Record<string, string> }).attributes['data-eq-scroll'];
      const el = document.createElement('div');
      el.setAttribute('data-eq-scroll', path);
      Object.defineProperty(el, 'clientHeight', { value: 400, configurable: true });
      document.body.append(el);
      commitScrollViewports();
      expect(seen).toEqual([400]);
      expect(observers).toHaveLength(1);
      expect(observers[0].target).toBe(el);

      Object.defineProperty(el, 'clientHeight', { value: 640, configurable: true });
      observers[0].deliver();
      expect(seen).toEqual([400, 640]);

      // Once per element, however many passes declare it again.
      lowerVisualNode(scrollNode({ onViewportChanged: (h) => seen.push(h) }), context());
      commitScrollViewports();
      expect(observers).toHaveLength(1);
    });

    /**
     * A scroll view can keep its offset and stop asking for its viewport, and keep its marker with
     * the offset: its observer was left watching, told of every resize for nobody.
     */
    it('stops watching a scroll view that keeps its offset and stops asking', () => {
      const lowered = lowerVisualNode(scrollNode({ offset: 10, onViewportChanged: () => {} }), context());
      const path = (lowered as { attributes: Record<string, string> }).attributes['data-eq-scroll'];
      const el = document.createElement('div');
      el.setAttribute('data-eq-scroll', path);
      Object.defineProperty(el, 'clientHeight', { value: 400, configurable: true });
      document.body.append(el);
      commitScrollViewports();
      expect(observers[0].target).toBe(el);

      lowerVisualNode(scrollNode({ offset: 10 }), context());
      commitScrollViewports();

      expect(observers[0].target).toBeUndefined();
    });

    /** With no queueMicrotask the commit still waits for the write, rather than measuring the tree
     * before it. */
    it('waits for the write where there is no queueMicrotask', async () => {
      vi.stubGlobal('queueMicrotask', undefined);
      const seen: number[] = [];
      const lowered = lowerVisualNode(scrollNode({ onViewportChanged: (h) => seen.push(h) }), context());
      const path = (lowered as { attributes: Record<string, string> }).attributes['data-eq-scroll'];
      scheduleScrollViewportCommit();

      const el = document.createElement('div');
      el.setAttribute('data-eq-scroll', path);
      Object.defineProperty(el, 'clientHeight', { value: 400, configurable: true });
      document.body.append(el);
      expect(seen).toEqual([]);

      await Promise.resolve();

      expect(seen).toEqual([400]);
    });

    it('and says nothing once the scroll view stops asking', () => {
      const seen: number[] = [];
      const lowered = lowerVisualNode(
        scrollNode({ onViewportChanged: (h) => seen.push(h) }),
        context(),
      );
      const path = (lowered as { attributes: Record<string, string> }).attributes['data-eq-scroll'];
      const el = document.createElement('div');
      el.setAttribute('data-eq-scroll', path);
      Object.defineProperty(el, 'clientHeight', { value: 400, configurable: true });
      document.body.append(el);
      commitScrollViewports();

      el.removeAttribute('data-eq-scroll');
      Object.defineProperty(el, 'clientHeight', { value: 640, configurable: true });
      observers[0].deliver();

      expect(seen).toEqual([400]);
      expect(observers[0].target).toBeUndefined();
    });
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
