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

/** `string.TrimStart()`. A loop, not a pattern: a pattern anchored at the end backtracks over every
 * run of white space it meets, which is quadratic in a long line of them. */
export function trimStart(value: string): string {
  let start = 0;
  while (start < value.length && isWhiteUnit(value.charCodeAt(start))) start++;
  return start === 0 ? value : value.slice(start);
}

/** `string.TrimEnd()`. */
export function trimEnd(value: string): string {
  let end = value.length;
  while (end > 0 && isWhiteUnit(value.charCodeAt(end - 1))) end--;
  return end === value.length ? value : value.slice(0, end);
}

/** `string.Trim()`. */
export function trim(value: string): string {
  return trimEnd(trimStart(value));
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

/**
 * `string.IsNullOrWhiteSpace`, reading its argument once. A type predicate for what C# knows of it
 * (`[NotNullWhen(false)]`): past a false answer the value is a string, which the `!x || !x.trim()`
 * this replaced told TypeScript by its shape. The true branch reads as absent, stricter than C#'s
 * own "maybe null" there, which only a read C# already warns about can notice.
 */
export function isNullOrWhiteSpace(value: string | null | undefined): value is null | undefined {
  if (value == null) return true;
  for (let i = 0; i < value.length; i++) if (!isWhiteUnit(value.charCodeAt(i))) return false;
  return true;
}
