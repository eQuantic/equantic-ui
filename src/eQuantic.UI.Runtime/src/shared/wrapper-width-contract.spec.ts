import { describe, expect, it } from 'vitest';
import { lowerVisualNode } from './lowering';
import { effectiveStyle } from './style-atomizer';
import type { ColorTokenValue, VisualNodeValue } from './nodes';

/**
 * A wrapper carries its child's WHOLE width contract (C# cross-pin:
 * WrapperLayoutTransparencyTests).
 *
 * Each of these stands between a Fill child and its flex parent, so it takes the child's 100% —
 * otherwise the child's own 100% resolves against a shrink-to-fit box and collapses. None of them
 * took the child's max-width, which left a full-width wrapper holding a narrower block: the child
 * pinned to the start edge, and a centring row with an item already filling it.
 */
const textPrimary: ColorTokenValue = {
  light: { r: 0x17, g: 0x1b, b: 0x21, a: 255 },
  dark: { r: 0xf2, g: 0xf4, b: 0xf7, a: 255 },
};
const ctx = { textPrimary };

const capped = () => ({
  nodeKind: 'box',
  style: { width: { kind: 'fill' }, maxWidth: 980 },
  child: { nodeKind: 'text', content: 'panel', role: 'bodyL', maxLines: 0 },
});

// The twin writes ATOMIC CLASSES, not inline style — `effectiveStyle` reconstructs the resting
// declarations from them, which is what the C# side writes literally.
const styleOf = (node: unknown): string =>
  effectiveStyle(node as { attributes: Record<string, string | undefined> });

describe('a wrapper carries the whole width contract', () => {
  for (const [name, wrap] of [
    ['pressable', (child: unknown) => ({ nodeKind: 'pressable', child, onPressed: () => {} })],
    ['hoverable', (child: unknown) => ({ nodeKind: 'hoverable', child, onChanged: () => {} })],
    ['link', (child: unknown) => ({ nodeKind: 'link', child, destination: '/somewhere' })],
    ['adjustable', (child: unknown) => ({ nodeKind: 'adjustable', child, onAdjust: () => {} })],
  ] as const) {
    it(`${name} passes the cap through with the fill`, () => {
      const lowered = lowerVisualNode(wrap(capped()) as unknown as VisualNodeValue, ctx);

      expect(styleOf(lowered)).toContain('width: 100%');
      expect(styleOf(lowered)).toContain('max-width: 980px');
    });
  }

  it('the link carries the HEIGHT axis too, as the C# side always has', () => {
    // Half a contract is its own divergence: the C# realizer passes both axes through here, so a
    // Height=Fill child inside a Link filled on the server and hugged in the browser.
    const tall = {
      nodeKind: 'box',
      style: { height: { kind: 'fill' } },
      child: { nodeKind: 'text', content: 'x', role: 'bodyL', maxLines: 0 },
    };
    const lowered = lowerVisualNode(
      { nodeKind: 'link', child: tall, destination: '/somewhere' } as unknown as VisualNodeValue,
      ctx,
    );

    expect(styleOf(lowered)).toContain('height: 100%');
  });

  it('a child with no cap acquires none', () => {
    const plain = {
      nodeKind: 'box',
      style: { width: { kind: 'fill' } },
      child: { nodeKind: 'text', content: 'x', role: 'bodyL', maxLines: 0 },
    };
    const lowered = lowerVisualNode(
      { nodeKind: 'pressable', child: plain, onPressed: () => {} } as unknown as VisualNodeValue,
      ctx,
    );

    expect(styleOf(lowered)).toContain('width: 100%');
    expect(styleOf(lowered)).not.toContain('max-width');
  });
});
