import { describe, it, expect } from 'vitest';
import { lowerVisualNode } from './lowering';
import { effectiveStyle } from './style-atomizer';
import { photonTheme } from './design-system.generated';
import type { LoweringContext } from './lowering';
import type { VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** Spec S3 client lowering — the LITERAL strings the C# S3WrapRealizerTests pin. */
describe('S3 wrap lowering (C# cross-pin)', () => {
  const flex = (kind: 'row' | 'column', extra: Record<string, unknown>): VisualNodeValue =>
    ({ nodeKind: kind, gap: 8, main: 'start', cross: 'center', children: [], ...extra }) as unknown as VisualNodeValue;

  it('wrap emits flex-wrap and keeps the single gap', () => {
    const style = effectiveStyle(lowerVisualNode(flex('row', { wrap: true }), ctx));
    expect(style).toContain('flex-wrap: wrap');
    expect(style).toContain('gap: 8px');
  });

  it('runGap emits the pair in the stacking order of the axis', () => {
    expect(effectiveStyle(lowerVisualNode(flex('row', { wrap: true, runGap: 12 }), ctx)))
      .toContain('gap: 12px 8px');
    expect(effectiveStyle(lowerVisualNode(flex('column', { wrap: true, runGap: 12 }), ctx)))
      .toContain('gap: 8px 12px');
  });

  it('no wrap never emits flex-wrap', () => {
    expect(effectiveStyle(lowerVisualNode(flex('row', {}), ctx))).not.toContain('flex-wrap');
  });
});
