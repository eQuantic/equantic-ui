/**
 * The canvas, styled ONE way. These class names are CROSS-PINNED with the C# suite
 * (`CanvasUnderALayerTests.TheAtomizedCanvas_CarriesTheSameClassesTheTwinProduces`), which asserts
 * the same four for the same canvas: an element that carries shared classes when the server drew it
 * and an inline style when the browser drew it is the same element described twice.
 *
 * The pointer declaration is the load-bearing one. `pointer-events` inherits, and a layout node
 * that paints nothing disclaims it, so an interactive canvas inside almost any layout inherited the
 * disclaimer and went mute — the handlers were never called while the arithmetic behind them stayed
 * right, which is how a green suite kept saying nothing was wrong.
 */
import { describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import type { CanvasNodeValue } from './nodes';

/** The WIRE shape, typed — `SizeValueValue` carries a value beside its kind, and a helper that
 * cast its way past that could not fail when the shape moved under it. */
const fill = { kind: 'fill', value: 0 } as const;

const canvas = (interactive: boolean): CanvasNodeValue => ({
  nodeKind: 'canvas',
  draw: () => {},
  width: fill,
  height: fill,
  label: interactive ? 'Sunburst' : null,
  onPointerMove: interactive ? () => {} : null,
});

/** The lowering only reads the theme for tokens a canvas never carries. */
const context = {} as Parameters<typeof lowerVisualNode>[1];

describe('canvas parity (C# CanvasUnderALayerTests cross-pin)', () => {
  it('an interactive canvas carries the four classes the C# atomizer produces', () => {
    const lowered = lowerVisualNode(canvas(true), context);
    expect(lowered.attributes['class']).toBe('eq-16x7aca eq-1jjc6f6 eq-akjizx eq-g7fowl');
    expect(lowered.attributes['style']).toBeUndefined();
    expect(lowered.attributes['data-eq-canvas']).toBe('1');
  });

  it('a decorative canvas differs in exactly one class, the pointer one', () => {
    const listening = lowerVisualNode(canvas(true), context).attributes['class'] ?? '';
    const mute = lowerVisualNode(canvas(false), context).attributes['class'] ?? '';

    const only = (a: string, b: string) => a.split(' ').filter((c) => !b.split(' ').includes(c));
    expect(only(listening, mute)).toHaveLength(1);
    expect(only(mute, listening)).toHaveLength(1);
    expect(lowerVisualNode(canvas(false), context).attributes['data-eq-canvas']).toBeUndefined();
  });
});
