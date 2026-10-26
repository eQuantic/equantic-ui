import { activeFormatData, INVARIANT_FORMAT, type NumberFormatData } from './culture';
import { exception } from './exceptions';

/**
 * The text of a number, read as .NET 10's parser reads it (`Number.TryParseNumber`), in the culture
 * the call names (#1087): the page's, which `Parse` and `TryParse` read with no provider, with the
 * current culture and with a null one, or the invariant one, which the compiler asks for where the
 * call names it. The culture gives the signs, the decimal point and the thousands separator, and the
 * currency symbol with its own point and separator; the invariant culture's are `+`, `-`, `.`, `,`
 * and `¤`.
 *
 * Which characters may appear where is decided by the `NumberStyles` the call passes, whose flags
 * arrive here as the number a C# `NumberStyles` holds. The whitespace is .NET's own set (space and
 * `\t` to `\r`, not JavaScript's `trim()`, which also takes a no-break space); a sign sits before
 * the digits with nothing between, or after them, but never both; a thousands separator counts only
 * after a digit and before the point; and the text may end in `\0` characters, which .NET ignores.
 * A culture's symbol is matched as .NET matches it: a no-break space in it (fr-FR's thousands) is
 * also read as a space, which is what a keyboard types, and a culture whose minus is one of the
 * dashes CLDR names (sv-SE's U+2212) also reads a hyphen as its minus.
 */
export const NumberStyles = {
  AllowLeadingWhite: 1,
  AllowTrailingWhite: 2,
  AllowLeadingSign: 4,
  AllowTrailingSign: 8,
  AllowParentheses: 16,
  AllowDecimalPoint: 32,
  AllowThousands: 64,
  AllowExponent: 128,
  AllowCurrencySymbol: 256,
  AllowHexSpecifier: 512,
  AllowBinarySpecifier: 1024,
  /** `NumberStyles.Integer`, the style an integer type reads when the call names none. */
  Integer: 7,
  /** `NumberStyles.Number`, the style `decimal.Parse` reads when the call names none. */
  Number: 111,
  /** `NumberStyles.Float`: `double.Parse` and `float.Parse` read it with `AllowThousands` beside. */
  Float: 167,
  /** `NumberStyles.HexNumber`: the hex specifier and whitespace around it, nothing else. */
  HexNumber: 515,
  /** `NumberStyles.BinaryNumber`: the binary specifier and whitespace around it, nothing else. */
  BinaryNumber: 1027,
} as const;

/** Every flag NumberStyles defines; a bit beyond them is not a style. */
const DEFINED_STYLES = 2047;
const UNDEFINED_STYLE = "An undefined NumberStyles value is being used. (Parameter 'style')";

/** `ArgumentNullException` for the text a Parse was handed, as .NET words it. */
export const NULL_TEXT = "Value cannot be null. (Parameter 's')";

/** The FormatException a Parse throws for text its grammar refuses, as .NET words it. */
export function badFormat(text: string): Error {
  return exception(
    'System.FormatException',
    `The input string '${text}' was not in a correct format.`,
  );
}

/**
 * `NumberFormatInfo.ValidateParseStyleInteger`: a flag NumberStyles does not define, or the hex or
 * the binary specifier beside anything but the whitespace around it, is an ArgumentException.
 */
export function validateIntegerStyles(styles: number): void {
  if (
    (styles &
      (~DEFINED_STYLES | NumberStyles.AllowHexSpecifier | NumberStyles.AllowBinarySpecifier)) !==
      0 &&
    (styles & ~NumberStyles.HexNumber) !== 0 &&
    (styles & ~NumberStyles.BinaryNumber) !== 0
  ) {
    throw exception(
      'System.ArgumentException',
      (styles & ~DEFINED_STYLES) !== 0
        ? UNDEFINED_STYLE
        : 'With the AllowHexSpecifier or AllowBinarySpecifier bit set in the enum bit field, the ' +
            'only other valid bits that can be combined into the enum value must be ' +
            "AllowLeadingWhite and AllowTrailingWhite. (Parameter 'style')",
    );
  }
}

/**
 * `NumberFormatInfo.ValidateParseStyleFloatingPoint`, which `decimal`, `double` and `float` share:
 * a flag NumberStyles does not define, or either specifier at all, is an ArgumentException.
 */
export function validateRealStyles(styles: number): void {
  if (
    (styles &
      (~DEFINED_STYLES | NumberStyles.AllowHexSpecifier | NumberStyles.AllowBinarySpecifier)) !==
    0
  ) {
    throw exception(
      'System.ArgumentException',
      (styles & ~DEFINED_STYLES) !== 0
        ? UNDEFINED_STYLE
        : 'The number styles AllowHexSpecifier and AllowBinarySpecifier are not supported on ' +
            "floating point data types. (Parameter 'style')",
    );
  }
}

