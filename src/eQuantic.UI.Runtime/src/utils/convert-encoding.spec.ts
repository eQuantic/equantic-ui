import { describe, it, expect } from 'vitest';
import {
  toBase64CharArray,
  toBase64String,
  toHexString,
  toHexStringLower,
} from './convert-encoding';

// Every answer below was measured on .NET 10; the conformance suite runs the same calls on both
// sides (ConvertEncodingConformanceTests).
const counting = Array.from({ length: 114 }, (_, i) => (i * 7) & 0xff);

describe('toBase64String (Convert.ToBase64String)', () => {
  it('writes four characters for three bytes, padded with =', () => {
    expect(toBase64String([1, 2, 255])).toBe('AQL/');
    expect(toBase64String([1])).toBe('AQ==');
    expect(toBase64String([1, 2])).toBe('AQI=');
    expect(toBase64String([251, 255, 191])).toBe('+/+/');
    expect(toBase64String([])).toBe('');
  });

  it('reads a Uint8Array as it reads an array', () => {
    expect(toBase64String(new Uint8Array([1, 2, 255]))).toBe('AQL/');
  });

  it('encodes the range an offset and a length name', () => {
    expect(toBase64String([1, 2, 3, 4, 5], 1, 3)).toBe('AgME');
    expect(toBase64String([1, 2, 3, 4, 5], 5, 0)).toBe('');
  });

  it('breaks a line every 76 characters, and none after the last', () => {
    expect(toBase64String(counting, 0, 57, 1)).not.toContain('\r\n');
    expect(
      toBase64String(counting, 0, 58, 1)
        .split('\r\n')
        .map((line) => line.length),
    ).toEqual([76, 4]);
    expect(
      toBase64String(counting, undefined, undefined, 1)
        .split('\r\n')
        .map((line) => line.length),
    ).toEqual([76, 76]);
  });

  it('refuses as .NET refuses, in the order it checks', () => {
    expect(() => toBase64String(null)).toThrow("Value cannot be null. (Parameter 'inArray')");
    expect(() => toBase64String([1, 2, 3], -1, -1)).toThrow(
      "length ('-1') must be a non-negative value.",
    );
    expect(() => toBase64String([1, 2, 3], -1, 2)).toThrow(
      "offset ('-1') must be a non-negative value.",
    );
    expect(() => toBase64String([1, 2, 3], 2, 2)).toThrow(
      "offset ('2') must be less than or equal to '1'.",
    );
    expect(() => toBase64String([1, 2, 3], undefined, undefined, 2)).toThrow(
      "Illegal enum value: 2. (Parameter 'options')",
    );
    expect(() => toBase64String([1, 2, 3], 4, 0, 2)).toThrow(
      "offset ('4') must be less than or equal to '3'.",
    );
  });
});

describe('toHexString and toHexStringLower (Convert.ToHexString, Convert.ToHexStringLower)', () => {
  it('writes two digits a byte, in either case', () => {
    expect(toHexString([1, 2, 255, 16])).toBe('0102FF10');
    expect(toHexStringLower([1, 2, 255, 16])).toBe('0102ff10');
    expect(toHexString([1, 2, 255, 16], 1, 2)).toBe('02FF');
    expect(toHexString(new Uint8Array([0xc3, 0xa9]))).toBe('C3A9');
  });

  it('refuses a null array and a range past its end', () => {
    expect(() => toHexString(undefined)).toThrow("Value cannot be null. (Parameter 'inArray')");
    expect(() => toHexStringLower([1, 2, 3], 1, 3)).toThrow(
      "offset ('1') must be less than or equal to '0'.",
    );
  });
});

describe('toBase64CharArray (Convert.ToBase64CharArray)', () => {
  it('writes the characters from its offset and answers their count', () => {
    const chars = Array.from('........');
    expect(toBase64CharArray([1, 2, 255], 0, 3, chars, 2)).toBe(4);
    expect(chars.join('')).toBe('..AQL/..');
  });

  it('answers 0 for no bytes before it checks the room left', () => {
    expect(toBase64CharArray([1, 2, 3], 3, 0, [], 9)).toBe(0);
  });

  it('refuses an array with no room for the characters', () => {
    expect(() => toBase64CharArray([1, 2, 3], 0, 3, ['.', '.', '.', '.'], 1)).toThrow(
      "offsetOut ('1') must be less than or equal to '0'.",
    );
    expect(() => toBase64CharArray([1, 2, 3], 0, 0, null, 0)).toThrow(
      "Value cannot be null. (Parameter 'outArray')",
    );
  });
});
