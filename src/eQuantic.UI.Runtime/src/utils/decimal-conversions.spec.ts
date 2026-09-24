import { describe, it, expect } from 'vitest';
import { Decimal, decConvert } from './decimal';

/**
 * What .NET 10 answers for each input, invariant culture, measured with `decimal.Parse`, the
 * `(decimal)` casts and `Convert.ToDecimal` and written here as it printed (#358). A value is the
 * decimal's `ToString()`; an exception is its type, and the messages are checked once below.
 */
function outcome(read: () => Decimal): string {
  try {
    return read().toString();
  } catch (error) {
    const message = (error as Error).message;
    if (message.endsWith('was not in a correct format.')) return 'FormatException';
    if (message === 'Value was either too large or too small for a Decimal.')
      return 'OverflowException';
    return message;
  }
}

describe('decimal.Parse reads a number as .NET does', () => {
  it.each([
    ['0.1', '0.1'],
    ['1.50', '1.50'],
    ['-0', '0'],
    ['-0.00', '0.00'],
    ['0.000', '0.000'],
    ['000', '0'],
    ['  -5  ', '-5'],
    ['5-', '-5'],
    ['5 -', '-5'],
    ['5 - ', '-5'],
    ['- 5', 'FormatException'],
    ['+5', '5'],
    ['(5)', 'FormatException'],
    ['1,234.5', '1234.5'],
    ['1,2,3', '123'],
    ['1,,2', '12'],
    [',5', 'FormatException'],
    ['1,', '1'],
    ['1,.5', '1.5'],
    ['.5', '0.5'],
    ['-.5', '-0.5'],
    ['5.', '5'],
    ['.', 'FormatException'],
    ['', 'FormatException'],
    [' ', 'FormatException'],
    ['-', 'FormatException'],
    ['+', 'FormatException'],
    ['1e5', 'FormatException'],
    ['1E5', 'FormatException'],
    ['0x10', 'FormatException'],
    ['1_000', 'FormatException'],
    ['\u0661\u0662', 'FormatException'],
    ['\u00a05', 'FormatException'],
    ['5\u0000', '5'],
    ['5\u0000\u0000', '5'],
    ['5\u0000x', 'FormatException'],
    ['\t5\n', '5'],
    ['\u000b5\f', '5'],
    ['\r5', '5'],
    ['+-5', 'FormatException'],
    ['-+5', 'FormatException'],
    ['5+', '5'],
    ['--5', 'FormatException'],
    ['5--', 'FormatException'],
    ['-5-', 'FormatException'],
    ['-5+', 'FormatException'],
    ['1.2.3', 'FormatException'],
    ['1.2,3', 'FormatException'],
    ['79228162514264337593543950335', '79228162514264337593543950335'],
    ['79228162514264337593543950336', 'OverflowException'],
    ['-79228162514264337593543950335', '-79228162514264337593543950335'],
    ['-79228162514264337593543950336', 'OverflowException'],
    ['79228162514264337593543950335.4', '79228162514264337593543950335'],
    ['79228162514264337593543950335.5', 'OverflowException'],
    ['79228162514264337593543950334.5', '79228162514264337593543950334'],
    ['7.9228162514264337593543950335', '7.9228162514264337593543950335'],
    ['7.92281625142643375935439503355', '7.922816251426433759354395034'],
    ['7.92281625142643375935439503345', '7.9228162514264337593543950334'],
    ['0.00000000000000000000000000005', '0.0000000000000000000000000000'],
    ['0.00000000000000000000000000015', '0.0000000000000000000000000002'],
    ['0.000000000000000000000000000051', '0.0000000000000000000000000001'],
    ['0.0000000000000000000000000000500', '0.0000000000000000000000000000'],
    ['0.00000000000000000000000000006', '0.0000000000000000000000000001'],
    ['1.00000000000000000000000000005', '1.0000000000000000000000000000'],
    ['1.00000000000000000000000000015', '1.0000000000000000000000000002'],
    ['1.000000000000000000000000000000000', '1.0000000000000000000000000000'],
    ['123456789012345678901234567890', 'OverflowException'],
    ['12345678901234567890123456789.5', '12345678901234567890123456790'],
    ['12345678901234567890123456788.5', '12345678901234567890123456788'],
    ['1234567890123456789012345678.95', '1234567890123456789012345679.0'],
    ['0000000000000000000000000000000000000001.5', '1.5'],
    ['0.0000000000000000000000000000000001', '0.0000000000000000000000000000'],
    ['-0.0000000000000000000000000000000001', '0.0000000000000000000000000000'],
    ['0.0000000000000000000000000000000000', '0.0000000000000000000000000000'],
    ['99999999999999999999999999999', 'OverflowException'],
    ['9999999999999999999999999999.9', '10000000000000000000000000000'],
    ['3.0500000000000000000000000001', '3.0500000000000000000000000001'],
    ['3.05000000000000000000000000001', '3.0500000000000000000000000000'],
    ['3.050000000000000000000000000001', '3.0500000000000000000000000000'],
    ['1234.5678', '1234.5678'],
    ['-1234.5678', '-1234.5678'],
    ['0.10', '0.10'],
    ['100', '100'],
    ['1,000,000.00', '1000000.00'],
  ])('%j → %s', (text, expected) => {
    expect(outcome(() => Decimal.parse(text))).toBe(expected);
  });
});

