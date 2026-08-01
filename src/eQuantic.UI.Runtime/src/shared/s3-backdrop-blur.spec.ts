import { describe, it, expect } from 'vitest';
import { lowerVisualNode } from './lowering';
import { effectiveStyle } from './style-atomizer';
import { photonTheme } from './design-system.generated';
import type { LoweringContext } from './lowering';
import type { VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** Spec S3 client lowering — the LITERAL strings the C# S3BackdropBlurRealizerTests pin. */
describe('S3 backdrop blur lowering (C# cross-pin)', () => {
  const box = (style: Record<string, unknown>): VisualNodeValue =>
    ({ nodeKind: 'box', style }) as unknown as VisualNodeValue;

  it('backdropBlur lowers to both declarations (Safari still needs the prefix)', () => {
    const style = effectiveStyle(lowerVisualNode(box({ backdropBlur: 12 }), ctx));
    expect(style).toContain('backdrop-filter: blur(12px)');
    expect(style).toContain('-webkit-backdrop-filter: blur(12px)');
  });

  it('zero/absent blur emits nothing', () => {
    expect(effectiveStyle(lowerVisualNode(box({}), ctx))).not.toContain('backdrop-filter');
    expect(effectiveStyle(lowerVisualNode(box({ backdropBlur: 0 }), ctx))).not.toContain('backdrop-filter');
  });
});
