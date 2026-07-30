import { describe, it, expect } from 'vitest';
import { lowerVisualNode } from './lowering';
import { effectiveStyle } from './style-atomizer';
import { photonTheme } from './design-system.generated';
import { Transform2D } from './value-types';
import type { LoweringContext } from './lowering';
import type { VisualNodeValue } from './nodes';

const ctx: LoweringContext = { textPrimary: photonTheme.textPrimary };

/** Spec S1 client lowering — the LITERAL strings the C# S1StyleRealizerTests pin (parity pair). */
describe('S1 style lowering (C# cross-pin)', () => {
  const box = (style: Record<string, unknown>): VisualNodeValue =>
    ({ nodeKind: 'box', style }) as unknown as VisualNodeValue;

  it('opacity lowers to the CSS declaration', () => {
    expect(effectiveStyle(lowerVisualNode(box({ opacity: 0.85 }), ctx))).toContain('opacity: 0.85');
  });

  it('transform emits the ordered list, only non-neutral parts', () => {
    const full = lowerVisualNode(
      box({ transform: { translateX: 4, translateY: -2, rotationDegrees: 8, scaleX: 1.05, scaleY: 1.05 } }),
      ctx,
    );
    expect(effectiveStyle(full)).toContain('transform: translate(4px, -2px) rotate(8deg) scale(1.05)');

    const rotateOnly = lowerVisualNode(box({ transform: { rotationDegrees: 90 } }), ctx);
    const style = effectiveStyle(rotateOnly);
    expect(style).toContain('transform: rotate(90deg)');
    expect(style).not.toContain('translate(');
    expect(style).not.toContain('scale(');
  });

  it('aspect-ratio lowers to the CSS declaration', () => {
    expect(
      effectiveStyle(lowerVisualNode(box({ aspectRatio: 16 / 9, width: { kind: 'fill', value: 0 } }), ctx)),
    ).toContain('aspect-ratio: 1.7778');
  });

  it('align-self overrides the container cross on the child only', () => {
    const row: VisualNodeValue = {
      nodeKind: 'row',
      gap: 8,
      main: 'start',
      cross: 'end',
      children: [
        box({ width: { kind: 'fixed', value: 10 }, height: { kind: 'fixed', value: 10 } }),
        {
          ...(box({ width: { kind: 'fixed', value: 10 }, height: { kind: 'fixed', value: 10 } }) as object),
          alignSelf: 'start',
        } as VisualNodeValue,
      ],
    } as unknown as VisualNodeValue;

    const node = lowerVisualNode(row, ctx);
    expect(effectiveStyle(node.children[0])).not.toContain('align-self');
    expect(effectiveStyle(node.children[1])).toContain('align-self: flex-start');
  });

  it('Transform2D mirror composes like the C# struct', () => {
    const t = Transform2D.translate(4, -2).withRotate(8).withScale(1.05);
    expect(t.translateX).toBe(4);
    expect(t.rotationDegrees).toBe(8);
    expect(t.scaleY).toBe(1.05);
    expect(Transform2D.scale(2, 3).scaleY).toBe(3);
  });
});