/**
 * What a number's text says before a type holds it: the sign, the significant digits from the
 * first that is not zero (every one of them, trailing zeros included), and where the point falls
 * among them, so that the number is `0.digits × 10^scale`. `0.050` is `{ digits: '50', scale: -1 }`.
 * And whether a decimal point was read at all, because an integer read from a zero keeps the zero's
 * sign only when one was: .NET's `uint.Parse("-0.0", NumberStyles.Float)` overflows, and
 * `uint.Parse("-0", NumberStyles.Float)` is 0.
 */
export interface NumberText {
  negative: boolean;
  digits: string;
  scale: number;
  point: boolean;
}

const SIGN = 0x01;
const PARENS = 0x02;
const DIGITS = 0x04;
const NON_ZERO = 0x08;
const DECIMAL = 0x10;
const CURRENCY = 0x20;

const MINUS = 0x2d;
const OPEN = 0x28;
const CLOSE = 0x29;
const ZERO = 0x30;

/** Past this many exponent digits .NET stops counting and makes the exponent too large to hold. */
const EXPONENT_LIMIT = 100_000_000;
const INT_MAX = 2_147_483_647;

/** .NET's whitespace for a number: space, and `\t` through `\r`. */
export function isWhite(ch: number): boolean {
  return ch === 0x20 || (ch >= 0x09 && ch <= 0x0d);
}

/**
 * The number format a number is read in: the page's, or the invariant one where the call names it.
 * A page whose culture's data did not travel reads the invariant one, as its dates do.
 */
export function readingFormat(invariant = false): NumberFormatData {
  return invariant
    ? INVARIANT_FORMAT.numberFormat
    : (activeFormatData()?.numberFormat ?? INVARIANT_FORMAT.numberFormat);
}

/** The minus signs after which .NET also reads a hyphen as the minus
 * (`NumberFormatInfo.AllowHyphenDuringParsing`): the dashes CLDR's lenient parse data names. */
const HYPHEN_STAND_INS = new Set([
  '\u2012',
  '\u207b',
  '\u208b',
  '\u2212',
  '\u2796',
  '\ufe63',
  '\uff0d',
]);

/** Whether a culture whose minus is `negativeSign` reads a hyphen as its minus too. */
export function readsHyphenAsMinus(negativeSign: string): boolean {
  return negativeSign.length === 1 && HYPHEN_STAND_INS.has(negativeSign);
}

/**
 * Where `value` ends if it stands at `p` in `text`, or -1 (.NET's `MatchChars`): an empty value
 * matches nothing, and a no-break space or a narrow one in it is also matched by a space.
 */
export function matchChars(text: string, p: number, value: string): number {
  if (value.length === 0) return -1;
  for (let i = 0; i < value.length; i++, p++) {
    const ch = p < text.length ? text.charCodeAt(p) : 0;
    const want = value.charCodeAt(i);
    if (ch !== want && !((want === 0xa0 || want === 0x202f) && ch === 0x20)) return -1;
  }
  return p;
}

/** Where the culture's minus ends if it stands at `p`, or -1 (.NET's `MatchNegativeSignChars`). */
export function matchMinus(text: string, p: number, format: NumberFormatData): number {
  const end = matchChars(text, p, format.negativeSign);
  if (end >= 0) return end;
  return readsHyphenAsMinus(format.negativeSign) && text.charCodeAt(p) === MINUS ? p + 1 : -1;
}

function isDigit(ch: number): boolean {
  return ch >= ZERO && ch <= ZERO + 9;
}

/**
 * The number `text` holds under `styles` in `format`, or `undefined` where .NET's parser fails it
 * (a FormatException). The walk is .NET's, state for state, for the kind a decimal parses into: the
 * scale and the sign of a zero are kept, since a decimal zero carries both. With the currency symbol
 * allowed, the point and the separator are the currency's, and the number's are read too until the
 * symbol is met. White space after a leading sign is allowed only where the culture writes its minus
 * apart from the number (`NumberNegativePattern` 2) and the caller is not .NET's integer fast path,
 * which never allows it (`whiteAfterSign`).
 */
