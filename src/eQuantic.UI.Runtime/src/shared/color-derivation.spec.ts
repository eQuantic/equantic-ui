import { describe, expect, it } from 'vitest';
import { Color, ColorToken, VariantColors } from './value-types';

/** The §10 hover derivation, byte-pinned to the C# side (DesignTokenTests twin). */
describe('hover derivation (C# cross-pin)', () => {
  it('midpoint is per-channel with .5 rounding up, alpha included', () => {
    expect(Color.midpointWith(Color.fromRgba(0, 0, 0, 0), Color.fromRgba(1, 1, 1, 255))).toEqual({
      r: 1,
      g: 1,
      b: 1,
      a: 128,
    });
  });

  it('VariantColors.hover derives base→pressed per leg (Photon primary pin)', () => {
    const token = (light: [number, number, number], dark: [number, number, number]) =>
      new ColorToken(Color.fromRgb(...light), Color.fromRgb(...dark));
    const primary = new VariantColors(
      token([0x00, 0x50, 0xa0], [0x5c, 0xa2, 0xe8]), // base
      token([0xff, 0xff, 0xff], [0x06, 0x26, 0x3f]), // onBase
      token([0x00, 0x42, 0x7f], [0x7c, 0xb5, 0xee]), // pressed
      token([0xe8, 0xf1, 0xfa], [0x0f, 0x27, 0x40]), // subtle
      token([0x00, 0x3e, 0x7e], [0xa8, 0xcd, 0xf2]), // onSubtle
    );

    expect(primary.hover.light).toEqual({ r: 0x00, g: 0x49, b: 0x90, a: 255 });
    expect(primary.hover.dark).toEqual({ r: 0x6c, g: 0xac, b: 0xeb, a: 255 });
  });
});
