import { describe, expect, it } from 'vitest';
import type { ColorTokenValue, VisualNodeValue } from './nodes';
import { lowerVisualNode } from './lowering';
import { effectiveStyle } from './style-atomizer';
import { TypeStyle } from './value-types';

// The C# FluidTypeRealizerTests twin (#652): a size that follows the window lowers to a clamp and
// a unitless line height, a size in dp stays px — the same strings, so SSR and hydration agree.
// Text needs an ink to lower; which one does not matter here.
const ink = { light: { r: 23, g: 27, b: 33, a: 255 }, dark: { r: 242, g: 244, b: 247, a: 255 } } as unknown as ColorTokenValue;
const ctx = { textPrimary: ink };

const display = TypeStyle.ofSize(40, 'bold').withFluidSize(34, 4.2, 54);

const heading = (styleOverride: TypeStyle): VisualNodeValue =>
  ({ nodeKind: 'text', content: 'Fala com Portugal', role: 'bodyM', styleOverride }) as unknown as VisualNodeValue;

describe('fluid type', () => {
  it('lowers a fluid size to a clamp, with the line box as a ratio', () => {
    const style = effectiveStyle(lowerVisualNode(heading(display), ctx));
    expect(style).toContain('font-size: clamp(34px, 4.2vw, 54px)');
    expect(style).toContain('line-height: 1.25');
  });

  it('lets the tracking follow a fluid size, in em on the web and scaled at a window', () => {
    const tight = new TypeStyle(54, 53, 'extraBold', -1.89, 1.3).withFluidSize(34, 4.2, 54);
    expect(effectiveStyle(lowerVisualNode(heading(tight), ctx))).toContain('letter-spacing: -0.035em');
    expect(tight.atWindow(400).tracking).toBeCloseTo(-1.19, 3);
  });

  it('leaves a size in dp alone', () => {
    const style = effectiveStyle(lowerVisualNode(heading(TypeStyle.ofSize(16, 'regular')), ctx));
    expect(style).toContain('font-size: 16px');
    expect(style).toContain('line-height: 20px');
    expect(style).not.toContain('clamp');
  });

  it('resolves at a window between the floor and the ceiling, the line box following', () => {
    expect(display.atWindow(400).size).toBe(34);
    expect(display.atWindow(1000).size).toBe(42);
    expect(display.atWindow(2000).size).toBe(54);
    expect(display.atWindow(400).lineHeight).toBe(42.5);
  });

  it('holds the ceiling where no window is known, and gives way to a size in dp', () => {
    expect(display.size).toBe(54);
    expect(display.withSize(20).fluid).toBeNull();
    expect(display.withSize(20).atWindow(2000).size).toBe(20);
  });

  it('rounds in single precision as the C# does, from single-rounded arguments', () => {
    // C#: WithFluidSize(34, 4.2f, 54.1f) on a 32dp style with a 41dp line box.
    const style = new TypeStyle(32, 41, 'regular', 0, 1.3).withFluidSize(34, Math.fround(4.2), Math.fround(54.1));
    expect(style.lineHeight).toBe(69.31562042236328);
    expect(style.fluid!.at(1000)).toBe(42);
  });

  it('refuses a size that cannot be, as the C# does', () => {
    expect(() => display.withFluidSize(0, 4, 54)).toThrow(/positive floor/);
    expect(() => display.withFluidSize(34, 0, 54)).toThrow(/share of the window/);
    expect(() => display.withFluidSize(34, 4, 30)).toThrow(/under its floor/);
  });
});
