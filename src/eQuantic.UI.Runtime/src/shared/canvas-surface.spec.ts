/**
 * A FILLING canvas is drawn twice on the web: once at lowering, where the box is not yet decided
 * and honestly reports zero, and again once the element has been measured. Photon has no such
 * problem — it lays out and then paints, every frame — so this is the module that keeps a
 * visualization written once looking the same on both.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { commitCanvasSurfaces, declareCanvas } from './canvas-surface';
import type { ICanvasPainter } from './nodes';
import { modifiersOf } from './css-values';

const INK = { light: { r: 1, g: 2, b: 3, a: 255 }, dark: { r: 1, g: 2, b: 3, a: 255 } };

describe('canvas surfaces measure before they draw', () => {
  let svg: SVGElement;
  let observed: Element[];

  beforeEach(() => {
    document.body.innerHTML = '';
    svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('data-eq-canvas-fill', 'root/0');
    document.body.appendChild(svg);

    observed = [];
    vi.stubGlobal('ResizeObserver', class {
      observe(element: Element): void {
        observed.push(element);
      }
      disconnect(): void {}
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  /** The box the browser would report; jsdom measures nothing on its own. */
  const size = (width: number, height: number): void => {
    svg.getBoundingClientRect = () => ({ width, height, top: 0, left: 0, right: width, bottom: height, x: 0, y: 0, toJSON: () => ({}) });
  };

  it('draws at the MEASURED size, not at zero', () => {
    size(200, 100);
    declareCanvas('root/0', {
      draw: (p: ICanvasPainter) => p.fillCircle(p.width / 2, p.height / 2, 10, INK),
    });

    commitCanvasSurfaces();

    const circle = svg.querySelector('circle');
    expect(circle?.getAttribute('cx')).toBe('100');
    expect(circle?.getAttribute('cy')).toBe('50');
    expect(svg.getAttribute('viewBox')).toBe('0 0 200 100');
  });

  it('does not draw into a box with no size', () => {
    size(0, 0);
    declareCanvas('root/0', { draw: (p) => p.fillCircle(1, 1, 1, INK) });

    commitCanvasSurfaces();

    expect(svg.querySelector('circle')).toBeNull();
  });

  it('observes the element so a resize redraws it', () => {
    size(120, 60);
    declareCanvas('root/0', { draw: (p) => p.fillCircle(p.width, 0, 1, INK) });

    commitCanvasSurfaces();

    expect(observed).toEqual([svg]);
  });

  it('replaces what it drew before rather than appending', () => {
    size(100, 100);
    declareCanvas('root/0', { draw: (p) => p.fillCircle(1, 1, 1, INK) });
    commitCanvasSurfaces();

    declareCanvas('root/0', { draw: (p) => p.fillRect(0, 0, 2, 2, INK) });
    commitCanvasSurfaces();

    expect(svg.querySelector('circle')).toBeNull();
    expect(svg.querySelectorAll('rect')).toHaveLength(1);
  });
});

describe('canvas pointer modifiers', () => {
  it('maps each DOM flag to the bit C# names, through the ONE shared expression', () => {
    expect(modifiersOf({ shiftKey: true })).toBe(1);
    expect(modifiersOf({ altKey: true })).toBe(2);
    expect(modifiersOf({ metaKey: true })).toBe(4);
    // Ctrl IS the command key off Apple, which is what `command` means — and the key path in the
    // lowering has always resolved it this way, so the canvas must not invent a second rule.
    expect(modifiersOf({ ctrlKey: true })).toBe(4);
    expect(modifiersOf({ shiftKey: true, altKey: true, metaKey: true })).toBe(1 | 2 | 4);
    expect(modifiersOf({})).toBe(0);
  });
});
