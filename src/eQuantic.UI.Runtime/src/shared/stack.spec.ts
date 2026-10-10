import { effectiveStyle } from './style-atomizer';
import { describe, expect, it } from 'vitest';
import { Box, BoxStyle, Drawing, Positioned, Stack, VectorDrawing, VectorPaint, VectorShape } from './vocabulary';
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
        'min-height: 0; min-width: 0; pointer-events: none; width: 100%',
    );
    // …and min-0 is load-bearing, not decoration: a grid item's automatic minimum size is its
    // min-content size, so a layer holding a horizontal scroller would size the track to the
    // widest line in the file and the stack would swell past the box that contains it. The SAME
    // pair StackRealizerTests pins on the C# side.
    expect(effectiveStyle(node.children[0])).toContain('min-width: 0');
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

  it('lowers an end fraction alone to a percentage, and no shift to no transform', () => {
    const stack = new Stack();
    stack.add(new Positioned(new Box(new BoxStyle({ width: 40, height: 20 })), null, null, null, null, { endFraction: 0.1 }));

    const anchor = effectiveStyle(stack.render().children[0]);
    expect(anchor).toContain('right: 10%');
    expect(anchor).not.toContain('translate(');
  });
});
