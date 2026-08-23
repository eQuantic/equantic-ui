import { describe, it, expect } from 'vitest';
import { lowerVisualNode } from './lowering';
import { effectiveStyle } from './style-atomizer';
import { photonTheme } from './design-system.generated';
import type { LoweringContext } from './lowering';
import type { VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };
const box = (): VisualNodeValue =>
  ({
    nodeKind: 'box',
    style: { width: { kind: 'fixed', value: 10 }, height: { kind: 'fixed', value: 10 } },
  }) as unknown as VisualNodeValue;

/** Spec S7 client lowering — the LITERAL strings the C# S7ScrollRealizerTests pin. */
describe('S7 scroll semantics (C# cross-pin)', () => {
  it('sticky lowers to position:sticky at the offset', () => {
    const style = effectiveStyle(
      lowerVisualNode(
        { nodeKind: 'sticky', child: box(), offset: 8 } as unknown as VisualNodeValue,
        ctx,
      ),
    );
    expect(style).toContain('position: sticky');
    expect(style).toContain('top: 8px');
    // Chrome sits on its own plane, above anything the CONTENT can reach (the C# twin pins
    // the same literal). It was 1, which a merely raised card now out-stacks.
    expect(style).toContain('z-index: 100');
  });

  it('floating chrome outranks pinned chrome, so two of them do not tie', () => {
    // The C# twin pins the same rule. Reported by the site: a rail pinned on one page painted over
    // the header's open mega menu, because both are chrome and both were 100 — so document order
    // decided. The header's panel could not climb out either: its z-index lives inside the
    // header's own stacking context.
    const layer = (node: VisualNodeValue) =>
      Number(/z-index: (\d+)/.exec(effectiveStyle(lowerVisualNode(node, ctx)))?.[1]);

    const floating = layer({ nodeKind: 'sticky', child: box(), offset: 0, float: true } as unknown as VisualNodeValue);
    const pinned = layer({ nodeKind: 'sticky', child: box(), offset: 96 } as unknown as VisualNodeValue);

    expect(floating).toBe(110);
    expect(pinned).toBe(100);
    expect(floating).toBeGreaterThan(pinned);
  });

  it('positioned zIndex rides the anchor', () => {
    const stack = {
      nodeKind: 'stack',
      align: 'topStart',
      children: [box(), { nodeKind: 'positioned', child: box(), top: 0, zIndex: 3 }],
    } as unknown as VisualNodeValue;
    const node = lowerVisualNode(stack, ctx);
    expect(effectiveStyle(node.children[1])).toContain('z-index: 3');
  });

  it('both-axis scroll is auto on both', () => {
    const style = effectiveStyle(
      lowerVisualNode(
        { nodeKind: 'scrollView', axis: 'both', child: box() } as unknown as VisualNodeValue,
        ctx,
      ),
    );
    expect(style).toContain('overflow-x: auto');
    expect(style).toContain('overflow-y: auto');
  });
});
