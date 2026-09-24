import { describe, it, expect } from 'vitest';
import {
  intConvert,
  intParse,
  intTryParse,
  realConvert,
  realParse,
  realTryParse,
  type IntegerType,
} from './number-parse';

/**
 * The integer and binary floating-point readers, against answers measured on .NET 10 in the
 * invariant culture. The conformance suite runs the same cases through the compiler on both sides
 * (NumberTextConformanceTests); these pin the runtime on its own.
 */
function outcome(read: () => number | bigint | undefined): string {
  try {
    const value = read();
    if (typeof value === 'bigint') return `${value}n`;
    return Object.is(value, -0) ? '-0' : String(value);
  } catch (error) {
    return (error as Error).message;
  }
}

const FORMAT = (text: string): string => `The input string '${text}' was not in a correct format.`;
const OVERFLOW = (name: string): string => `Value was either too large or too small for ${name}.`;

describe('an integer reads the whole text, under the Integer style', () => {
  it.each([
    ['12abc', FORMAT('12abc')],
    ['1e3', FORMAT('1e3')],
    ['1,000', FORMAT('1,000')],
    ['0x1F', FORMAT('0x1F')],
    ['', FORMAT('')],
    ['   ', FORMAT('   ')],
    ['-', FORMAT('-')],
    ['- 5', FORMAT('- 5')],
    ['5-', FORMAT('5-')],
    ['\u00a05', FORMAT('\u00a05')],
    [' 7 ', '7'],
    ['\t\n5\r\n', '5'],
    ['+0005', '5'],
    ['-0', '0'],
    ['000000000000000000042', '42'],
    ['5\0\0', '5'],
    ['5 \0', '5'],
    ['2147483647', '2147483647'],
    ['-2147483648', '-2147483648'],
    ['2147483648', OVERFLOW('an Int32')],
    ['-2147483649', OVERFLOW('an Int32')],
  ])('%j → %s', (text, expected) => {
    expect(outcome(() => intParse(text, 'int'))).toBe(expected);
  });

  it('reports a format error before an overflow, reading the digits to the end first', () => {
    expect(outcome(() => intParse('99999999999x', 'int'))).toBe(FORMAT('99999999999x'));
    expect(outcome(() => intParse('99999999999 ', 'int'))).toBe(OVERFLOW('an Int32'));
    expect(outcome(() => intParse('5\0 ', 'int'))).toBe(FORMAT('5\0 '));
  });
});

describe('each width holds its own range, and a long is a BigInt', () => {
  it.each<[IntegerType, string, string]>([
    ['byte', '255', '255'],
    ['byte', '256', OVERFLOW('an unsigned byte')],
    ['byte', '-1', OVERFLOW('an unsigned byte')],
    ['byte', '-0', '0'],
    ['sbyte', '-128', '-128'],
    ['sbyte', '128', OVERFLOW('a signed byte')],
    ['short', '-32768', '-32768'],
    ['short', '32768', OVERFLOW('an Int16')],
    ['ushort', '65535', '65535'],
    ['ushort', '65536', OVERFLOW('a UInt16')],
    ['uint', '4294967295', '4294967295'],
    ['uint', '4294967296', OVERFLOW('a UInt32')],
    ['uint', '-1', OVERFLOW('a UInt32')],
    ['long', '9007199254740993', '9007199254740993n'],
    ['long', '-9223372036854775808', '-9223372036854775808n'],
    ['long', '9223372036854775808', OVERFLOW('an Int64')],
    ['ulong', '18446744073709551615', '18446744073709551615n'],
    ['ulong', '18446744073709551616', OVERFLOW('a UInt64')],
  ])('%s %j → %s', (type, text, expected) => {
    expect(outcome(() => intParse(text, type as 'int'))).toBe(expected);
  });
});

