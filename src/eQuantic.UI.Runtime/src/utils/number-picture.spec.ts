import { describe, it, expect } from 'vitest';
import { drawPicture, type PictureNumber, type PictureSymbols } from './number-picture';

// A custom picture in a culture's own symbols (#445). The conformance suite holds the picture
// against .NET in the invariant culture; this holds what the invariant culture cannot show — a
// group size other than three, a comma for a point, a minus that is not a hyphen — with the
// strings .NET 10 writes for the same value and culture (measured with `dotnet fsi`).

const invariant: PictureSymbols = {
  decimal: '.',
  group: ',',
  groupSizes: [3],
  minus: '-',
  plus: '+',
  percent: '%',
  perMille: '‰',
};
const ptBR: PictureSymbols = { ...invariant, decimal: ',', group: '.' };
const enIN: PictureSymbols = { ...invariant, groupSizes: [3, 2] };
const svSE: PictureSymbols = { ...invariant, decimal: ',', group: ' ', minus: '−' };

/** `0.digits × 10^scale`, as a double's digits reach a picture. */
function double(digits: string, scale: number, negative = false): PictureNumber {
  return { negative, digits, scale, floating: true };
}

describe('drawPicture', () => {
  it('groups by the culture’s sizes, the last one repeating', () => {
    expect(drawPicture(double('1234567', 7), '#,##0', enIN)).toBe('12,34,567');
    expect(drawPicture(double('1234567', 7), '#,##0', invariant)).toBe('1,234,567');
  });

  it('writes the culture’s point, group separator and percent', () => {
    expect(drawPicture(double('12345', 4), '#,##0.00', ptBR)).toBe('1.234,50');
    expect(drawPicture(double('12345', 4), '0.0%', ptBR)).toBe('123450,0%');
  });

  it('writes the culture’s minus, in the number and in its exponent', () => {
    expect(drawPicture(double('15', 1, true), '0.0', svSE)).toBe('−1,5');
    expect(drawPicture(double('12', -3), '0.0E-00', svSE)).toBe('1,2E−04');
  });

  it('draws a negative number through its own section without a sign, and zero through the third', () => {
    expect(drawPicture(double('1', 1, true), '0.00;(0.00);zero', invariant)).toBe('(1.00)');
    expect(drawPicture(double('', 0), '0.00;(0.00);zero', invariant)).toBe('zero');
  });

  it('keeps a double’s negative zero, and drops a decimal’s', () => {
    expect(drawPicture(double('4', 0, true), '0', invariant)).toBe('-0');
    expect(
      drawPicture({ negative: true, digits: '4', scale: 0, floating: false }, '0', invariant),
    ).toBe('0');
  });
});
