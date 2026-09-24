/**
 * The text of a number, read as .NET's parser reads it (`Number.TryParseNumber`), in the invariant
 * culture the twin always parses in (EQ2110 names what a culture changes): `.` and `,` for the
 * decimal point and the thousands, `+` and `-` for the signs, `¤` for the currency symbol.
 *
 * Which characters may appear where is decided by the `NumberStyles` the call passes, whose flags
 * arrive here as the number a C# `NumberStyles` holds. The whitespace is .NET's own set (space and
 * `\t` to `\r`, not JavaScript's `trim()`, which also takes a no-break space); a sign sits before
 * the digits with nothing between, or after them, but never both; a thousands separator counts only
 * after a digit and before the point; and the text may end in `\0` characters, which .NET ignores.
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
  /** `NumberStyles.Number`, the style `decimal.Parse` reads when the call names none. */
  Number: 111,
} as const;

/**
 * What a number's text says before a type holds it: the sign, the significant digits from the
 * first that is not zero (every one of them, trailing zeros included), and where the point falls
 * among them, so that the number is `0.digits × 10^scale`. `0.050` is `{ digits: '50', scale: -1 }`.
 */
export interface NumberText {
  negative: boolean;
  digits: string;
  scale: number;
}

const SIGN = 0x01;
const PARENS = 0x02;
const DIGITS = 0x04;
const NON_ZERO = 0x08;
const DECIMAL = 0x10;
const CURRENCY = 0x20;

const PLUS = 0x2b;
const MINUS = 0x2d;
const POINT = 0x2e;
const COMMA = 0x2c;
const OPEN = 0x28;
const CLOSE = 0x29;
const CURRENCY_SYMBOL = 0xa4;
const ZERO = 0x30;

/** Past this many exponent digits .NET stops counting and makes the exponent too large to hold. */
const EXPONENT_LIMIT = 100_000_000;
const INT_MAX = 2_147_483_647;

/** .NET's whitespace for a number: space, and `\t` through `\r`. */
function isWhite(ch: number): boolean {
  return ch === 0x20 || (ch >= 0x09 && ch <= 0x0d);
}

function isDigit(ch: number): boolean {
  return ch >= ZERO && ch <= ZERO + 9;
}

/**
 * The number `text` holds under `styles`, or `undefined` where .NET's parser fails it (a
 * FormatException). The walk is .NET's, state for state, for the kind a decimal parses into: the
 * scale and the sign of a zero are kept, since a decimal zero carries both.
 */
export function readNumber(text: string, styles: number): NumberText | undefined {
  const end = text.length;
  const at = (index: number): number => (index < end ? text.charCodeAt(index) : 0);
  let currencySymbol = (styles & NumberStyles.AllowCurrencySymbol) !== 0;
  let state = 0;
  let negative = false;
  let p = 0;
  let ch = at(p);

  // Before the digits: whitespace, one sign or an opening parenthesis, one currency symbol. After a
  // sign, whitespace only once a currency symbol has come: "-¤ 5" is a number, "- 5" is not.
  for (;;) {
    if (
      !isWhite(ch) ||
      (styles & NumberStyles.AllowLeadingWhite) === 0 ||
      ((state & SIGN) !== 0 && (state & CURRENCY) === 0)
    ) {
      if (
        (styles & NumberStyles.AllowLeadingSign) !== 0 &&
        (state & SIGN) === 0 &&
        (ch === PLUS || ch === MINUS)
      ) {
        state |= SIGN;
        if (ch === MINUS) negative = true;
      } else if (
        ch === OPEN &&
        (styles & NumberStyles.AllowParentheses) !== 0 &&
        (state & SIGN) === 0
      ) {
        state |= SIGN | PARENS;
        negative = true;
      } else if (currencySymbol && ch === CURRENCY_SYMBOL) {
        state |= CURRENCY;
        currencySymbol = false;
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
      ch === POINT
    ) {
      state |= DECIMAL;
    } else if (
      (styles & NumberStyles.AllowThousands) !== 0 &&
      (state & DIGITS) !== 0 &&
      (state & DECIMAL) === 0 &&
      ch === COMMA
    ) {
      // A separator between digits of the integer part is read past, as many as there are.
    } else {
      break;
    }
    ch = at(++p);
  }

  if ((state & DIGITS) === 0) return undefined;

  // The exponent: an `e` with no digit after it (and after its sign) is not one, and is left for
  // the trailing part to refuse.
  if ((ch === 0x45 || ch === 0x65) && (styles & NumberStyles.AllowExponent) !== 0) {
    const mark = p;
    ch = at(++p);
    let negativeExponent = false;
    if (ch === PLUS) {
      ch = at(++p);
    } else if (ch === MINUS) {
      ch = at(++p);
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
        (ch === PLUS || ch === MINUS)
      ) {
        state |= SIGN;
        if (ch === MINUS) negative = true;
      } else if (ch === CLOSE && (state & PARENS) !== 0) {
        state &= ~PARENS;
      } else if (currencySymbol && ch === CURRENCY_SYMBOL) {
        currencySymbol = false;
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
  return { negative, digits, scale };
}