export function readNumber(
  text: string,
  styles: number,
  format: NumberFormatData = INVARIANT_FORMAT.numberFormat,
  whiteAfterSign: boolean = format.numberNegativePattern === 2,
): NumberText | undefined {
  const end = text.length;
  const at = (index: number): number => (index < end ? text.charCodeAt(index) : 0);
  const parsingCurrency = (styles & NumberStyles.AllowCurrencySymbol) !== 0;
  let currencySymbol = parsingCurrency ? format.currencySymbol : '';
  const point = parsingCurrency ? format.currencyDecimalSeparator : format.numberDecimalSeparator;
  const separator = parsingCurrency ? format.currencyGroupSeparator : format.numberGroupSeparator;
  let state = 0;
  let negative = false;
  let p = 0;
  let ch = at(p);
  let next: number;

  /** A sign at `p`: where it ends, and whether it was the minus; -1 where none stands there. */
  const sign = (): number => {
    const plus = matchChars(text, p, format.positiveSign);
    if (plus >= 0) return plus;
    const minus = matchMinus(text, p, format);
    if (minus >= 0) negative = true;
    return minus;
  };

  // Before the digits: whitespace, one sign or an opening parenthesis, one currency symbol. After a
  // sign, whitespace only once a currency symbol has come: "-¤ 5" is a number, "- 5" is not.
  for (;;) {
    if (
      !isWhite(ch) ||
      (styles & NumberStyles.AllowLeadingWhite) === 0 ||
      ((state & SIGN) !== 0 && (state & CURRENCY) === 0 && !whiteAfterSign)
    ) {
      if (
        (styles & NumberStyles.AllowLeadingSign) !== 0 &&
        (state & SIGN) === 0 &&
        (next = sign()) >= 0
      ) {
        state |= SIGN;
        p = next - 1;
      } else if (
        ch === OPEN &&
        (styles & NumberStyles.AllowParentheses) !== 0 &&
        (state & SIGN) === 0
      ) {
        state |= SIGN | PARENS;
        negative = true;
      } else if ((next = matchChars(text, p, currencySymbol)) >= 0) {
        state |= CURRENCY;
        currencySymbol = '';
        p = next - 1;
      } else {
        break;
      }
    }
    ch = at(++p);
  }

  // The digits, the point and the thousands separators. A zero before the first other digit is not
  // a significant digit: before the point it is nothing, after it it moves the point.
  let digits = '';
  let scale = 0;
  for (;;) {
    if (isDigit(ch)) {
      state |= DIGITS;
      if (ch !== ZERO || (state & NON_ZERO) !== 0) {
        digits += String.fromCharCode(ch);
        if ((state & DECIMAL) === 0) scale++;
        state |= NON_ZERO;
      } else if ((state & DECIMAL) !== 0) {
        scale--;
      }
    } else if (
      (styles & NumberStyles.AllowDecimalPoint) !== 0 &&
      (state & DECIMAL) === 0 &&
      ((next = matchChars(text, p, point)) >= 0 ||
        (parsingCurrency &&
          (state & CURRENCY) === 0 &&
          (next = matchChars(text, p, format.numberDecimalSeparator)) >= 0))
    ) {
      state |= DECIMAL;
      p = next - 1;
    } else if (
      (styles & NumberStyles.AllowThousands) !== 0 &&
      (state & DIGITS) !== 0 &&
      (state & DECIMAL) === 0 &&
      ((next = matchChars(text, p, separator)) >= 0 ||
        (parsingCurrency &&
          (state & CURRENCY) === 0 &&
          (next = matchChars(text, p, format.numberGroupSeparator)) >= 0))
    ) {
      // A separator between digits of the integer part is read past, as many as there are.
      p = next - 1;
    } else {
      break;
    }
    ch = at(++p);
  }

  if ((state & DIGITS) === 0) return undefined;

  // The exponent: an `e` with no digit after it (and after its sign) is not one, and is left for
  // the trailing part to refuse. Its sign is the culture's.
  if ((ch === 0x45 || ch === 0x65) && (styles & NumberStyles.AllowExponent) !== 0) {
    const mark = p;
    ch = at(++p);
    let negativeExponent = false;
    if ((next = matchChars(text, p, format.positiveSign)) >= 0) {
      ch = at((p = next));
    } else if ((next = matchMinus(text, p, format)) >= 0) {
      ch = at((p = next));
      negativeExponent = true;
    }
    if (isDigit(ch)) {
      let exponent = 0;
      do {
        if (exponent >= EXPONENT_LIMIT) {
          exponent = INT_MAX;
          scale = 0;
          while (isDigit(ch)) ch = at(++p);
          break;
        }
        exponent = exponent * 10 + (ch - ZERO);
        ch = at(++p);
      } while (isDigit(ch));
      scale += negativeExponent ? -exponent : exponent;
    } else {
      p = mark;
      ch = at(p);
    }
  }

  // After the digits: whitespace, a sign if none came before, the closing parenthesis, the
  // currency symbol if it has not appeared.
  for (;;) {
    if (!isWhite(ch) || (styles & NumberStyles.AllowTrailingWhite) === 0) {
      if (
        (styles & NumberStyles.AllowTrailingSign) !== 0 &&
        (state & SIGN) === 0 &&
        (next = sign()) >= 0
      ) {
        state |= SIGN;
        p = next - 1;
      } else if (ch === CLOSE && (state & PARENS) !== 0) {
        state &= ~PARENS;
      } else if ((next = matchChars(text, p, currencySymbol)) >= 0) {
        currencySymbol = '';
        p = next - 1;
      } else {
        break;
      }
    }
    ch = at(++p);
  }

  if ((state & PARENS) !== 0) return undefined;
  // What is left may only be `\0` characters.
  for (let index = p; index < end; index++) {
    if (text.charCodeAt(index) !== 0) return undefined;
  }
  return { negative, digits, scale, point: (state & DECIMAL) !== 0 };
}
