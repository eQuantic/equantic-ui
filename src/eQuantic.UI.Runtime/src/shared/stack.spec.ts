import { describe, expect, it } from 'vitest';
import { Box, BoxStyle, Positioned, Stack } from './vocabulary';

describe('Stack (spec A3) client lowering', () => {
  it('lowers to a single-cell grid with the cross-pinned positioned anchor', () => {
    const stack = new Stack('center');
    stack.add(new Box(new BoxStyle({ width: 40, height: 40 })));
    stack.add(new Positioned(new Box(new BoxStyle({ width: 16, height: 16 })), -4, -4));

    const node = stack.render();
    expect(node.attributes.style).toContain('display: grid');
    expect(node.attributes.style).toContain('position: relative');
    expect(node.attributes.style).toContain('place-items: center center');
    expect(node.children[0].attributes.style).toContain('grid-area: 1 / 1');
    // The SAME literal StackRealizerTests pins on the C# side (hydration parity).
    expect(node.children[1].attributes.style).toBe('position: absolute; top: -4px; right: -4px');
  });
});
