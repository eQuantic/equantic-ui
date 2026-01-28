import { describe, it, expect, beforeEach } from 'vitest';
import { Reconciler } from './reconciler';
import { HtmlNode } from '../core/types';

/**
 * Helper to create HtmlNode (virtual DOM node)
 */
function h(tag: string, key?: string | number | null, children: HtmlNode[] = []): HtmlNode {
  return {
    tag,
    key: key?.toString(),
    children,
    attributes: {},
    events: {},
  };
}

describe('Reconciler Keyed Diffing', () => {
  let reconciler: Reconciler;
  let parent: HTMLElement;

  beforeEach(() => {
    reconciler = new Reconciler();
    parent = document.createElement('div');
  });

  it('should mount children', () => {
    const oldV = h('div', 'root', []);
    reconciler.reconcile(parent, null, oldV);
    const rootEl = parent.childNodes[0] as HTMLElement;

    const newV = h('div', 'root', [h('span', 'A'), h('span', 'B')]);
    reconciler.reconcile(parent, oldV, newV, 0);

    expect(rootEl.childNodes.length).toBe(2);
    expect((rootEl.childNodes[0] as HTMLElement).tagName).toBe('SPAN');
    expect((rootEl.childNodes[1] as HTMLElement).tagName).toBe('SPAN');
  });

  it('should reorder nodes (swap)', () => {
    // Initial: A, B
    const oldV = h('div', 'root', [h('span', 'A'), h('span', 'B')]);
    reconciler.reconcile(parent, null, oldV);
    const rootEl = parent.childNodes[0] as HTMLElement;
    const [nodeA, nodeB] = Array.from(rootEl.childNodes);

    // Update: B, A
    const newV = h('div', 'root', [h('span', 'B'), h('span', 'A')]);
    reconciler.reconcile(parent, oldV, newV, 0);

    // Verify order - same instances should be moved
    expect(rootEl.childNodes.length).toBe(2);
    expect(rootEl.childNodes[0]).toBe(nodeB);
    expect(rootEl.childNodes[1]).toBe(nodeA);
  });

  it('should insert in middle', () => {
    // Initial: A, C
    const oldV = h('div', 'root', [h('span', 'A'), h('span', 'C')]);
    reconciler.reconcile(parent, null, oldV);
    const rootEl = parent.childNodes[0] as HTMLElement;
    const [nodeA, nodeC] = Array.from(rootEl.childNodes);

    // Update: A, B, C
    const newV = h('div', 'root', [h('span', 'A'), h('span', 'B'), h('span', 'C')]);
    reconciler.reconcile(parent, oldV, newV, 0);

    expect(rootEl.childNodes.length).toBe(3);
    expect(rootEl.childNodes[0]).toBe(nodeA);
    expect((rootEl.childNodes[1] as HTMLElement).tagName).toBe('SPAN'); // New B
    expect(rootEl.childNodes[2]).toBe(nodeC);
  });

  it('should remove from middle', () => {
    // Initial: A, B, C
    const oldV = h('div', 'root', [h('span', 'A'), h('span', 'B'), h('span', 'C')]);
    reconciler.reconcile(parent, null, oldV);
    const rootEl = parent.childNodes[0] as HTMLElement;
    const [nodeA, nodeB, nodeC] = Array.from(rootEl.childNodes);

    // Update: A, C
    const newV = h('div', 'root', [h('span', 'A'), h('span', 'C')]);
    reconciler.reconcile(parent, oldV, newV, 0);

    expect(rootEl.childNodes.length).toBe(2);
    expect(rootEl.childNodes[0]).toBe(nodeA);
    expect(rootEl.childNodes[1]).toBe(nodeC);
    // Node B should be detached
    expect(nodeB.parentNode).toBeNull();
  });

  it('should handle complex LIS reorder', () => {
    // Initial: A, B, C, D, E
    const oldV = h('div', 'root', [
      h('span', 'A'),
      h('span', 'B'),
      h('span', 'C'),
      h('span', 'D'),
      h('span', 'E'),
    ]);
    reconciler.reconcile(parent, null, oldV);
    const rootEl = parent.childNodes[0] as HTMLElement;
    const [a, b, c, d, e] = Array.from(rootEl.childNodes);

    // Update: A, C, E, B, D (Mix of moves)
    const newV = h('div', 'root', [
      h('span', 'A'),
      h('span', 'C'),
      h('span', 'E'),
      h('span', 'B'),
      h('span', 'D'),
    ]);
    reconciler.reconcile(parent, oldV, newV, 0);

    // Verify positions
    const newNodes = Array.from(rootEl.childNodes);
    expect(newNodes.length).toBe(5);
    expect(newNodes[0]).toBe(a);
    expect(newNodes[1]).toBe(c);
    expect(newNodes[2]).toBe(e);
    expect(newNodes[3]).toBe(b);
    expect(newNodes[4]).toBe(d);
  });

  it('should replace node when key changes', () => {
    // Initial: A, B, C
    const oldV = h('div', 'root', [h('span', 'A'), h('span', 'B'), h('span', 'C')]);
    reconciler.reconcile(parent, null, oldV);
    const rootEl = parent.childNodes[0] as HTMLElement;
    const originalThird = rootEl.childNodes[2];

    // Update: A, B, D (C replaced by D)
    const newV = h('div', 'root', [h('span', 'A'), h('span', 'B'), h('span', 'D')]);
    reconciler.reconcile(parent, oldV, newV, 0);

    expect(rootEl.childNodes.length).toBe(3);
    // Third node should be a different instance (replaced)
    expect(rootEl.childNodes[2]).not.toBe(originalThird);
  });
});
