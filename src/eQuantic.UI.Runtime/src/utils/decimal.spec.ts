import { describe, it, expect } from 'vitest';
import { Decimal, dec } from './decimal';
import { typesOf } from './exceptions';

/** The .NET type a call throws, the most derived, or undefined where it throws nothing. */
function thrownType(run: () => unknown): string | undefined {
  try {
    run();
  } catch (error) {
    return typesOf(error)?.[0];
  }
  return undefined;
}

describe('Decimal — exact base-10 arithmetic', () => {
  it('adds exactly (no binary float error)', () => {
    expect(dec('0.1').add(dec('0.2')).toString()).toBe('0.3'); // not 0.30000000000000004
    expect(dec('1.1').add(dec('2.2')).toString()).toBe('3.3');
  });

  it('subtracts exactly', () => {
    expect(dec('0.3').sub(dec('0.1')).toString()).toBe('0.2');
    expect(dec('5').sub(dec('2.5')).toString()).toBe('2.5');
  });

  it('takes a remainder exactly, with the dividend\'s sign, at the larger scale', () => {
    expect(dec('5.5').mod(dec('2')).toString()).toBe('1.5');
    expect(dec('-5.5').mod(dec('2')).toString()).toBe('-1.5');
    expect(dec('5.5').mod(dec('-2')).toString()).toBe('1.5');
    expect(dec('0.3').mod(dec('0.1')).toString()).toBe('0.0');
    expect(dec('5.50').mod(dec('2')).toString()).toBe('1.50');
    expect(() => dec('1').mod(dec('0'))).toThrow('Attempted to divide by zero.');
  });

  it('multiplies and preserves scale like .NET', () => {
    expect(dec('1.5').mul(dec('2')).toString()).toBe('3.0'); // .NET keeps the trailing zero
    expect(dec('0.1').mul(dec('0.1')).toString()).toBe('0.01');
  });

  it('divides terminating quotients', () => {
    expect(dec('10').div(dec('4')).toString()).toBe('2.5');
    expect(dec('1').div(dec('4')).toString()).toBe('0.25');
    expect(dec('6').div(dec('2')).toString()).toBe('3');
  });

  // Each row is .NET 10's answer (#764): 1.5 million random operations on every width DecCalc
  // takes a path for matched it before these were written down.
  const MAX = '79228162514264337593543950335';
  const OVERFLOW = 'Value was either too large or too small for a Decimal.';

  it("throws .NET's OverflowException for a result no decimal holds", () => {
    expect(() => dec(MAX).add(dec(MAX))).toThrow(OVERFLOW);
    expect(thrownType(() => dec(MAX).add(dec(MAX)))).toBe('System.OverflowException');
    expect(() => dec(`-${MAX}`).sub(dec('1'))).toThrow(OVERFLOW);
    expect(() => dec(MAX).mul(dec('2'))).toThrow(OVERFLOW);
    expect(() => dec(MAX).div(dec('0.5'))).toThrow(OVERFLOW);
    expect(() => dec(MAX).div(dec('0.1'))).toThrow(OVERFLOW);
  });

  it('rounds a sum or a product past 96 bits or 28 places half to even', () => {
    expect(dec(MAX).sub(dec('0.1')).toString()).toBe(MAX);
    expect(dec('7922.8162514264337593543950335').add(dec('4.012027874412007892')).toString()).toBe(
      '7926.828279300845767246395034',
    );
    expect(dec('7922.8162514264337593543950335').mul(dec('-4.012027874412007892')).toString()).toBe(
      '-31786.559644567307325397988085',
    );
    expect(dec('0.1234567890123456789').mul(dec('0.1234567890123456789')).toString()).toBe(
      '0.0152415787532388367501905200',
    );
    expect(dec('0.0000000000000000000000000005').mul(dec('0.1')).toString()).toBe(
      '0.0000000000000000000000000000',
    );
    expect(dec('0.0000000000000000000000000015').mul(dec('0.1')).toString()).toBe(
      '0.0000000000000000000000000002',
    );
    expect(dec('0.0000000000000000000000000251').mul(dec('0.1')).toString()).toBe(
      '0.0000000000000000000000000025',
    );
  });

  it('keeps as many places in a quotient as fit, rounded half to even', () => {
    expect(dec('1').div(dec('3')).toString()).toBe('0.3333333333333333333333333333');
    expect(dec('2').div(dec('3')).toString()).toBe('0.6666666666666666666666666667');
    expect(dec(MAX).div(dec('6')).toString()).toBe('13204693752377389598923991722');
    expect(dec('15518717730321859317').div(dec('0.00000003512407616471289109')).toString()).toBe(
      '441825648525173427785103587.23',
    );
    expect(dec('-55.4').div(dec('-7.9228162514264337593543950335')).toString()).toBe(
      '6.9924630638790486431695620028',
    );
  });

  it('gives each result the scale .NET gives it', () => {
    expect(dec('4.00').div(dec('2')).toString()).toBe('2.00');
    expect(dec('10').div(dec('0.1')).toString()).toBe('100');
    expect(dec('1').div(dec('0.5')).toString()).toBe('2');
    expect(dec('1.5').mod(dec('2.00')).toString()).toBe('1.5');
    expect(dec('0').mod(dec('1.50')).toString()).toBe('0');
    expect(dec('-5.5').mod(dec('5.50')).toString()).toBe('0.00');
    expect(dec('0.00').mul(dec('1.5')).toString()).toBe('0.000');
    expect(dec('0.00').mul(dec('12345678901.5')).toString()).toBe('0');
    expect(
      dec('0.0000000000000000000000000001').mul(dec('0.0000000000000000000000000001')).toString(),
    ).toBe('0');
    expect(dec('0.000000000000001').mul(dec('0.000000000000001')).toString()).toBe(
      '0.0000000000000000000000000000',
    );
  });

  it('compares and tests equality by value', () => {
    expect(dec('0.1').add(dec('0.2')).equals(dec('0.3'))).toBe(true);
    expect(dec('1.10').equals(dec('1.1'))).toBe(true);
    expect(dec('2.5').compareTo(dec('2.50'))).toBe(0);
    expect(dec('2.5').compareTo(dec('2.6'))).toBe(-1);
    expect(dec('3').compareTo(dec('2.6'))).toBe(1);
  });

  it('handles negatives', () => {
    expect(dec('-1.1').add(dec('2.2')).toString()).toBe('1.1');
    expect(dec('-0.3').toString()).toBe('-0.3');
  });

  it('serializes to a JSON string (exact wire protocol)', () => {
    expect(new Decimal(33n, 1).toJSON()).toBe('3.3');
    expect(JSON.stringify({ v: dec('1.1').add(dec('2.2')) })).toBe('{"v":"3.3"}');
  });

  it('round-trips a high-precision value through JSON without loss', () => {
    const original = dec('0.123456789012345678901234567'); // 27 digits — a double would mangle it
    const json = JSON.stringify({ v: original });
    expect(json).toBe('{"v":"0.123456789012345678901234567"}');
    const parsed = dec((JSON.parse(json) as { v: string }).v);
    expect(parsed.toString()).toBe('0.123456789012345678901234567');
  });
});

