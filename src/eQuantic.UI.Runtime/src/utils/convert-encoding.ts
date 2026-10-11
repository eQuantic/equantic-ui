/**
 * `Convert.ToBase64String`, `ToBase64CharArray`, `ToHexString` and `ToHexStringLower`, every overload,
 * as .NET 10's `Convert.cs` writes them: the same characters, the same line breaks, and the same
 * refusals in the same order.
 *
 * The compiler hands each argument to its parameter, the offset, the length and the options as the
 * values they are. Each was lowered to `String()` of the array, so `new byte[] { 1, 2, 255 }` was
 * `1,2,255` where .NET writes `AQL/` and `0102FF` (#716).
 *
 * A `byte[]` is an array of numbers when C# builds one and a `Uint8Array` when the platform does
 * (`Encoding.UTF8.GetBytes`): both index and count alike, so both are read here. A `char[]` holds
 * strings of one unit, and `ToBase64CharArray` writes them into the one it is handed.
 */
import { exception } from './exceptions';
import { outOfRange, requireNonNegative } from './string-statics';

/** What a `byte[]` is in the browser. */
type Bytes = ArrayLike<number>;

/** `Base64FormattingOptions.InsertLineBreaks`, the one flag .NET defines. */
const INSERT_LINE_BREAKS = 1;
/** `Convert.Base64LineBreakPosition`: a line of base64 holds 76 characters. */
const LINE = 76;
const ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
const DIGITS_UPPER = '0123456789ABCDEF';
const DIGITS_LOWER = '0123456789abcdef';

/** `ArgumentNullException.ThrowIfNull`, in .NET's words. */
function requireArray(parameter: string, value: unknown): void {
  if (value == null)
    throw exception(
      'System.ArgumentNullException',
      `Value cannot be null. (Parameter '${parameter}')`,
    );
}

/** `ArgumentOutOfRangeException.ThrowIfGreaterThan`, in .NET's words. */
function requireAtMost(parameter: string, value: number, other: number): void {
  if (value > other)
    throw outOfRange(
      parameter,
      `${parameter} ('${value}') must be less than or equal to '${other}'.`,
      value,
    );
}

/** Any flag besides `InsertLineBreaks` is refused, as `Arg_EnumIllegalVal` words it. */
function requireOptions(options: number): void {
  if (options !== 0 && options !== INSERT_LINE_BREAKS)
    throw exception(
      'System.ArgumentException',
      `Illegal enum value: ${options}. (Parameter 'options')`,
    );
}

/** The offset and the length of a range of the array, checked as the overloads that take them do:
 * the length, then the offset, then the room the offset leaves. */
function requireRange(array: Bytes, offset: number, length: number): void {
  requireNonNegative('length', length);
  requireNonNegative('offset', offset);
  requireAtMost('offset', offset, array.length - length);
}

/** `ToBase64_CalculateAndValidateOutputLength`: four characters for every three bytes or part of
 * them, and two for each line break, which no line ends the text with. */
function base64Length(length: number, lineBreaks: boolean): number {
  let size = Math.ceil(length / 3) * 4;
  if (size === 0 || !lineBreaks) return size;
  let breaks = Math.floor(size / LINE);
  if (size % LINE === 0) breaks--;
  size += breaks * 2;
  return size;
}

/** `ConvertToBase64Array`: the characters of `length` bytes from `offset`, a line break before every
 * group of four that would start a line past the 76th character. */
