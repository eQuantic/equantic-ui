import { effectiveStyle } from './style-atomizer';
import { describe, expect, it } from 'vitest';
import {
  Box,
  BoxStyle,
  Drawing,
  Positioned,
  ScrollView,
  Stack,
  VectorDrawing,
  VectorPaint,
  VectorShape,
  VisualNode,
} from './vocabulary';
import { SizeValue } from './value-types';

describe('Stack (spec A3) client lowering', () => {
  it('lowers to a single-cell grid with the cross-pinned positioned anchor', () => {
    const stack = new Stack('center');
    stack.add(new Box(new BoxStyle({ width: 40, height: 40 })));
    stack.add(new Positioned(new Box(new BoxStyle({ width: 16, height: 16 })), -4, -4));

    const node = stack.render();
    expect(effectiveStyle(node)).toContain('display: grid');
    expect(effectiveStyle(node)).toContain('position: relative');
    // `pointer-events: none` rides along because this layer is a BARE box: it paints nothing and
    // holds nothing, so it intercepts nothing (see paintsNothing). A cell that stretches to the
    // whole stack and takes the layer's z-index would otherwise cover everything under it with an
    // invisible target — which is what a CLOSED Drawer did to the compact shell's own menu button.
    expect(effectiveStyle(node.children[0])).toContain(
      'align-items: center; display: flex; grid-area: 1 / 1; height: 100%; justify-content: center; ' +
        'pointer-events: none; width: 100%',
    );
    // Spec A3: paint order IS child order — each cell carries its DEPTH, so a child with a filter
    // (which creates a stacking context) can't jump above the siblings drawn after it.
    expect(effectiveStyle(node.children[0])).toContain('z-index: 1');
    // The SAME literal StackRealizerTests pins on the C# side (hydration parity).
    expect(effectiveStyle(node.children[1])).toBe(
      'position: absolute; right: -4px; top: -4px; z-index: 2',
    );
  });

  it('places by fractions of the stack and shifts by fractions of the child', () => {
    const stack = new Stack();
    stack.add(
      new Positioned(new Box(new BoxStyle({ width: 100, height: 30 })), -16, null, null, null, {
        startFraction: 0.5,
        topFraction: 0.25,
        shiftX: -0.5,
        shiftY: -1,
      }),
    );

    const anchor = effectiveStyle(stack.render().children[0]);
    // The SAME literals PositionedSpanTests pins on the C# side (hydration parity).
    expect(anchor).toContain('left: 50%');
    expect(anchor).toContain('top: calc(25% - 16px)');
    expect(anchor).toContain('transform: translate(-50%, -100%)');
  });

  it('pins the opposite edge for a drawing at its parent width (the C# Fills twin)', () => {
    const art = new VectorDrawing(0, 0, 10, 10, [new VectorShape('M0 0L10 0L10 10Z', VectorPaint.solid({ r: 0, g: 0, b: 0, a: 255 } as never))]);
    const stack = new Stack();
    stack.add(new Positioned(new Drawing(art, SizeValue.fill), null, null, null, null, { startFraction: 0.5 }));

    const anchor = effectiveStyle(stack.render().children[0]);
    expect(anchor).toContain('left: 50%');
    expect(anchor).toContain('right: 0');
  });

  it('shifts by the child on a wrapper when both edges stretch the anchor (C# twin)', () => {
    const stack = new Stack();
    stack.add(new Positioned(new Box(new BoxStyle({ width: 100, height: 30 })), null, 10, null, 10, { shiftX: -0.5 }));

    const anchor = stack.render().children[0];
    expect(effectiveStyle(anchor)).not.toContain('transform');
    const wrapper = effectiveStyle(anchor.children[0]);
    expect(wrapper).toContain('width: fit-content');
    expect(wrapper).toContain('transform: translate(-50%, 0)');
  });

  it('lowers an end fraction alone to a percentage, and no shift to no transform', () => {
    const stack = new Stack();
    stack.add(new Positioned(new Box(new BoxStyle({ width: 40, height: 20 })), null, null, null, null, { endFraction: 0.1 }));

    const anchor = effectiveStyle(stack.render().children[0]);
    expect(anchor).toContain('right: 10%');
    expect(anchor).not.toContain('translate(');
  });

  it("carries a Positioned's key, bookmark and origin on its anchor (#940, the C# Decorate twin)", () => {
    const positioned = new Positioned(new Box(new BoxStyle({ width: 16, height: 16 })), 0, -28);
    positioned.bookmark = 'request';
    positioned.key = 'request-card';
    (positioned as unknown as { origin: string }).origin = 'Landing.cs:42';
    const stack = new Stack();
    stack.add(positioned);

    const anchor = stack.render().children[0];
    expect(anchor.attributes['id']).toBe('request');
    expect(anchor.key).toBe('request-card');
    expect(anchor.attributes['data-eq-origin']).toBe('Landing.cs:42');
    // The SAME declarations StackRealizerTests pins on the C# side (hydration parity).
    expect(effectiveStyle(anchor)).toContain('position: absolute; right: -28px; scroll-margin-top');
    expect(effectiveStyle(anchor)).toContain('top: 0; z-index: 1');
  });

  // A grid item's automatic minimum size is its min-content size, so a layer holding a horizontal
  // scroller would size the track to the widest line in the file and the stack would swell past
  // the box that contains it. The SAME axes StackRealizerTests pins on the C# side.
  it('caps a layer only on an axis it fills, or a scroller hugs (#698)', () => {
    const cell = (layer: VisualNode): string => {
      const stack = new Stack();
      stack.add(layer);
      return effectiveStyle(stack.render().children[0]);
    };
    const wide = (): Box => new Box(new BoxStyle({ width: 4000 }));

    const fillsAcross = cell(new ScrollView(wide(), 'horizontal', { width: SizeValue.fill }));
    expect(fillsAcross).toContain('min-width: 0');
    expect(fillsAcross).toContain('min-height: 0');

    const hugging = cell(new ScrollView(wide(), 'horizontal'));
    expect(hugging).toContain('min-width: 0');
    expect(hugging).toContain('min-height: 0');

    const fillingBox = cell(new Box(new BoxStyle({ width: SizeValue.fill, height: 20 })));
    expect(fillingBox).toContain('min-width: 0');
    expect(fillingBox).not.toContain('min-height');

    const fixedBox = cell(new Box(new BoxStyle({ width: 44, height: 24 })));
    expect(fixedBox).not.toContain('min-width');
    expect(fixedBox).not.toContain('min-height');

    const fixedScroller = cell(new ScrollView(wide(), 'horizontal', { width: 300, height: 20 }));
    expect(fixedScroller).not.toContain('min-width');
    expect(fixedScroller).not.toContain('min-height');
  });
});
