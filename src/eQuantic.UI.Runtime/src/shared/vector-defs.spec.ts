import { readFileSync } from 'node:fs';
import { beforeEach, describe, expect, it } from 'vitest';
import {
  Drawing,
  VectorDrawing,
  VectorPaint,
  VectorShape,
  VectorGradient,
  VectorStop,
} from './vocabulary';
import type { HtmlNode } from '../core/types';

/**
 * Where a gradient's definition lives.
 *
 * A `<defs>` inside each drawing deduplicates within that drawing and nowhere else. An AdaptiveNode
 * puts every arm in the document, so the same artwork appeared N times with N identical defs — and
 * `url(#id)` binds to the FIRST in document order, which is the arm the media query hides. A paint
 * server inside a `display:none` subtree is not rendered, so the shape came out with no fill on the
 * layout it was written for.
 *
 * The id is untouched: it is the hash of the run, which is what lets the server and this twin
 * arrive at the same string. What moved is the container — one per document, adopted rather than
 * recreated, because a second container is two defs with one id and the bug back through the other
 * door.
 */
describe('gradient definitions belong to the document', () => {
  const run = new VectorGradient(0, 0, 0, 1, 0, false, [
    new VectorStop(0, { r: 79, g: 172, b: 254, a: 255 }),
    new VectorStop(1, { r: 0, g: 242, b: 254, a: 255 }),
  ]);

  const sky = () =>
    new VectorDrawing(0, 0, 100, 100, [
      new VectorShape('M0 0 H100 V100 H0 Z', VectorPaint.gradients('linearGradient', run)),
    ]);

  beforeEach(() => {
    document.body.innerHTML = '';
    document.head.innerHTML = '';
  });

  it('declares a run once, however many drawings paint with it', () => {
    new Drawing(sky(), 100, 100).render();
    new Drawing(sky(), 100, 100).render();

    const containers = document.querySelectorAll('#eq-vectors');
    expect(containers.length).toBe(1);
    expect(containers[0].querySelectorAll('linearGradient').length).toBe(1);
  });

  it('leaves the drawing itself without defs, so no arm can own the document"s', () => {
    const svg = new Drawing(sky(), 100, 100).render() as HtmlNode;

    const walk = (node: HtmlNode): HtmlNode[] => [
      node,
      ...node.children.flatMap((c) => walk(c as HtmlNode)),
    ];
    expect(walk(svg).filter((n) => n.tag === 'defs')).toHaveLength(0);
    // …and the shape still names the run, which is the half that must not change.
    const path = walk(svg).find((n) => n.tag === 'path')!;
    expect(path.attributes['fill']).toMatch(/^url\(#eq-g-/);
  });

  /**
   * The server's container is the one that must be used — a second one would carry the same ids.
   * The id here is a REAL one, read from the cross-pinned fixture that both producers are held to,
   * so this is the client meeting what the server actually wrote rather than a shape invented here.
   */
  it('adopts the container the server left instead of creating another', () => {
    const fixture = readFileSync('src/shared/__fixtures__/vector-gradient.txt', 'utf8');
    const id = fixture.split('\n')[0].split('|').pop()!;
    expect(id).toMatch(/^eq-g-/);
    document.body.insertAdjacentHTML(
      'beforeend',
      `<svg id="eq-vectors" width="0" height="0" aria-hidden="true" style="position:absolute"><defs>` +
        `<linearGradient id="${id}"></linearGradient></defs></svg>`,
    );

    new Drawing(sky(), 100, 100).render();

    expect(document.querySelectorAll('#eq-vectors').length).toBe(1);
    expect(document.getElementById(id)).not.toBeNull();
  });

  /** Rendered, not hidden: `display:none` is what made the definition unusable in the first place. */
  it('creates a container that is out of flow rather than hidden', () => {
    new Drawing(sky(), 100, 100).render();

    const container = document.getElementById('eq-vectors')!;
    expect(container.getAttribute('style')).toContain('position:absolute');
    expect(container.getAttribute('style')).not.toContain('display:none');
    expect(container.getAttribute('aria-hidden')).toBe('true');
  });
});
