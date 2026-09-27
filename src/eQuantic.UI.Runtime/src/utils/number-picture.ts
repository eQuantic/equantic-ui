/**
 * A CUSTOM numeric format — a picture of the number such as `#,##0.00;(#,##0.00);'none'` — drawn
 * as .NET draws it: `NumberToStringFormat` in .NET's `Number.Formatting.cs`, ported case for case,
 * because every quirk of it is observable in a formatted number and a reimplementation that
 * reasoned its own way to the common cases would disagree on the rest.
 *
 * What a picture holds: `0` and `#` digit places, one `.` for the point, `,` between digit places
 * for groups and just before the point to divide by a thousand, up to three `;` sections (positive,
 * negative, zero), `%` and `‰`, which scale the value by 100 and 1000, an exponent (`E0`, `E+00`,
 * `e-0`), quoted text and `\`-escaped characters, and anything else written as it stands. The
 * formatter used to read the digit places and nothing else, so `(0.25).ToString("0%")` printed `0`
 * where .NET prints `25%`, and `"0.00;(0.00)"` wrote a negative number with a minus sign (#393).
 */

/** The number a picture draws: significant digits with no leading or trailing zeros (none at all
 * for zero) and where the point goes, `0.digits × 10^scale`, which is .NET's `NumberBuffer`. */
export interface PictureNumber {
  readonly negative: boolean;
  readonly digits: string;
  readonly scale: number;
  /** A double's or a float's, whose zero keeps its sign: `(-0.4).ToString("0")` is `-0`, where a
   * decimal's and an integer's zero has none. */
  readonly floating: boolean;
}

/** The culture's symbols a picture writes, as .NET's `NumberFormatInfo` holds them. */
export interface PictureSymbols {
  readonly decimal: string;
  readonly group: string;
  /** The digits in each group, the one next to the point first; the last one repeats (`[3]`, and
   * en-IN's `[3, 2]`, which writes `12,34,567`). */
  readonly groupSizes: readonly number[];
  readonly minus: string;
  readonly plus: string;
  readonly percent: string;
  readonly perMille: string;
}

/** .NET's "no zero placeholder yet" marker, `0x7FFFFFFF`. */
const NONE = 0x7fffffff;

/**
 * Where a section starts: 0 for the first, or the index after the `;` that opens the one asked
 * for — 1 for negative numbers, 2 for zero — when the picture has it and it is not empty. A section
 * that is absent or empty falls back to the first. .NET's `FindSection`.
 */
function findSection(format: string, section: number): number {
  if (section === 0) return 0;
  let src = 0;
  for (;;) {
    if (src >= format.length) return 0;
    const ch = format[src++];
    switch (ch) {
      case "'":
      case '"':
        while (src < format.length && format[src] !== '\0') if (format[src++] === ch) break;
        break;
      case '\\':
        if (src < format.length && format[src] !== '\0') src++;
        break;
      case ';':
        if (--section !== 0) break;
        return src < format.length && format[src] !== '\0' && format[src] !== ';' ? src : 0;
      case '\0':
        return 0;
    }
  }
}

/**
 * Rounds the digits at `pos` places from the first, half away from zero: a picture rounds its
 * digits by the digit alone, .NET's `RoundNumber` for a custom format. A zero loses its sign unless
 * it is a double's or a float's.
 */
function roundDigits(number: PictureNumber, pos: number): PictureNumber {
  const dig = number.digits;
  let i = Math.max(0, Math.min(pos, dig.length));
  let digits: string;
  let scale = number.scale;
  if (i === pos && i < dig.length && dig.charCodeAt(i) >= 53 /* '5' */) {
    while (i > 0 && dig[i - 1] === '9') i--;
    if (i > 0) {
      digits = dig.slice(0, i - 1) + String.fromCharCode(dig.charCodeAt(i - 1) + 1);
    } else {
      scale++;
      digits = '1';
    }
  } else {
    while (i > 0 && dig[i - 1] === '0') i--;
    digits = dig.slice(0, i);
  }
  if (digits.length > 0) return { ...number, digits, scale };
  return { ...number, digits, scale: 0, negative: number.negative && number.floating };
}

