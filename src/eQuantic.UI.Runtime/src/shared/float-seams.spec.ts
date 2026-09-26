/**
 * WHERE C# SAYS FLOAT, THE RUNTIME HANDS C# A SINGLE.
 *
 * eqc rounds a float where C# code PRODUCES one (SinglePrecision). A value the BROWSER produces, a
 * scroll offset, a drag's travel, a pointer's position, enters C# code through one of the seams
 * below, and a raw double there is a value .NET could not hold: stored in float state, it survives
 * until the next operation rounds it, and a comparison or a print on the way sees the double.
 *
 * `FloatSeamsTests` (eQuantic.UI.Web.Tests) enumerates the vocabulary's float-taking callbacks by
 * reflection and requires each to name a test in this file, so a new callback fails there until it
 * is proven here.
 */
import { beforeEach, describe, expect, it } from 'vitest';
import { lowerVisualNode, type LoweringContext } from './lowering';
import type { DraggableNode, ScrollViewNode } from './nodes';
import { commitScrollViewports } from './scroll-viewports';
import { photonTheme } from './design-system.generated';
import { CanvasPointer } from './canvas-pointer';

/** A double that is not a single, so a seam that passes it through is visible. */
const RAW = 0.1;
const SINGLE = Math.fround(RAW);

function context(): LoweringContext {
  return { textPrimary: photonTheme.textPrimary } as LoweringContext;
}

function handlerOf(lowered: unknown, event: string): (e: Event) => void {
  const handler = (lowered as { events: Record<string, (e: Event) => void> }).events[event];
  expect(handler, `the lowered node listens for ${event}`).toBeDefined();
  return handler;
}

function scrollNode(overrides: Partial<ScrollViewNode>): ScrollViewNode {
  return {
    nodeKind: 'scrollView',
    child: { nodeKind: 'column', children: [] } as never,
    axis: 'vertical',
    ...overrides,
  } as ScrollViewNode;
}

function draggableNode(overrides: Partial<DraggableNode>): DraggableNode {
  return {
    nodeKind: 'draggable',
    child: { nodeKind: 'column', children: [] } as never,
    axis: 'vertical',
    ...overrides,
  } as DraggableNode;
}

describe('the runtime hands C# a single wherever C# says float', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
  });

  it('ScrollView.OnScrolled', () => {
    const seen: number[] = [];
    const lowered = lowerVisualNode(scrollNode({ onScrolled: (offset) => seen.push(offset) }), context());
    const view = document.createElement('div');
    Object.defineProperty(view, 'scrollTop', { value: 240 + RAW });

    handlerOf(lowered, 'scroll')({ target: view } as unknown as Event);

    expect(seen).toEqual([Math.fround(240 + RAW)]);
    expect(seen[0]).not.toBe(240 + RAW);
  });

  it('ScrollView.OnViewportChanged', () => {
    const seen: number[] = [];
    const lowered = lowerVisualNode(scrollNode({ onViewportChanged: (extent) => seen.push(extent) }), context());
    const view = document.createElement('div');
    view.setAttribute('data-eq-scroll', (lowered as { attributes: Record<string, string> }).attributes['data-eq-scroll']);
    // A browser reports an integer here; the seam rounds whatever it is given all the same.
    Object.defineProperty(view, 'clientHeight', { value: 300 + RAW });
    document.body.appendChild(view);

    commitScrollViewports();

    expect(seen).toEqual([Math.fround(300 + RAW)]);
  });

  it('Draggable.OnMoved', () => {
    const seen: number[] = [];
    const lowered = lowerVisualNode(draggableNode({ onMoved: (offset) => seen.push(offset) }), context());

    handlerOf(lowered, 'eq-drag-moved')(new CustomEvent('eq-drag-moved', { detail: RAW }));

    expect(seen).toEqual([SINGLE]);
  });

  it('Draggable.OnReleased', () => {
    const seen: number[] = [];
    const lowered = lowerVisualNode(draggableNode({ onReleased: (offset) => seen.push(offset) }), context());

    handlerOf(lowered, 'eq-drag-released')(new CustomEvent('eq-drag-released', { detail: RAW }));

    expect(seen).toEqual([SINGLE]);
  });

  // The pointer twin is written by hand, so its members answer what the C# record's do, measured
  // in .NET: `new CanvasPointer(0.1f, 0.2f, false, 0)`, centre (0.3f, 0.7f).
  it('CanvasPointer', () => {
    const pointer = new CanvasPointer(0.1, 0.2, false);

    expect(pointer.x).toBe(0.10000000149011612);
    expect(pointer.y).toBe(Math.fround(0.2));
    expect(pointer.angleFrom(Math.fround(0.3), Math.fround(0.7))).toBe(-1.9513027667999268);
    expect(pointer.distanceFrom(Math.fround(0.3), Math.fround(0.7))).toBe(0.5385165214538574);
  });
});
