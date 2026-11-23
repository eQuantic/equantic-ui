import { exception } from './exceptions';

/**
 * `Uri.EscapeDataString`, `Uri.EscapeUriString` and `Uri.UnescapeDataString` as .NET 10 runs them (#1805).
 *
 * They were lowered to `encodeURIComponent`, `encodeURI` and `decodeURIComponent`, which differ from .NET's:
 *
 * - `encodeURIComponent` keeps `! ' ( ) *`, which .NET escapes, so a link a component built with
 *   `Uri.EscapeDataString` changed in the browser once the page hydrated;
 * - `encodeURI` escapes `[` and `]`, which .NET keeps;
 * - both throw on a lone surrogate, which .NET writes as U+FFFD (`%EF%BF%BD`);
 * - `decodeURIComponent` throws on an escape that is no escape (`%`, `%zz`) and on bytes that are no UTF-8
 *   (`%C3%28`, `%FF`), all of which .NET leaves as they are written, decoding what is around them.
 *
 * The conformance suite runs each on both sides (`UriEscapingConformanceTests`).
 */

const HEX = '0123456789ABCDEF';

/** The ASCII characters a function keeps as they are, by code. */
function keeping(characters: string): boolean[] {
  const kept = new Array<boolean>(128).fill(false);
  for (const character of characters) kept[character.charCodeAt(0)] = true;
  return kept;
}

/** RFC 3986's unreserved characters: all `EscapeDataString` keeps. */
const UNRESERVED = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~';
const DATA_KEPT = keeping(UNRESERVED);
/** `EscapeUriString` keeps the reserved characters too, which a whole URI is written with; never `%`. */
const URI_KEPT = keeping(UNRESERVED + "!#$&'()*+,/:;=?@[]");

function refuseNull(text: string | null | undefined, parameter: string): asserts text is string {
  if (text === null || text === undefined) {
    throw exception('System.ArgumentNullException', `Value cannot be null. (Parameter '${parameter}')`);
  }
}

/** The text's UTF-8 bytes, each kept or written `%XX` in upper-case hex. A lone surrogate is U+FFFD, which is
 *  what `TextEncoder` writes for it, as .NET does. */
function escaped(text: string, kept: boolean[]): string {
  let result = '';
  for (const byte of new TextEncoder().encode(text)) {
    result += byte < 0x80 && kept[byte] ? String.fromCharCode(byte) : '%' + HEX[byte >> 4] + HEX[byte & 0x0f];
  }
  return result;
}

/** `Uri.EscapeDataString(text)`. */
export function escapeData(text: string): string {
  refuseNull(text, 'stringToEscape');
  return escaped(text, DATA_KEPT);
}

/** `Uri.EscapeUriString(text)`. */
export function escapeUri(text: string): string {
  refuseNull(text, 'stringToEscape');
  return escaped(text, URI_KEPT);
}

function hexValue(code: number): number {
  if (code >= 0x30 && code <= 0x39) return code - 0x30;
  if (code >= 0x41 && code <= 0x46) return code - 0x37;
  if (code >= 0x61 && code <= 0x66) return code - 0x57;
  return -1;
}

/** The byte an escape at `index` writes, or -1 when what stands there is no escape. */
function escapeAt(text: string, index: number): number {
  if (text.charCodeAt(index) !== 0x25 || index + 2 >= text.length) return -1;
  const high = hexValue(text.charCodeAt(index + 1));
  const low = hexValue(text.charCodeAt(index + 2));
  return high < 0 || low < 0 ? -1 : (high << 4) | low;
}

/** The second byte's range each lead byte allows, and how many continuation bytes it takes: Unicode's
 *  well-formed UTF-8 (Table 3-7), which refuses overlong forms, surrogates and what lies past U+10FFFF. */
function sequenceOf(lead: number): { length: number; low: number; high: number } | null {
  if (lead >= 0xc2 && lead <= 0xdf) return { length: 2, low: 0x80, high: 0xbf };
  if (lead === 0xe0) return { length: 3, low: 0xa0, high: 0xbf };
  if ((lead >= 0xe1 && lead <= 0xec) || lead === 0xee || lead === 0xef) return { length: 3, low: 0x80, high: 0xbf };
  if (lead === 0xed) return { length: 3, low: 0x80, high: 0x9f };
  if (lead === 0xf0) return { length: 4, low: 0x90, high: 0xbf };
  if (lead >= 0xf1 && lead <= 0xf3) return { length: 4, low: 0x80, high: 0xbf };
  if (lead === 0xf4) return { length: 4, low: 0x80, high: 0x8f };
  return null;
}

/** A run of escapes decoded: each well-formed UTF-8 character as itself, and each byte that starts none, or
 *  starts one the run cuts short, written as it stood in the text. */
function decodedRun(text: string, bytes: number[], starts: number[]): string {
  let result = '';
  for (let at = 0; at < bytes.length; ) {
    const lead = bytes[at];
    if (lead < 0x80) {
      result += String.fromCharCode(lead);
      at += 1;
      continue;
    }
    const sequence = sequenceOf(lead);
    let wellFormed = sequence !== null && at + sequence.length <= bytes.length;
    if (sequence !== null && wellFormed) {
      const second = bytes[at + 1];
      wellFormed = second >= sequence.low && second <= sequence.high;
      for (let next = 2; wellFormed && next < sequence.length; next++) {
        const continuation = bytes[at + next];
        wellFormed = continuation >= 0x80 && continuation <= 0xbf;
      }
    }
    if (sequence === null || !wellFormed) {
      result += text.substring(starts[at], starts[at] + 3);
      at += 1;
      continue;
    }
    let codePoint = lead & (0xff >> (sequence.length + 1));
    for (let next = 1; next < sequence.length; next++) codePoint = (codePoint << 6) | (bytes[at + next] & 0x3f);
    result += String.fromCodePoint(codePoint);
    at += sequence.length;
  }
  return result;
}

/** `Uri.UnescapeDataString(text)`. */
export function unescapeData(text: string): string {
  refuseNull(text, 'stringToUnescape');
  if (text.indexOf('%') < 0) return text;
  let result = '';
  for (let index = 0; index < text.length; ) {
    if (escapeAt(text, index) < 0) {
      result += text[index];
      index += 1;
      continue;
    }
    const bytes: number[] = [];
    const starts: number[] = [];
    for (let byte = escapeAt(text, index); byte >= 0; byte = escapeAt(text, index)) {
      bytes.push(byte);
      starts.push(index);
      index += 3;
    }
    result += decodedRun(text, bytes, starts);
  }
  return result;
}
