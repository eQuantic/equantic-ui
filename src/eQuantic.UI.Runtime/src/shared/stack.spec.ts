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
    expect(effectiveStyle(node.children[0])).toContain(
      'align-items: center; display: flex; grid-area: 1 / 1; height: 100%; justify-content: center; width: 100%',
    );
    // Spec A3: paint order IS child order — each cell carries its DEPTH, so a child with a filter
    // (which creates a stacking context) can't jump above the siblings drawn after it.
    expect(effectiveStyle(node.children[0])).toContain('z-index: 1');
    // The SAME literal StackRealizerTests pins on the C# side (hydration parity).
    expect(effectiveStyle(node.children[1])).toBe(
      'position: absolute; right: -4px; top: -4px; z-index: 2',
    );
  });
});