describe('decimal.Parse with the styles a call names', () => {
  it.each([
    ['Float', 167, '1e5', '100000'],
    ['Float', 167, '1.5E-3', '0.0015'],
    ['Float', 167, '1e+2', '100'],
    ['Float', 167, '-1e2', '-100'],
    ['Float', 167, '1e', 'FormatException'],
    ['Float', 167, '1e+', 'FormatException'],
    ['Float', 167, '1e-', 'FormatException'],
    ['Float', 167, 'e5', 'FormatException'],
    ['Float', 167, '1,000', 'FormatException'],
    ['Float', 167, '1e29', 'OverflowException'],
    ['Float', 167, '1e28', '10000000000000000000000000000'],
    ['Float', 167, '7.9e28', '79000000000000000000000000000'],
    ['Float', 167, '1e-28', '0.0000000000000000000000000001'],
    ['Float', 167, '1e-29', '0.0000000000000000000000000000'],
    ['Float', 167, '1e-30', '0.0000000000000000000000000000'],
    ['Float', 167, '1.23456789e-20', '0.0000000000000000000123456789'],
    ['Float', 167, '1e999999999', 'OverflowException'],
    ['Float', 167, '1e1000000000', 'OverflowException'],
    ['Float', 167, '1e-1000000000', '0.0000000000000000000000000000'],
    ['Float', 167, '5e-1', '0.5'],
    ['Float', 167, '0e10', '0'],
    ['Float', 167, '0e-50', '0.0000000000000000000000000000'],
    ['Float', 167, '(5)', 'FormatException'],
    ['Float', 167, '(5', 'FormatException'],
    ['Float', 167, '5)', 'FormatException'],
    ['Float', 167, '( 5 )', 'FormatException'],
    ['Float', 167, '(-5)', 'FormatException'],
    ['Float', 167, '-(5)', 'FormatException'],
    ['Float', 167, '\u00a45', 'FormatException'],
    ['Float', 167, '5\u00a4', 'FormatException'],
    ['Float', 167, '\u00a4 5', 'FormatException'],
    ['Float', 167, '-\u00a45', 'FormatException'],
    ['Float', 167, '-\u00a4 5', 'FormatException'],
    ['Float', 167, '- \u00a45', 'FormatException'],
    ['Float', 167, '\u00a4-5', 'FormatException'],
    ['Float', 167, '$5', 'FormatException'],
    ['Float', 167, '1,000.5e2', 'FormatException'],
    ['Float', 167, '(1,234.50)', 'FormatException'],
    ['Float', 167, '5.5', '5.5'],
    ['Float', 167, ' 5', '5'],
    ['Float', 167, '5', '5'],
    ['Float', 167, '1.5e0', '1.5'],
    ['Float', 167, '1e0001', '10'],
    ['Float', 167, '12e-1', '1.2'],
    ['Float', 167, '1.e5', '100000'],
    ['Float', 167, '.e5', 'FormatException'],
    ['Float', 167, '1e5.5', 'FormatException'],
    ['Float', 167, '1e5 ', '100000'],
    ['Float', 167, '1e5-', 'FormatException'],
    ['Float', 167, '1e-5-', 'FormatException'],
    ['Any', 511, '1e5', '100000'],
    ['Any', 511, '1.5E-3', '0.0015'],
    ['Any', 511, '1e+2', '100'],
    ['Any', 511, '-1e2', '-100'],
    ['Any', 511, '1e', 'FormatException'],
    ['Any', 511, '1e+', 'FormatException'],
    ['Any', 511, '1e-', 'FormatException'],
    ['Any', 511, 'e5', 'FormatException'],
    ['Any', 511, '1,000', '1000'],
    ['Any', 511, '1e29', 'OverflowException'],
    ['Any', 511, '1e28', '10000000000000000000000000000'],
    ['Any', 511, '7.9e28', '79000000000000000000000000000'],
    ['Any', 511, '1e-28', '0.0000000000000000000000000001'],
    ['Any', 511, '1e-29', '0.0000000000000000000000000000'],
    ['Any', 511, '1e-30', '0.0000000000000000000000000000'],
    ['Any', 511, '1.23456789e-20', '0.0000000000000000000123456789'],
    ['Any', 511, '1e999999999', 'OverflowException'],
    ['Any', 511, '1e1000000000', 'OverflowException'],
    ['Any', 511, '1e-1000000000', '0.0000000000000000000000000000'],
    ['Any', 511, '5e-1', '0.5'],
    ['Any', 511, '0e10', '0'],
    ['Any', 511, '0e-50', '0.0000000000000000000000000000'],
    ['Any', 511, '(5)', '-5'],
    ['Any', 511, '(5', 'FormatException'],
    ['Any', 511, '5)', 'FormatException'],
    ['Any', 511, '( 5 )', 'FormatException'],
    ['Any', 511, '(-5)', 'FormatException'],
    ['Any', 511, '-(5)', 'FormatException'],
    ['Any', 511, '\u00a45', '5'],
    ['Any', 511, '5\u00a4', '5'],
    ['Any', 511, '\u00a4 5', '5'],
    ['Any', 511, '-\u00a45', '-5'],
    ['Any', 511, '-\u00a4 5', '-5'],
    ['Any', 511, '- \u00a45', 'FormatException'],
    ['Any', 511, '\u00a4-5', '-5'],
    ['Any', 511, '$5', 'FormatException'],
    ['Any', 511, '1,000.5e2', '100050'],
    ['Any', 511, '(1,234.50)', '-1234.50'],
    ['Any', 511, '5.5', '5.5'],
    ['Any', 511, ' 5', '5'],
    ['Any', 511, '5', '5'],
    ['Any', 511, '1.5e0', '1.5'],
    ['Any', 511, '1e0001', '10'],
    ['Any', 511, '12e-1', '1.2'],
    ['Any', 511, '1.e5', '100000'],
    ['Any', 511, '.e5', 'FormatException'],
    ['Any', 511, '1e5.5', 'FormatException'],
    ['Any', 511, '1e5 ', '100000'],
    ['Any', 511, '1e5-', '-100000'],
    ['Any', 511, '1e-5-', '-0.00001'],
    ['Currency', 383, '1e5', 'FormatException'],
    ['Currency', 383, '1.5E-3', 'FormatException'],
    ['Currency', 383, '1e+2', 'FormatException'],
    ['Currency', 383, '-1e2', 'FormatException'],
    ['Currency', 383, '1e', 'FormatException'],
    ['Currency', 383, '1e+', 'FormatException'],
    ['Currency', 383, '1e-', 'FormatException'],
    ['Currency', 383, 'e5', 'FormatException'],
    ['Currency', 383, '1,000', '1000'],
    ['Currency', 383, '1e29', 'FormatException'],
    ['Currency', 383, '1e28', 'FormatException'],
    ['Currency', 383, '7.9e28', 'FormatException'],
    ['Currency', 383, '1e-28', 'FormatException'],
    ['Currency', 383, '1e-29', 'FormatException'],
    ['Currency', 383, '1e-30', 'FormatException'],
    ['Currency', 383, '1.23456789e-20', 'FormatException'],
    ['Currency', 383, '1e999999999', 'FormatException'],
    ['Currency', 383, '1e1000000000', 'FormatException'],
    ['Currency', 383, '1e-1000000000', 'FormatException'],
    ['Currency', 383, '5e-1', 'FormatException'],
    ['Currency', 383, '0e10', 'FormatException'],
    ['Currency', 383, '0e-50', 'FormatException'],
    ['Currency', 383, '(5)', '-5'],
    ['Currency', 383, '(5', 'FormatException'],
    ['Currency', 383, '5)', 'FormatException'],
    ['Currency', 383, '( 5 )', 'FormatException'],
    ['Currency', 383, '(-5)', 'FormatException'],
    ['Currency', 383, '-(5)', 'FormatException'],
    ['Currency', 383, '\u00a45', '5'],
    ['Currency', 383, '5\u00a4', '5'],
    ['Currency', 383, '\u00a4 5', '5'],
    ['Currency', 383, '-\u00a45', '-5'],
    ['Currency', 383, '-\u00a4 5', '-5'],
    ['Currency', 383, '- \u00a45', 'FormatException'],
    ['Currency', 383, '\u00a4-5', '-5'],
    ['Currency', 383, '$5', 'FormatException'],
    ['Currency', 383, '1,000.5e2', 'FormatException'],
    ['Currency', 383, '(1,234.50)', '-1234.50'],
    ['Currency', 383, '5.5', '5.5'],
    ['Currency', 383, ' 5', '5'],
    ['Currency', 383, '5', '5'],
    ['Currency', 383, '1.5e0', 'FormatException'],
    ['Currency', 383, '1e0001', 'FormatException'],
    ['Currency', 383, '12e-1', 'FormatException'],
    ['Currency', 383, '1.e5', 'FormatException'],
    ['Currency', 383, '.e5', 'FormatException'],
    ['Currency', 383, '1e5.5', 'FormatException'],
    ['Currency', 383, '1e5 ', 'FormatException'],
    ['Currency', 383, '1e5-', 'FormatException'],
    ['Currency', 383, '1e-5-', 'FormatException'],
    ['Integer', 7, ' 5', '5'],
    ['Integer', 7, '5', '5'],
    ['None', 0, '5', '5'],
    ['AllowExponent', 128, '1e5', '100000'],
    ['AllowExponent', 128, '1e+2', '100'],
    ['AllowExponent', 128, '1e28', '10000000000000000000000000000'],
    ['AllowExponent', 128, '1e-28', '0.0000000000000000000000000001'],
    ['AllowExponent', 128, '1e-29', '0.0000000000000000000000000000'],
    ['AllowExponent', 128, '1e-30', '0.0000000000000000000000000000'],
    ['AllowExponent', 128, '1e-1000000000', '0.0000000000000000000000000000'],
    ['AllowExponent', 128, '5e-1', '0.5'],
    ['AllowExponent', 128, '0e10', '0'],
    ['AllowExponent', 128, '0e-50', '0.0000000000000000000000000000'],
    ['AllowExponent', 128, '5', '5'],
    ['AllowExponent', 128, '1e0001', '10'],
    ['AllowExponent', 128, '12e-1', '1.2'],
    ['AllowParentheses', 48, '(5)', '-5'],
    ['AllowParentheses', 48, '5.5', '5.5'],
    ['AllowParentheses', 48, '5', '5'],
  ])('%s (%i): %j → %s', (_style, styles, text, expected) => {
    expect(outcome(() => Decimal.parse(text, styles))).toBe(expected);
  });
});

