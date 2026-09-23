/**
 * A character's general category, the way .NET's `CharUnicodeInfo.GetUnicodeCategory` and
 * `char.GetUnicodeCategory` answer it, as its `UnicodeCategory` member crosses: the member's name,
 * camel-cased, which is what the transpiled enum compares and casts from. The platform knows the
 * categories through its Unicode property escapes, so each is asked in the enum's own order. A code
 * point, a character, or a string and an index, where a surrogate pair is read whole and an index
 * outside the string throws, as .NET's does.
 */

const CATEGORIES: [string, RegExp][] = [
  ['uppercaseLetter', 'Lu'], ['lowercaseLetter', 'Ll'], ['titlecaseLetter', 'Lt'],
  ['modifierLetter', 'Lm'], ['otherLetter', 'Lo'], ['nonSpacingMark', 'Mn'],
  ['spacingCombiningMark', 'Mc'], ['enclosingMark', 'Me'], ['decimalDigitNumber', 'Nd'],
  ['letterNumber', 'Nl'], ['otherNumber', 'No'], ['spaceSeparator', 'Zs'], ['lineSeparator', 'Zl'],
  ['paragraphSeparator', 'Zp'], ['control', 'Cc'], ['format', 'Cf'], ['surrogate', 'Cs'],
  ['privateUse', 'Co'], ['connectorPunctuation', 'Pc'], ['dashPunctuation', 'Pd'],
  ['openPunctuation', 'Ps'], ['closePunctuation', 'Pe'], ['initialQuotePunctuation', 'Pi'],
  ['finalQuotePunctuation', 'Pf'], ['otherPunctuation', 'Po'], ['mathSymbol', 'Sm'],
  ['currencySymbol', 'Sc'], ['modifierSymbol', 'Sk'], ['otherSymbol', 'So'],
].map(([name, property]): [string, RegExp] => [name, new RegExp(`^\\p{${property}}$`, 'u')]);

export function unicodeCategory(value: number | string, index?: number): string {
  let character: string;
  if (typeof value === 'number') {
    character = String.fromCodePoint(value);
  } else if (index === undefined) {
    character = value;
  } else {
    if (index < 0 || index >= value.length) {
      throw new RangeError('Index was out of range. Must be non-negative and less than the size of the collection.');
    }
    character = String.fromCodePoint(value.codePointAt(index)!);
  }
  for (const [name, pattern] of CATEGORIES) {
    if (pattern.test(character)) return name;
  }
  return 'otherNotAssigned';
}
