import { formatLocale } from './culture';
import { exception } from './exceptions';

/**
 * A text's case as .NET 10 changes it: `ToUpper`, `ToLower` and their invariant pair, of a string and
 * of a char (#848, #725).
 *
 * .NET changes each UTF-16 code unit, or surrogate pair, to exactly one other, by the SIMPLE case
 * mappings of its own Unicode data, and in a culture. JavaScript's `toUpperCase` and `toLowerCase`
 * apply the FULL mappings, which lengthen a text (`ß` is `SS`, `ﬁ` is `FI`, `ᾳ` is `ΑΙ`, and `İ`
 * lower-cased is `i` with a combining dot), read the context of a final sigma (`ΟΔΟΣ` is `οδος`), and
 * know no culture. So a text that holds none of those characters is changed by the engine whole, and
 * any other one code point at a time, each keeping what .NET keeps:
 *
 * - a mapping that lengthens is the simple one, which is the code point itself, except for the Greek
 *   letters with a iota subscript, which take the letter with a prosgegrammeni (`ᾳ` is `ᾼ`);
 * - the dotted and the dotless i are the culture's: tr and az upper-case `i` to `İ` and lower-case `I`
 *   to `ı`, every other culture upper-cases `ı` to `I` and lower-cases `İ` to `i`, and the invariant
 *   culture leaves both as they are;
 * - a code point the engine's Unicode cases and .NET 10's does not keeps its case: `ƛ`, whose capital
 *   is Unicode 16's, and the scripts that came with it.
 *
 * The last list is the difference measured, code point by code point over every plane, between .NET
 * 10 and engines of a later Unicode (bun 1.4, Node 22, and the browsers of their year). The bun this
 * repository ships cases as .NET 10 does, so the conformance suite, which compares every code unit of
 * the BMP and every code point of the first supplementary plane with .NET's own answer, proves the
 * rest of the rules there. An engine whose Unicode is older than .NET's can still leave out a mapping
 * .NET has; the list moves when either side does.
 */

/** How a culture cases the two i's. */
type Casing = 'invariant' | 'turkic' | 'other';

/** `ArgumentNullException` for a char's culture handed as null, in .NET's words. */
const NULL_CULTURE = "Value cannot be null. (Parameter 'culture')";

/** The code points .NET 10 does not case and an engine of a later Unicode does, as ranges. */
const KEEPS_CASE: readonly (readonly [number, number])[] = [
  [0x019b, 0x019b],
  [0x0264, 0x0264],
  [0x1c89, 0x1c8a],
  [0xa7cb, 0xa7cf],
  [0xa7d2, 0xa7d5],
  [0xa7da, 0xa7dc],
  [0x10d50, 0x10d65],
  [0x10d70, 0x10d85],
  [0x16ea0, 0x16eb8],
  [0x16ebb, 0x16ed3],
];

/** The simple upper case of a letter whose full one lengthens, where it is not the letter itself. */
const SIMPLE_UPPER = new Map<number, number>([
  [0x1fb3, 0x1fbc],
  [0x1fc3, 0x1fcc],
  [0x1ff3, 0x1ffc],
]);
for (const start of [0x1f80, 0x1f90, 0x1fa0]) {
  for (let unit = start; unit < start + 8; unit++) SIMPLE_UPPER.set(unit, unit + 8);
}

/** The characters an engine upper-cases otherwise than .NET does, the culture's i's aside: those whose
 * full mapping lengthens, those of {@link KEEPS_CASE} in the BMP, and any surrogate. */
const UPPER_APART =
  '\\u00df\\u0149\\u01f0\\u0390\\u03b0\\u0587\\u1e96-\\u1e9a\\u1f50\\u1f52\\u1f54\\u1f56\\u1f80-\\u1faf' +
  '\\u1fb2-\\u1fb4\\u1fb6\\u1fb7\\u1fbc\\u1fc2-\\u1fc4\\u1fc6\\u1fc7\\u1fcc\\u1fd2\\u1fd3\\u1fd6\\u1fd7' +
  '\\u1fe2-\\u1fe4\\u1fe6\\u1fe7\\u1ff2-\\u1ff4\\u1ff6\\u1ff7\\u1ffc\\ufb00-\\ufb06\\ufb13-\\ufb17';

/** The same for lower case: the capital sigma, which an engine reads in its context, and the dotted
 * capital I, whose full mapping lengthens. */
const LOWER_APART = '\\u03a3\\u0130';

const BMP_KEEPS_CASE = '\\u019b\\u0264\\u1c89\\u1c8a\\ua7cb-\\ua7cf\\ua7d2-\\ua7d5\\ua7da-\\ua7dc';