describe('an integer under the styles a call names', () => {
  const Float = 167;
  it.each([
    ['1,000', 64, '1000'],
    ['1e3', 128, '1000'],
    ['1.0', 32, '1'],
    ['2.5e1', Float, '25'],
    ['-2147483648.000', Float, '-2147483648'],
    ['(5)', 16, '-5'],
    ['5-', 8, '-5'],
    ['¤5', 383, '5'],
    ['  (1,234)  ', 511, '-1234'],
    // A fraction that is not zero overflows: it is a number, and no integer holds it.
    ['1.5', 32, OVERFLOW('an Int32')],
    ['0.5', Float, OVERFLOW('an Int32')],
    ['2147483647.00000000000001', Float, OVERFLOW('an Int32')],
    ['1e10', Float, OVERFLOW('an Int32')],
  ])('%j under %i → %s', (text, styles, expected) => {
    expect(outcome(() => intParse(text, 'int', styles))).toBe(expected);
  });

  it('keeps the sign of a zero read with a decimal point, which an unsigned type cannot hold', () => {
    expect(outcome(() => intParse('-0', 'uint', Float))).toBe('0');
    expect(outcome(() => intParse('-0e5', 'uint', Float))).toBe('0');
    expect(outcome(() => intParse('-0.0', 'uint', Float))).toBe(OVERFLOW('a UInt32'));
    expect(outcome(() => intParse('-0.', 'uint', Float))).toBe(OVERFLOW('a UInt32'));
    expect(outcome(() => intParse('-0.0', 'int', Float))).toBe('0');
  });
});

describe("hex and binary read the type's bits, with no sign", () => {
  const HexNumber = 515;
  const BinaryNumber = 1027;
  it.each<[IntegerType, string, number, string]>([
    ['int', '1F', HexNumber, '31'],
    ['int', 'ffffffff', HexNumber, '-1'],
    ['int', '  00000000000000007FFFFFFF  ', HexNumber, '2147483647'],
    ['int', '1FFFFFFFF', HexNumber, OVERFLOW('an Int32')],
    ['int', '1FFFFFFFFx', HexNumber, FORMAT('1FFFFFFFFx')],
    ['int', '0x1F', HexNumber, FORMAT('0x1F')],
    ['int', '-1F', HexNumber, FORMAT('-1F')],
    ['int', ' 1F', 512, FORMAT(' 1F')],
    ['uint', 'FFFFFFFF', HexNumber, '4294967295'],
    ['sbyte', 'FF', HexNumber, '-1'],
    ['sbyte', '1FF', HexNumber, OVERFLOW('a signed byte')],
    ['short', 'FFFF', HexNumber, '-1'],
    ['long', 'ffffffffffffffff', HexNumber, '-1n'],
    ['ulong', 'ffffffffffffffff', HexNumber, '18446744073709551615n'],
    ['int', '101', BinaryNumber, '5'],
    ['int', '11111111111111111111111111111111', 1024, '-1'],
    ['int', '111111111111111111111111111111111', BinaryNumber, OVERFLOW('an Int32')],
    ['int', '102', BinaryNumber, FORMAT('102')],
  ])('%s %j under %i → %s', (type, text, styles, expected) => {
    expect(outcome(() => intParse(text, type as 'int', styles))).toBe(expected);
  });
});

describe('a style an integer cannot read', () => {
  const HEX_OR_BINARY =
    'With the AllowHexSpecifier or AllowBinarySpecifier bit set in the enum bit field, the only ' +
    "other valid bits that can be combined into the enum value must be AllowLeadingWhite and AllowTrailingWhite. (Parameter 'style')";
  const UNDEFINED = "An undefined NumberStyles value is being used. (Parameter 'style')";

  it.each([
    [512 | 4, HEX_OR_BINARY],
    [512 | 1024, HEX_OR_BINARY],
    [4096, UNDEFINED],
    [-1, UNDEFINED],
  ])('%i', (styles, message) => {
    expect(() => intParse('5', 'int', styles)).toThrow(message);
    expect(() => intTryParse('5', 'int', styles)).toThrow(message);
  });

  it('comes after a null text in Parse, and before it in TryParse, as .NET orders them', () => {
    expect(() => intParse(null, 'int', 4096)).toThrow("Value cannot be null. (Parameter 's')");
    expect(() => intTryParse(null, 'int', 4096)).toThrow(UNDEFINED);
  });
});