describe('a style a decimal cannot read throws before the text is read', () => {
  it.each([
    [
      512,
      "The number styles AllowHexSpecifier and AllowBinarySpecifier are not supported on floating point data types. (Parameter 'style')",
    ],
    [
      515,
      "The number styles AllowHexSpecifier and AllowBinarySpecifier are not supported on floating point data types. (Parameter 'style')",
    ],
    [
      1024,
      "The number styles AllowHexSpecifier and AllowBinarySpecifier are not supported on floating point data types. (Parameter 'style')",
    ],
    [4096, "An undefined NumberStyles value is being used. (Parameter 'style')"],
    [4103, "An undefined NumberStyles value is being used. (Parameter 'style')"],
  ])('%i', (styles, message) => {
    expect(() => Decimal.parse('5', styles)).toThrow(message);
    expect(() => Decimal.tryParse('5', styles)).toThrow(message);
  });
});

describe('decimal.TryParse answers undefined where Parse throws for the text', () => {
  it.each([
    ['0.1', '0.1'],
    ['abc', undefined],
    ['', undefined],
    ['79228162514264337593543950336', undefined],
    ['1e5', undefined],
    [' 5 ', '5'],
  ])('%j → %s', (text, expected) => {
    expect(Decimal.tryParse(text)?.toString()).toBe(expected);
  });

  it('reads no text as a failure, where Parse throws', () => {
    expect(Decimal.tryParse(null)).toBeUndefined();
    expect(() => Decimal.parse(null)).toThrow("Value cannot be null. (Parameter 's')");
  });

  it('refuses a null text before a style in Parse, and the style first in TryParse, as .NET does', () => {
    expect(() => Decimal.parse(null, 512)).toThrow("Value cannot be null. (Parameter 's')");
    expect(() => Decimal.tryParse(null, 512)).toThrow(
      "The number styles AllowHexSpecifier and AllowBinarySpecifier are not supported on floating point data types. (Parameter 'style')",
    );
  });
});