function base64(bytes: Bytes, offset: number, length: number, lineBreaks: boolean): string[] {
  const out: string[] = [];
  const whole = offset + length - (length % 3);
  let column = 0;
  let i = offset;
  for (; i < whole; i += 3) {
    if (lineBreaks) {
      if (column === LINE) {
        out.push('\r', '\n');
        column = 0;
      }
      column += 4;
    }
    const a = bytes[i];
    const b = bytes[i + 1];
    const c = bytes[i + 2];
    out.push(
      ALPHABET[a >> 2],
      ALPHABET[((a & 0x03) << 4) | (b >> 4)],
      ALPHABET[((b & 0x0f) << 2) | (c >> 6)],
      ALPHABET[c & 0x3f],
    );
  }
  const rest = length % 3;
  if (lineBreaks && rest !== 0 && column === LINE) out.push('\r', '\n');
  if (rest === 2) {
    const a = bytes[i];
    const b = bytes[i + 1];
    out.push(
      ALPHABET[a >> 2],
      ALPHABET[((a & 0x03) << 4) | (b >> 4)],
      ALPHABET[(b & 0x0f) << 2],
      '=',
    );
  } else if (rest === 1) {
    const a = bytes[i];
    out.push(ALPHABET[a >> 2], ALPHABET[(a & 0x03) << 4], '=', '=');
  }
  return out;
}

/**
 * `Convert.ToBase64String(inArray[, options])`, `(inArray, offset, length[, options])` and
 * `(ReadOnlySpan<byte>[, options])`: the range is checked where the overload takes one, then the
 * options, as .NET checks them.
 */
export function toBase64String(
  inArray: Bytes | null | undefined,
  offset?: number,
  length?: number,
  options = 0,
): string {
  requireArray('inArray', inArray);
  const bytes = inArray as Bytes;
  const start = offset ?? 0;
  const count = length ?? bytes.length;
  if (offset !== undefined) requireRange(bytes, start, count);
  requireOptions(options);
  return base64(bytes, start, count, options === INSERT_LINE_BREAKS).join('');
}

/**
 * `Convert.ToBase64CharArray(inArray, offsetIn, length, outArray, offsetOut[, options])`: the characters
 * written into `outArray` from `offsetOut`, and their count.
 */
export function toBase64CharArray(
  inArray: Bytes | null | undefined,
  offsetIn: number,
  length: number,
  outArray: string[] | null | undefined,
  offsetOut: number,
  options = 0,
): number {
  requireArray('inArray', inArray);
  requireArray('outArray', outArray);
  requireNonNegative('length', length);
  requireNonNegative('offsetIn', offsetIn);
  requireNonNegative('offsetOut', offsetOut);
  requireOptions(options);
  const bytes = inArray as Bytes;
  const chars = outArray as string[];
  requireAtMost('offsetIn', offsetIn, bytes.length - length);
  if (length === 0) return 0;
  const lineBreaks = options === INSERT_LINE_BREAKS;
  const required = base64Length(length, lineBreaks);
  requireAtMost('offsetOut', offsetOut, chars.length - required);
  const written = base64(bytes, offsetIn, length, lineBreaks);
  for (let i = 0; i < written.length; i++) chars[offsetOut + i] = written[i];
  return written.length;
}

function hex(
  inArray: Bytes | null | undefined,
  offset: number | undefined,
  length: number | undefined,
  digits: string,
): string {
  requireArray('inArray', inArray);
  const bytes = inArray as Bytes;
  const start = offset ?? 0;
  const count = length ?? bytes.length;
  if (offset !== undefined) requireRange(bytes, start, count);
  let text = '';
  for (let i = start; i < start + count; i++)
    text += digits[bytes[i] >> 4] + digits[bytes[i] & 0x0f];
  return text;
}

/** `Convert.ToHexString(inArray)`, `(inArray, offset, length)` and `(ReadOnlySpan<byte>)`: two
 * uppercase digits a byte. */
export function toHexString(
  inArray: Bytes | null | undefined,
  offset?: number,
  length?: number,
): string {
  return hex(inArray, offset, length, DIGITS_UPPER);
}

/** `Convert.ToHexStringLower`, every overload: two lowercase digits a byte. */
export function toHexStringLower(
  inArray: Bytes | null | undefined,
  offset?: number,
  length?: number,
): string {
  return hex(inArray, offset, length, DIGITS_LOWER);
}