/** Whether a text holds a character the engine cases otherwise than .NET, per direction and culture. */
const APART = {
  upper: {
    invariant: new RegExp(`[${UPPER_APART}${BMP_KEEPS_CASE}\\u0131\\ud800-\\udfff]`),
    turkic: new RegExp(`[${UPPER_APART}${BMP_KEEPS_CASE}i\\u0131\\ud800-\\udfff]`),
    other: new RegExp(`[${UPPER_APART}${BMP_KEEPS_CASE}\\ud800-\\udfff]`),
  },
  lower: {
    invariant: new RegExp(`[${LOWER_APART}${BMP_KEEPS_CASE}\\ud800-\\udfff]`),
    turkic: new RegExp(`[${LOWER_APART}${BMP_KEEPS_CASE}I\\ud800-\\udfff]`),
    other: new RegExp(`[${LOWER_APART}${BMP_KEEPS_CASE}\\ud800-\\udfff]`),
  },
} as const;

function keepsCase(codePoint: number): boolean {
  for (const [first, last] of KEEPS_CASE) {
    if (codePoint < first) return false;
    if (codePoint <= last) return true;
  }
  return false;
}

/**
 * The casing of the culture the call names: the invariant one, or the page's, which is the invariant
 * one too until the server installs another. tr and az are told by their language, as .NET tells them.
 */
function casingOf(invariant: boolean | undefined): Casing {
  if (invariant) return 'invariant';
  const culture = formatLocale();
  if (culture === undefined) return 'invariant';
  const language = culture.split('-')[0].toLowerCase();
  return language === 'tr' || language === 'az' ? 'turkic' : 'other';
}

/**
 * One code point's upper case in the invariant culture, as .NET's. .NET's ordinal casing, which an
 * ordinal comparison that ignores case reads, is this one but for the long s, which it keeps
 * (`ordinalUpper`).
 */
export function invariantUpper(codePoint: number): number {
  if (codePoint === 0x131 || keepsCase(codePoint)) return codePoint;
  const upper = [...String.fromCodePoint(codePoint).toUpperCase()];
  return upper.length === 1
    ? (upper[0].codePointAt(0) as number)
    : (SIMPLE_UPPER.get(codePoint) ?? codePoint);
}

/** One code point's upper case, as .NET's. */
function upperOf(codePoint: number, casing: Casing): number {
  if (codePoint === 0x69 && casing === 'turkic') return 0x130;
  if (codePoint === 0x131 && casing !== 'invariant') return 0x49;
  return invariantUpper(codePoint);
}

/** One code point's lower case, as .NET's. */
function lowerOf(codePoint: number, casing: Casing): number {
  if (codePoint === 0x49) return casing === 'turkic' ? 0x131 : 0x69;
  if (codePoint === 0x130) return casing === 'invariant' ? codePoint : 0x69;
  if (keepsCase(codePoint)) return codePoint;
  // One code point alone has no context, so a capital sigma is the medial one, as .NET maps it.
  const lower = [...String.fromCodePoint(codePoint).toLowerCase()];
  return lower.length === 1 ? (lower[0].codePointAt(0) as number) : codePoint;
}

/** The text changed a code point at a time: a surrogate pair as its code point, and a lone surrogate
 * as itself, which no case mapping touches. */
function changed(text: string, change: (codePoint: number) => number): string {
  let result = '';
  for (let index = 0; index < text.length; ) {
    const codePoint = text.codePointAt(index) as number;
    result += String.fromCodePoint(change(codePoint));
    index += codePoint > 0xffff ? 2 : 1;
  }
  return result;
}

/**
 * `string.ToUpper()`, `ToUpper(culture)` and `ToUpperInvariant()`, and `char.ToUpper`'s three: in the
 * invariant culture where `invariant` is true, and in the page's where it is not. A char's culture
 * handed as null is refused, as .NET refuses it, where a string's is read as the current one, which
 * the call passes as none.
 */
export function toUpper(text: string, invariant?: boolean | null): string {
  if (invariant === null) throw exception('System.ArgumentNullException', NULL_CULTURE);
  const casing = casingOf(invariant);
  return APART.upper[casing].test(text)
    ? changed(text, (codePoint) => upperOf(codePoint, casing))
    : text.toUpperCase();
}

/** `string.ToLower()`, `ToLower(culture)` and `ToLowerInvariant()`, and `char.ToLower`'s three, as
 * {@link toUpper} reads its culture. */
export function toLower(text: string, invariant?: boolean | null): string {
  if (invariant === null) throw exception('System.ArgumentNullException', NULL_CULTURE);
  const casing = casingOf(invariant);
  return APART.lower[casing].test(text)
    ? changed(text, (codePoint) => lowerOf(codePoint, casing))
    : text.toLowerCase();
}