describe("the messages are .NET's", () => {
  it('for text that is not a number, naming it', () => {
    expect(() => Decimal.parse('- 5')).toThrow(
      "The input string '- 5' was not in a correct format.",
    );
  });
  it('for a number no decimal holds', () => {
    expect(() => Decimal.parse('79228162514264337593543950336')).toThrow(
      'Value was either too large or too small for a Decimal.',
    );
  });
});

describe('a double converts to a decimal as .NET converts it', () => {
  it.each([
    [0.1, '0.1'],
    [0.30000000000000004, '0.3'],
    [0.3333333333333333, '0.333333333333333'],
    [0.6666666666666666, '0.666666666666667'],
    [1000000000000000, '1000000000000000'],
    [123456789012345.67, '123456789012346'],
    [1234567890123456.8, '1234567890123460'],
    [1e20, '100000000000000000000'],
    [1e28, '10000000000000000000000000000'],
    [7.922816251426433e28, '79228162514264300000000000000'],
    [7.922816251426434e28, 'OverflowException'],
    [1e-28, '0.0000000000000000000000000001'],
    [1e-29, '0'],
    [2.5e-29, '0'],
    [5e-29, '0'],
    [5e-29, '0'],
    [1e-30, '0'],
    [5e-324, '0'],
    [-0, '0'],
    [NaN, 'OverflowException'],
    [Infinity, 'OverflowException'],
    [-Infinity, 'OverflowException'],
    [-1.5, '-1.5'],
    [0.5, '0.5'],
    [1.5, '1.5'],
    [2.5, '2.5'],
    [0.1234567890123455, '0.123456789012346'],
    [0.1234567890123465, '0.123456789012346'],
    [1.000000000000005, '1.00000000000001'],
    [1.000000000000015, '1.00000000000002'],
    [9999999999999996, '10000000000000000'],
    [999999999999999.5, '1000000000000000'],
    [1.234567890123456e-6, '0.00000123456789012346'],
    [1.7976931348623157e308, 'OverflowException'],
    [123.456, '123.456'],
    [100, '100'],
    [100000000000000, '100000000000000'],
    [99999999999999.98, '100000000000000'],
    [10000000000000000, '10000000000000000'],
    [1.2345678901234567e19, '12345678901234600000'],
    [0.30000000000000004, '0.3'],
    [1e23, '100000000000000000000000'],
    [1e24, '1000000000000000000000000'],
    [1e25, '10000000000000000000000000'],
    [1e26, '100000000000000000000000000'],
    [1e27, '1000000000000000000000000000'],
    [9.5e-29, '0.0000000000000000000000000001'],
    [4.4e-29, '0'],
    [3e-5, '0.00003'],
    [1.1, '1.1'],
    [2.2, '2.2'],
    [3.3, '3.3'],
    [1e-10, '0.0000000001'],
    [1.5e-15, '0.0000000000000015'],
    [7e-29, '0.0000000000000000000000000001'],
    [5.5e-28, '0.0000000000000000000000000005'],
    [1e-27, '0.000000000000000000000000001'],
    [5e-324, '0'],
    [2.2250738585072014e-308, '0'],
    [-1234560000000, '-1234560000000'],
    [8.5, '8.5'],
    [9.5, '9.5'],
  ])('%s → %s', (value, expected) => {
    expect(outcome(() => Decimal.fromDouble(value))).toBe(expected);
  });
});

