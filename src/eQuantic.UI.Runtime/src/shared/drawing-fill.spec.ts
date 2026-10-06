import { describe, expect, it } from 'vitest';
import { effectiveStyle } from './style-atomizer';
import { Drawing } from './vocabulary';
import { SizeValue } from './value-types';
import type { VectorDrawing } from './vocabulary';

/** A 480 × 832 artwork, the shape falei.pt's district map is drawn in. */
const map = {
  minX: 0,
  minY: 8,
  width: 480,
  height: 832,
  shapes: [{ path: 'M0 8H480V840H0Z', fill: { kind: 'solid', color: { r: 0, g: 0, b: 0, a: 255 } }, stroke: { kind: 'none' }, strokeWidth: 1, evenOdd: false, opacity: 1 }],
} as unknown as VectorDrawing;

/** The literals DrawingRealizerTests pins on the C# side (hydration parity). */
describe('Drawing at a fill width', () => {
  it("takes the parent's width and keeps the artwork's aspect", () => {
    const style = effectiveStyle(new Drawing(map, SizeValue.fill).render());
    expect(style).toContain('width: 100%');
    expect(style).toContain('aspect-ratio: 0.5769');
    expect(style).not.toContain('height');
  });

  it('keeps a decided height over the aspect', () => {
    const style = effectiveStyle(new Drawing(map, SizeValue.fill, 120).render());
    expect(style).toContain('width: 100%');
    expect(style).toContain('height: 120px');
    expect(style).not.toContain('aspect-ratio');
  });

  it('takes a raw number as dp, the way the C# conversion passes it through', () => {
    const style = effectiveStyle(new Drawing(map, 240).render());
    expect(style).toContain('width: 240px');
    expect(style).toContain('height: 416px');
  });
});