/** The exponent a picture writes: its marker, a sign (a plus only where the picture asks for one),
 * and at least `minDigits` digits. .NET's `FormatExponent`. */
function exponentText(
  info: PictureSymbols,
  value: number,
  marker: string,
  minDigits: number,
  positiveSign: boolean,
): string {
  const sign = value < 0 ? info.minus : positiveSign ? info.plus : '';
  return marker + sign + String(Math.abs(value)).padStart(minDigits, '0');
}

/** Draws `number` through the custom numeric format `format`, in the culture `info` describes. */
export function drawPicture(value: PictureNumber, format: string, info: PictureSymbols): string {
  let number = value;
  let section = findSection(format, number.digits.length === 0 ? 2 : number.negative ? 1 : 0);

  // The first pass reads the section: how many digit places, where the point is, the first and
  // last zero placeholder, the groups, the scaling, and whether it writes an exponent.
  let digitCount: number;
  let decimalPos: number;
  let firstDigit: number;
  let lastDigit: number;
  let scientific: boolean;
  let thousandPos: number;
  let thousandCount = 0;
  let thousandSeps: boolean;
  let scaleAdjust: number;
  for (;;) {
    digitCount = 0;
    decimalPos = -1;
    firstDigit = NONE;
    lastDigit = 0;
    scientific = false;
    thousandPos = -1;
    thousandSeps = false;
    scaleAdjust = 0;
    let src = section;
    while (src < format.length) {
      const ch = format[src++];
      if (ch === '\0' || ch === ';') break;
      switch (ch) {
        case '#':
          digitCount++;
          break;
        case '0':
          if (firstDigit === NONE) firstDigit = digitCount;
          digitCount++;
          lastDigit = digitCount;
          break;
        case '.':
          if (decimalPos < 0) decimalPos = digitCount;
          break;
        case ',':
          if (digitCount > 0 && decimalPos < 0) {
            if (thousandPos >= 0) {
              if (thousandPos === digitCount) {
                thousandCount++;
                break;
              }
              thousandSeps = true;
            }
            thousandPos = digitCount;
            thousandCount = 1;
          }
          break;
        case '%':
          scaleAdjust += 2;
          break;
        case '‰':
          scaleAdjust += 3;
          break;
        case "'":
        case '"':
          while (src < format.length && format[src] !== '\0') if (format[src++] === ch) break;
          break;
        case '\\':
          if (src < format.length && format[src] !== '\0') src++;
          break;
        case 'E':
        case 'e':
          if (
            (src < format.length && format[src] === '0') ||
            (src + 1 < format.length &&
              (format[src] === '+' || format[src] === '-') &&
              format[src + 1] === '0')
          ) {
            do src++;
            while (src < format.length && format[src] === '0');
            scientific = true;
          }
          break;
      }
    }

    if (decimalPos < 0) decimalPos = digitCount;
    if (thousandPos >= 0) {
      // Commas right before the point divide by a thousand each; anywhere else they group.
      if (thousandPos === decimalPos) scaleAdjust -= thousandCount * 3;
      else thousandSeps = true;
    }

    if (number.digits.length > 0) {
      number = { ...number, scale: number.scale + scaleAdjust };
      const pos = scientific ? digitCount : number.scale + digitCount - decimalPos;
      number = roundDigits(number, pos);
      // A value that rounds to zero is written by the zero section, when the picture has one.
      if (number.digits.length === 0) {
        const zero = findSection(format, 2);
        if (zero !== section) {
          section = zero;
          continue;
        }
      }
    } else {
      number = { ...number, scale: 0, negative: number.negative && number.floating };
    }
    break;
  }

  firstDigit = firstDigit < decimalPos ? decimalPos - firstDigit : 0;
  lastDigit = lastDigit > decimalPos ? decimalPos - lastDigit : 0;
  let digPos: number;
  let adjust: number;
  if (scientific) {
    digPos = decimalPos;
    adjust = 0;
  } else {
    digPos = number.scale > decimalPos ? number.scale : decimalPos;
    // How many digits the value has past the picture's places (positive) or short of them.
    adjust = number.scale - decimalPos;
  }

  // Where the group separators go, counted in digits from the point.
  const separators: number[] = [];
  if (thousandSeps && info.group.length > 0) {
    const sizes = info.groupSizes;
    let index = 0;
    let total = sizes.length !== 0 ? sizes[0] : 0;
    let size = total;
    const totalDigits = digPos + (adjust < 0 ? adjust : 0);
    const numDigits = firstDigit > totalDigits ? firstDigit : totalDigits;
    while (numDigits > total && size !== 0) {
      separators.push(total);
      if (index < sizes.length - 1) size = sizes[++index];
      total += size;
    }
  }
  let separator = separators.length - 1;

  const dig = number.digits;
  let cur = 0;
  let out = number.negative && section === 0 && number.scale !== 0 ? info.minus : '';
  const grouped = (): void => {
    if (thousandSeps && digPos > 1 && separator >= 0 && digPos === separators[separator] + 1) {
      out += info.group;
      separator--;
    }
  };

  // The second pass writes the section.
  let decimalWritten = false;
  let src = section;
  while (src < format.length) {
    const ch = format[src++];
    if (ch === '\0' || ch === ';') break;
    // The digits the value has past the picture's places are all written at its first one.
    if (adjust > 0 && (ch === '#' || ch === '0' || ch === '.')) {
      while (adjust > 0) {
        out += cur < dig.length ? dig[cur++] : '0';
        grouped();
        digPos--;
        adjust--;
      }
    }
    switch (ch) {
      case '#':
      case '0': {
        let written: string;
        if (adjust < 0) {
          adjust++;
          written = digPos <= firstDigit ? '0' : '';
        } else {
          written = cur < dig.length ? dig[cur++] : digPos > lastDigit ? '0' : '';
        }
        if (written !== '') {
          out += written;
          grouped();
        }
        digPos--;
        break;
      }
      case '.':
        // Written once, and only where a digit follows it or the picture pads one.
        if (digPos !== 0 || decimalWritten) break;
        if (lastDigit < 0 || (decimalPos < digitCount && cur < dig.length)) {
          out += info.decimal;
          decimalWritten = true;
        }
        break;
      case '‰':
        out += info.perMille;
        break;
      case '%':
        out += info.percent;
        break;
      case ',':
        break;
      case "'":
      case '"':
        while (src < format.length && format[src] !== '\0' && format[src] !== ch)
          out += format[src++];
        if (src < format.length && format[src] !== '\0') src++;
        break;
      case '\\':
        if (src < format.length && format[src] !== '\0') out += format[src++];
        break;
      case 'E':
      case 'e': {
        if (!scientific) {
          // Not an exponent (or a second one): the marker and what follows it are text.
          out += ch;
          if (src < format.length) {
            if (format[src] === '+' || format[src] === '-') out += format[src++];
            while (src < format.length && format[src] === '0') out += format[src++];
          }
          break;
        }
        let positiveSign = false;
        let minDigits = 0;
        if (src < format.length && format[src] === '0') minDigits++;
        else if (src + 1 < format.length && format[src] === '+' && format[src + 1] === '0')
          positiveSign = true;
        else if (!(src + 1 < format.length && format[src] === '-' && format[src + 1] === '0')) {
          out += ch;
          break;
        }
        for (src++; src < format.length && format[src] === '0'; src++) minDigits++;
        const exponent = dig.length === 0 ? 0 : number.scale - decimalPos;
        out += exponentText(info, exponent, ch, Math.min(minDigits, 10), positiveSign);
        scientific = false;
        break;
      }
      default:
        out += ch;
    }
  }

  if (number.negative && section === 0 && number.scale === 0 && out.length > 0)
    out = info.minus + out;
  return out;
}
