import { effectiveStyle } from './style-atomizer';
import { describe, expect, it } from 'vitest';
import { Box, BoxStyle, Positioned, Stack } from './vocabulary';

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
});