describe('Decimal.round — half to even', () => {
  it('rounds midpoints to the even neighbour, like Math.Round(decimal)', () => {
    expect(Decimal.from('1.5').round().toString()).toBe('2');
    expect(Decimal.from('2.5').round().toString()).toBe('2');
    expect(Decimal.from('-2.5').round().toString()).toBe('-2');
    expect(Decimal.from('2.51').round().toString()).toBe('3');
    expect(Decimal.from('2.345').round(2).toString()).toBe('2.34');
    expect(Decimal.from('2.355').round(2).toString()).toBe('2.36');
    expect(Decimal.from('7').round(2).toString()).toBe('7');
  });

  // .NET's decimal: AwayFromZero moves only a half, the directed modes move every value, and each
  // mode is read on a negative value too, where "away" and "toward -∞" part company.
  it.each([
    ['2.5', 0, 'awayFromZero', '3'],
    ['-2.5', 0, 'awayFromZero', '-3'],
    ['2.345', 2, 'awayFromZero', '2.35'],
    ['2.344', 2, 'awayFromZero', '2.34'],
    ['2.349', 2, 'toZero', '2.34'],
    ['-2.349', 2, 'toZero', '-2.34'],
    ['-2.341', 2, 'toNegativeInfinity', '-2.35'],
    ['2.349', 2, 'toNegativeInfinity', '2.34'],
    ['2.341', 2, 'toPositiveInfinity', '2.35'],
    ['-2.349', 2, 'toPositiveInfinity', '-2.34'],
    ['2.345', 2, 'toEven', '2.34'],
    ['2.30', 1, 'toPositiveInfinity', '2.3'],
  ] as const)('rounds %s to %i digits %s as %s', (value, digits, mode, expected) => {
    expect(Decimal.from(value).round(digits, mode).toString()).toBe(expected);
  });

  it('refuses a mode that is not one, even where nothing needs rounding, as .NET does', () => {
    expect(thrownType(() => Decimal.from('1.2').round(2, 'sideways' as never))).toBe('System.ArgumentException');
    expect(thrownType(() => Decimal.from('1.25').round(1, 'sideways' as never))).toBe('System.ArgumentException');
    expect(thrownType(() => Decimal.from('1.2').round(2, 'toString' as never))).toBe('System.ArgumentException');
  });

  it('refuses a digit count outside 0..28, as .NET does', () => {
    expect(thrownType(() => Decimal.from('1.5').round(29))).toBe('System.ArgumentOutOfRangeException');
    expect(thrownType(() => Decimal.from('1.5').round(-1))).toBe('System.ArgumentOutOfRangeException');
  });
});
