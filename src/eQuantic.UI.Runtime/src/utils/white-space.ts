/**
 * White space as .NET reads it: what `char.IsWhiteSpace` answers true for, and what `string.Trim`,
 * its two halves and `string.IsNullOrWhiteSpace` take as white.
 *
 * That is U+0009 to U+000D, U+0085 NEXT LINE, and every separator (Zs, Zl and Zp): U+0020, U+00A0,
 * U+1680, U+2000 to U+200A, U+2028, U+2029, U+202F, U+205F and U+3000. JavaScript's `\s` and
 * `trim` are a different set: they leave U+0085, which .NET counts, and take U+FEFF, which .NET does
 * not (it is a format character), so a twin that split or trimmed through them answered its C#
 * differently wherever either appeared. The list is written out rather than read from the engine's
 * Unicode tables, which differ between engines and versions; the separators have not changed since
 * Unicode 6.3 took U+180E out of them, and the conformance suite compares all 65,536 code units
 * against .NET's own answer.
 */
function isWhiteUnit(unit: number): boolean {
  if (unit <= 0x20) return unit === 0x20 || (unit >= 0x09 && unit <= 0x0d);
  if (unit < 0x85) return false;
  if (unit <= 0xa0) return unit === 0x85 || unit === 0xa0;
  if (unit < 0x1680) return false;
  return (
    unit === 0x1680 ||
    (unit >= 0x2000 && unit <= 0x200a) ||
    unit === 0x2028 ||
    unit === 0x2029 ||
    unit === 0x202f ||
    unit === 0x205f ||
    unit === 0x3000
  );
}

/** `char.IsWhiteSpace`: one character, which a surrogate pair (the `(string, int)` overload's
 * answer at a pair) never is. */
export function isWhiteSpace(character: string): boolean {
  return character.length === 1 && isWhiteUnit(character.charCodeAt(0));
}

/**
 * The characters a `Trim(chars)` takes off: one char, which is a string of one here, or the array of
 * them. A null or an empty set is .NET's white space, as `"  a ".Trim(new char[0])` trims it.
 */
export type TrimChars = string | readonly string[] | null | undefined;

/** Whether the unit at `index` is one `chars` takes off. */
function trimmer(chars: TrimChars): (value: string, index: number) => boolean {
  if (chars == null || chars.length === 0) {
    return (value, index) => isWhiteUnit(value.charCodeAt(index));
  }
  return (value, index) => chars.includes(value[index]);
}

/** `string.TrimStart()` and `TrimStart(chars)`. A loop, not a pattern: a pattern anchored at the end
 * backtracks over every run of white space it meets, which is quadratic in a long line of them. */
export function trimStart(value: string, chars?: TrimChars): string {
  const takes = trimmer(chars);
  let start = 0;
  while (start < value.length && takes(value, start)) start++;
  return start === 0 ? value : value.slice(start);
}

/** `string.TrimEnd()` and `TrimEnd(chars)`. */
export function trimEnd(value: string, chars?: TrimChars): string {
  const takes = trimmer(chars);
  let end = value.length;
  while (end > 0 && takes(value, end - 1)) end--;
  return end === value.length ? value : value.slice(0, end);
}

/**
 * `string.Trim()` and `Trim(chars)`. The string and the characters arrive as arguments, each
 * evaluated once where C# evaluates it, the receiver first: the transpiler wrote this one as an
 * arrow around the characters' C#, which an `await` among them could not parse in.
 */
export function trim(value: string, chars?: TrimChars): string {
  return trimEnd(trimStart(value, chars), chars);
}

/** `string.Split()` with no separator: every white space character is one, and the empty entries
 * between two of them stay, as .NET keeps them. */
export function splitOnWhiteSpace(value: string): string[] {
  const parts: string[] = [];
  let start = 0;
  for (let i = 0; i < value.length; i++) {
    if (!isWhiteUnit(value.charCodeAt(i))) continue;
    parts.push(value.slice(start, i));
    start = i + 1;
  }
  parts.push(value.slice(start));
  return parts;
}

declare const nonWhite: unique symbol;

/** A string that holds something besides white space: what a true `hasNonWhiteSpace` proves. */
export type NonWhiteSpaceText = string & { readonly [nonWhite]: true };

/**
 * Whether `value` holds something besides white space: `!string.IsNullOrWhiteSpace(value)`, which
 * the transpiler writes as `(!$eq.text.hasNonWhiteSpace(value))`, reading its argument once.
 *
 * A predicate for one side only, as C#'s `[NotNullWhen(false)]` is: a true answer proves a string
 * (the branch where IsNullOrWhiteSpace answered false), and a false one proves nothing, since the
 * value may be null or a string of white space. The branded type is what keeps the false branch
 * whole: TypeScript narrows a predicate's false branch by taking the predicate's type out, and no
 * plain `string` is one, so `x is null | undefined` on the other side would have read a white
 * `string` as `never` there, and refused valid code that reads it.
 */
export function hasNonWhiteSpace(value: string | null | undefined): value is NonWhiteSpaceText {
  if (value == null) return false;
  for (let i = 0; i < value.length; i++) if (!isWhiteUnit(value.charCodeAt(i))) return true;
  return false;
}
