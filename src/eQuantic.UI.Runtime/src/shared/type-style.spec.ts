/**
 * TypeStyle's scaling, as its C# original computes it: the factor clamped from 0.5 to the style's
 * maxScale and the result snapped to the 0.5dp step, rounding half to even in single precision.
 * The expected values were measured on .NET (`TypeStyle.ScaledSize` and `ScaledLineHeight`), and a
 * transpiled twin that asks either one (the code editor's completion list sizes its documentation by
 * the line box) gets the number the native host draws with.
 */

import { describe, expect, it } from 'vitest';
import { TypeStyle } from './value-types';

describe('TypeStyle under a text-scale factor', () => {
  const measured: [TypeStyle, number, number, number][] = [
    // style, factor, ScaledSize, ScaledLineHeight (.NET)
    [new TypeStyle(13, 16.5, 'regular', 0, 1.3), 0.2, 6.5, 8],
    [new TypeStyle(13, 16.5, 'regular', 0, 1.3), 1, 13, 16.5],
    [new TypeStyle(13, 16.5, 'regular', 0, 1.3), 1.15, 15, 19],
    [new TypeStyle(13, 16.5, 'regular', 0, 1.3), 1.237, 16, 20.5],
    [new TypeStyle(13, 16.5, 'regular', 0, 1.3), 3, 17, 21.5],
    [new TypeStyle(15, 18.75, 'regular', 0, 2), 1, 15, 19],
    [new TypeStyle(15, 18.75, 'regular', 0, 2), 1.3, 19.5, 24.5],
    [new TypeStyle(15, 18.75, 'regular', 0, 2), 1.75, 26, 33],
    [new TypeStyle(15, 18.75, 'regular', 0, 2), 3, 30, 37.5],
  ];

  it.each(measured)('scales as .NET does (%#)', (style, factor, size, lineHeight) => {
    expect(style.scaledSize(factor)).toBe(size);
    expect(style.scaledLineHeight(factor)).toBe(lineHeight);
  });
});
