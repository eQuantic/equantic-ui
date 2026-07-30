import { describe, it, expect } from 'vitest';
import { lowerVisualNode } from './lowering';
import { effectiveStyle } from './style-atomizer';
import { photonTheme } from './design-system.generated';
import { Grid, GridTrack } from './vocabulary';
import type { LoweringContext } from './lowering';
import type { VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** Spec S4 client lowering — the LITERAL strings the C# S4GridRealizerTests pin. */
describe('S4 grid lowering (C# cross-pin)', () => {
  it('lowers to CSS grid with tracks, gap pair and spans', () => {
    const grid = new Grid([GridTrack.flex(1), GridTrack.flex(3), GridTrack.fixed(240)], 12, 16, {
      width: { kind: 'fill', value: 0 } as never,
    });
    const box = { nodeKind: 'box', style: { height: { kind: 'fixed', value: 40 } } };
    grid.add({ ...box, gridSpan: 2 } as unknown as VisualNodeValue as never);
    grid.add(box as never);

    const node = lowerVisualNode(grid as unknown as VisualNodeValue, ctx);
    const style = effectiveStyle(node);
    expect(style).toContain('display: grid');
    expect(style).toContain('grid-template-columns: 1fr 3fr 240px');
    expect(style).toContain('gap: 16px 12px');
    expect(effectiveStyle(node.children[0])).toContain('grid-column: span 2');
    expect(effectiveStyle(node.children[1])).not.toContain('grid-column');
  });

  it('auto tracks lower to auto; equal gaps collapse', () => {
    const grid = new Grid([GridTrack.auto, GridTrack.flex()], 8);
    const style = effectiveStyle(lowerVisualNode(grid as unknown as VisualNodeValue, ctx));
    expect(style).toContain('grid-template-columns: auto 1fr');
    expect(style).toContain('gap: 8px');
  });
});