describe('TryParse answers undefined where Parse throws for the text', () => {
  it.each<[IntegerType, string | null, string]>([
    ['int', '42', '42'],
    ['int', 'x', 'undefined'],
    ['int', '2147483648', 'undefined'],
    ['int', null, 'undefined'],
    ['long', '5', '5n'],
    ['long', 'x', 'undefined'],
    ['ulong', '-5', 'undefined'],
    ['byte', '300', 'undefined'],
  ])('%s %j → %s', (type, text, expected) => {
    expect(outcome(() => intTryParse(text, type as 'int'))).toBe(expected);
  });
});

describe('Convert over text is Parse, except that a null text is zero', () => {
  it("reads null as the type's zero", () => {
    expect(intConvert(null, 'int')).toBe(0);
    expect(intConvert(null, 'long')).toBe(0n);
    expect(realConvert(null, 'double')).toBe(0);
  });

  it('reads text as Parse does, refusals included', () => {
    expect(intConvert('  42  ', 'int')).toBe(42);
    expect(intConvert('9007199254740993', 'long')).toBe(9007199254740993n);
    expect(() => intConvert('12abc', 'int')).toThrow(FORMAT('12abc'));
    expect(() => intConvert('256', 'byte')).toThrow(OVERFLOW('an unsigned byte'));
    expect(realConvert('1,5', 'double')).toBe(15);
    expect(() => realConvert('abc', 'double')).toThrow(FORMAT('abc'));
  });
});

describe('a double is the one nearest the digits', () => {
  it.each([
    ['1,5', '15'],
    ['1,234.5', '1234.5'],
    ['  -1.5e3  ', '-1500'],
    ['.5', '0.5'],
    ['5.', '5'],
    ['1E+2', '100'],
    ['0.1', '0.1'],
    ['5\0', '5'],
    ['-0', '-0'],
    ['-0.0', '-0'],
    ['4.9406564584124654e-324', '5e-324'],
    ['2.4703282292062328e-324', '5e-324'],
    ['1e-400', '0'],
    ['1.7976931348623157e308', '1.7976931348623157e+308'],
    ['1e309', 'Infinity'],
    ['-1e309', '-Infinity'],
    ['1e999999999999', 'Infinity'],
    ['1e-999999999999', '0'],
    ['abc', FORMAT('abc')],
    ['0x10', FORMAT('0x10')],
    ['', FORMAT('')],
    ['1.5e', FORMAT('1.5e')],
    ['1.5.0', FORMAT('1.5.0')],
    ['(1.5)', FORMAT('(1.5)')],
  ])('%j → %s', (text, expected) => {
    expect(outcome(() => realParse(text, 'double'))).toBe(expected);
  });

  it('refuses what the style a call names does not allow', () => {
    expect(outcome(() => realParse('1,5', 'double', 167))).toBe(FORMAT('1,5'));
    expect(outcome(() => realParse('1e5', 'double', 32))).toBe(FORMAT('1e5'));
    expect(outcome(() => realParse('(1.5)', 'double', 511))).toBe('-1.5');
    expect(outcome(() => realParse('¤1,234.5', 'double', 383))).toBe('1234.5');
  });
});

