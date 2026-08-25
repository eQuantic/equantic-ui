import { describe, expect, it } from 'vitest';

import { SizeValue } from './value-types';

/**
 * The twin's STATIC SURFACE, pinned name by name against C#'s. The lowering already understood the
 * 'windowMinus' kind — cross-pinned when it shipped — while the class itself never grew the
 * factory, so `SizeValue.windowMinus(34)` in emitted code was `undefined is not a function` on a
 * page the build had accepted. Agreement of the lowering is not completeness of the surface: a
 * static added on the C# side belongs HERE in the same change.
 */
describe('SizeValue statics mirror the C# surface', () => {
  it('carries every C# factory by its camelCase name', () => {
    for (const name of ['hug', 'fill', 'fixed', 'from', 'windowMinus'])
      expect(name in SizeValue, `SizeValue.${name}`).toBe(true);
  });

  it('windowMinus builds the kind the lowering already understands', () => {
    const cap = SizeValue.windowMinus(34);
    expect(cap.kind).toBe('windowMinus');
    expect(cap.value).toBe(34);
  });

  it('refuses a negative inset exactly as C# does', () => {
    expect(() => SizeValue.windowMinus(-1)).toThrowError(/never negative/);
  });
});