describe('a float converts to a decimal as .NET converts it', () => {
  it.each([
    [0.1, '0.1'],
    [0.2, '0.2'],
    [0.33333334, '0.3333333'],
    [16777216, '16777220'],
    [1e-7, '0.0000001'],
    [3.4028235e38, 'OverflowException'],
    [1.1754944e-38, '0'],
    [0.12345675, '0.1234567'],
    [0.12345685, '0.1234569'],
    [1.0000005, '1'],
    [7.9228163e28, 'OverflowException'],
    [1e28, '9999999000000000000000000000'],
    [-0, '0'],
    [NaN, 'OverflowException'],
    [Infinity, 'OverflowException'],
    [1e-28, '0.0000000000000000000000000001'],
    [5e-29, '0.0000000000000000000000000001'],
    [1e-29, '0'],
    [123.456, '123.456'],
    [10000000, '10000000'],
    [1234567.5, '1234568'],
    [12345678, '12345680'],
    [2.5, '2.5'],
    [3.4e10, '34000000000'],
    [0.3, '0.3'],
    [1e-10, '0.0000000001'],
    [1e28, '9999999000000000000000000000'],
    [3.9e28, '39000000000000000000000000000'],
    [16777216, '16777220'],
    [8388608, '8388608'],
    [4e-29, '0'],
    [6e-29, '0.0000000000000000000000000001'],
  ])('%s → %s', (value, expected) => {
    expect(outcome(() => Decimal.fromSingle(value))).toBe(expected);
  });
});

