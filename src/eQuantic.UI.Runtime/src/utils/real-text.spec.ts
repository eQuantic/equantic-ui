import { describe, it, expect } from 'vitest';
import { double, single } from './real-text';

/**
 * What .NET 10 writes for each value, invariant culture, measured with ToString() at the boundaries
 * of its notation (#336): fixed from a decimal exponent of -4 up to 16 for a double and 8 for a
 * float, scientific outside, with the exponent signed and at least two digits.
 */
describe('a double reads as .NET writes it', () => {
  it.each([
    [0, '0'],
    [-0, '-0'],
    [1e14, '100000000000000'],
    [1e16, '10000000000000000'],
    [9.9e16, '99000000000000000'],
    [1e17, '1E+17'],
    [-1e17, '-1E+17'],
    [123456789012345.6, '123456789012345.6'],
    [4611686018427387904, '4.611686018427388E+18'],
    [1e21, '1E+21'],
    [1.5e300, '1.5E+300'],
    [0.0001, '0.0001'],
    [1e-5, '1E-05'],
    [0.000012345, '1.2345E-05'],
    [1e-7, '1E-07'],
    [1.7976931348623157e308, '1.7976931348623157E+308'],
    [5e-324, '5E-324'],
    [2.2250738585072014e-308, '2.2250738585072014E-308'],
    [0.1 + 0.2, '0.30000000000000004'],
    [1 / 3, '0.3333333333333333'],
    [100, '100'],
    [NaN, 'NaN'],
    [Infinity, 'Infinity'],
    [-Infinity, '-Infinity'],
  ])('%s → %s', (value, expected) => {
    expect(double(value)).toBe(expected);
  });
});

describe('a float reads as .NET writes it', () => {
  it.each([
    [0, '0'],
    [-0, '-0'],
    [1e7, '10000000'],
    [1.5e8, '150000000'],
    [9.9e8, '990000000'],
    [1e9, '1E+09'],
    [1e10, '1E+10'],
    [1e-4, '0.0001'],
    [1e-5, '1E-05'],
    [3.4028234663852886e38, '3.4028235E+38'],
    [1.401298464324817e-45, '1E-45'],
    [0.1, '0.1'],
    [16777216, '16777216'],
    [NaN, 'NaN'],
    [Infinity, 'Infinity'],
    [-Infinity, '-Infinity'],
  ])('%s → %s', (value, expected) => {
    expect(single(value)).toBe(expected);
  });
});