describe("the invariant culture's symbols, whatever the style", () => {
  it.each([
    ['Infinity', 'Infinity'],
    ['-infinity', '-Infinity'],
    ['+INFINITY', 'Infinity'],
    ['NaN', 'NaN'],
    [' nan ', 'NaN'],
    ['-NaN', 'NaN'],
    ['+nan', 'NaN'],
    // Trimmed of char.IsWhiteSpace, which takes a no-break space and U+0085 and leaves U+FEFF.
    ['\u00a0Infinity\u2003', 'Infinity'],
    ['\u0085Infinity', 'Infinity'],
    ['\ufeffInfinity', FORMAT('\ufeffInfinity')],
    ['Infinity\0', FORMAT('Infinity\0')],
    // Case-blind as an ordinal comparison is, which leaves the dotless i alone.
    ['\u0131nf\u0131n\u0131ty', FORMAT('\u0131nf\u0131n\u0131ty')],
    ['Infinityx', FORMAT('Infinityx')],
    ['+-Infinity', FORMAT('+-Infinity')],
    ['+ Infinity', FORMAT('+ Infinity')],
    ['- NaN', FORMAT('- NaN')],
    ['∞', FORMAT('∞')],
  ])('%j → %s', (text, expected) => {
    expect(outcome(() => realParse(text, 'double'))).toBe(expected);
  });

  it('reads them under a style that allows nothing else', () => {
    expect(realParse(' Infinity ', 'double', 0)).toBe(Infinity);
    expect(realParse('Infinity', 'double', 32)).toBe(Infinity);
  });
});

describe('a float is the single nearest the digits, rounded once', () => {
  it.each([
    ['0.1', 0.10000000149011612],
    ['3.4028235e38', 3.4028234663852886e38],
    ['3.5e38', Infinity],
    // Through a double, these land on the midpoint between two singles and round to the even one.
    ['1.0000000596046447753906250001', 1.0000001192092896],
    ['-1.0000000596046447753906250001', -1.0000001192092896],
    ['1.0000000596046447753906249999', 1],
    // A true tie goes to the even neighbour, up or down.
    ['1.000000059604644775390625', 1],
    ['1.000000178813934326171875', 1.0000002384185791],
    // Past the largest single, the midpoint is the one with 2^128, and a tie is infinity.
    ['340282356779733661637539395458142568447', 3.4028234663852886e38],
    ['340282356779733661637539395458142568448', Infinity],
    // Half the smallest single: a tie is zero, anything past it the smallest.
    [
      '7.00649232162408535461864791644958065640130970938257885878534141944895541342930300743319094181060791015625e-46',
      0,
    ],
    [
      '7.006492321624085354618647916449580656401309709382578858785341419448955413429303007433190941810607910156251e-46',
      1.401298464324817e-45,
    ],
    ['7.0064923216240861e-46', 1.401298464324817e-45],
  ])('%j → %d', (text, expected) => {
    expect(realParse(text, 'single')).toBe(expected);
  });

  it('keeps the sign of a zero', () => {
    expect(Object.is(realParse('-0', 'single'), -0)).toBe(true);
  });
});

describe("a real's TryParse and its style", () => {
  it('answers undefined where Parse throws for the text', () => {
    expect(realTryParse('x', 'double')).toBeUndefined();
    expect(realTryParse(null, 'single')).toBeUndefined();
    expect(realTryParse('-Infinity', 'double')).toBe(-Infinity);
    expect(realTryParse('1e5', 'double', 167)).toBe(100000);
  });

  it('refuses hex, binary and an undefined flag, after a null text in Parse and before it in TryParse', () => {
    const REAL_HEX =
      "The number styles AllowHexSpecifier and AllowBinarySpecifier are not supported on floating point data types. (Parameter 'style')";
    expect(() => realParse('1', 'double', 512)).toThrow(REAL_HEX);
    expect(() => realParse('1', 'double', 4096)).toThrow(
      "An undefined NumberStyles value is being used. (Parameter 'style')",
    );
    expect(() => realParse(null, 'double', 512)).toThrow("Value cannot be null. (Parameter 's')");
    expect(() => realTryParse(null, 'double', 512)).toThrow(REAL_HEX);
  });
});