describe('Convert.ToDecimal of a value the call site cannot type', () => {
  it('reads null as zero, where Parse throws', () => {
    expect(decConvert(null).toString()).toBe('0');
    expect(decConvert(undefined).toString()).toBe('0');
  });
  it('parses a string with the thousands the default style allows', () => {
    expect(decConvert('1,234.5').toString()).toBe('1234.5');
  });
  it('reads a boolean as one or zero', () => {
    expect(decConvert(true).toString()).toBe('1');
  });
  it('reads a long exactly', () => {
    expect(decConvert(9223372036854775807n).toString()).toBe('9223372036854775807');
    expect(decConvert(-9223372036854775808n).toString()).toBe('-9223372036854775808');
  });
  it('reads a number as the double, or the single, the call site says it is', () => {
    expect(decConvert(0.1 + 0.2).toString()).toBe('0.3');
    expect(decConvert(Math.fround(0.1), 'single').toString()).toBe('0.1');
  });
  it('hands a Decimal back as it is', () => {
    const value = Decimal.parse('1.50');
    expect(decConvert(value)).toBe(value);
  });
});

describe('a decimal read from text does exact arithmetic', () => {
  it("answers 0.3 for decimal.Parse('0.1') + 0.2m", () => {
    expect(Decimal.parse('0.1').add(Decimal.from('0.2')).toString()).toBe('0.3');
  });
});
