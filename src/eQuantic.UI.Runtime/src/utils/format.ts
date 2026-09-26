/**
 * C#-style formatting, resolved against the CULTURE the app is running in (Track L D7/D13).
 *
 * The rule this file exists to keep: a `{0:N2}` written in C# must read the same on the server and
 * in the browser. The server has real .NET; here there is `Intl`, and the two agree for a CLOSED,
 * TESTED subset — integers, fixed decimals, currency, percent, the standard date/time patterns —
 * which is exactly what the build-time diagnostic (EQ2100) allows through. Anything outside it is
 * refused where the developer can see it, never approximated at runtime.
 *
 * Every formatter reads the ACTIVE format culture. It used to pass `undefined`, which is "whatever
 * locale the browser is in" — so a pt-BR request rendered "1,234.50" on a US laptop and
 * "1.234,50" on a Brazilian one, from the same server response. The atom removes the question.
 */

import { double, single } from './real-text';
import { activeCurrency, activePattern, formatLocale } from './culture';

/**
 * .NET's InvariantCulture, as the closest thing Intl has to one: a "." decimal point and a ","
 * group separator, which is what en-US gives. There is no invariant LOCALE in Intl, so the
 * conversion is named here rather than guessed at six call sites.
 */
const INVARIANT_LOCALE = 'en-US';

/**
 * Depth rather than a flag: `format` can be reached again from inside a formatter, and a plain
 * boolean would be cleared by the inner call while the outer one still needed it. Safe without a
 * lock because nothing in this file awaits — a formatter runs to completion before anything else.
 */
let invariantDepth = 0;

/**
 * The locale every formatter in this file reads. It is the ACTIVE format culture, except while an
 * explicitly invariant conversion is being formatted — `value.ToString("0.##",
 * CultureInfo.InvariantCulture)`, which an author writes precisely so the number does NOT follow
 * whoever is reading it.
 */
function activeFormatLocale(): string | undefined {
  return invariantDepth > 0 ? INVARIANT_LOCALE : formatLocale();
}
import { DateTime as DotNetDateTime } from './datetime';
import { Decimal } from './decimal';
import {
  exactOfBigInt,
  exactOfDigits,
  exactOfDouble,
  exactOfScaled,
  fromSignificant,
  isZero,
  plainText,
  roundFraction,
  roundSignificant,
  scaled,
  type ExactDecimal,
  type Tie,
} from './exact-decimal';
import { drawPicture, type PictureNumber, type PictureSymbols } from './number-picture';

/**
 * The compat `DateTime` (tick-based, what `new DateTime(…)` transpiles to), as a native Date in
 * LOCAL parts — so one formatter serves both shapes. Without this, `{Moment:d}` over a compat
 * value fell through to `String(value)`, which is the INVARIANT default: the one date format on
 * the page that ignored the culture was the one written most naturally.
 */
function asJsDate(value: unknown): Date | null {
  if (value instanceof Date) return value;
  if (value instanceof DotNetDateTime)
    return new Date(
      value.year,
      value.month - 1,
      value.day,
      value.hour,
      value.minute,
      value.second,
      value.millisecond,
    );
  return null;
}

/** Which C# number a JavaScript number stands for, by the name of its .NET type. A number cannot
 * say it is a single or an int, so whoever knows passes it: the compiler, from the static type, at
 * every call it writes. A float's digits are not the double's (#378), and an integer rounds a
 * formatted half away from zero where a double rounds it to even (#393). A long and a decimal say
 * what they are on their own, as a BigInt and a Decimal. */
export type NumberKind = 'double' | 'single' | IntegerKind;

/** An integer that travels as a JavaScript number. Its width is what `X` and `B` write a negative
 * one at, as its two's complement: `((short)-1).ToString("X")` is `FFFF`, and an int's is
 * `FFFFFFFF` (#445). An unsigned one is never negative, and its bits are its digits. */
export type IntegerKind = 'sbyte' | 'byte' | 'int16' | 'uint16' | 'int32' | 'uint32';

const INTEGER_BITS: Readonly<Record<IntegerKind, number>> = {
  sbyte: 8,
  byte: 8,
  int16: 16,
  uint16: 16,
  int32: 32,
  uint32: 32,
};

function isInteger(kind: NumberKind): kind is IntegerKind {
  return Object.prototype.hasOwnProperty.call(INTEGER_BITS, kind);
}

/**
 * A float or an integer on its way into `string.Format`, whose arguments are objects in C#: boxed
 * with its kind, which the formatter reads and nothing else ever sees. Anywhere else a boxed
 * number is the plain number, and its kind is lost (#378).
 */
export class FormatNumber {
  constructor(
    readonly value: number,
    readonly kind: NumberKind,
  ) {}
}

/** Boxes a float for `string.Format`; null stays null. */
export function asSingle(value: number | null | undefined): FormatNumber | null | undefined {
  return value == null ? value : new FormatNumber(value, 'single');
}

/** Boxes an integer of the given kind for `string.Format`; null stays null. */
export function asInteger(
  value: number | null | undefined,
  kind: IntegerKind,
): FormatNumber | null | undefined {
  return value == null ? value : new FormatNumber(value, kind);
}

/**
 * @param value The value to format
 * @param format The format string (e.g. "C2", "N0", "yyyy-MM-dd")
 * @param alignment Optional alignment width
 * @param invariant Format against the INVARIANT culture rather than the active one — what
 *   `ToString(CultureInfo.InvariantCulture)` asks for, and the shape a number written for a
 *   machine (a CSS length, a key, a wire value) has to keep whoever is reading the page.
 * @param kind A number's binary kind, when it is a single: the shortest digits that read back as
 *   a float are not those of the double underneath (0.1f is 0.1, not 0.10000000149011612).
 */
export function format(
  value: any,
  format: string | null,
  alignment?: number,
  invariant?: boolean,
  kind?: NumberKind,
): string {
  if (value instanceof FormatNumber) {
    kind = value.kind;
    value = value.value;
  }
  // Null writes nothing, and nothing is still aligned: `$"[{n,4}]"` is "[    ]" for a null n.
  if (value === null || value === undefined) return pad('', alignment);
  if (invariant) invariantDepth++;
  try {
    return formatCore(value, format, alignment, kind ?? 'double');
  } finally {
    if (invariant) invariantDepth--;
  }
}

function formatCore(
  value: any,
  format: string | null,
  alignment: number | undefined,
  kind: NumberKind,
): string {
  // .NET spells a bool `True`/`False`, where JavaScript lowercases it, and writes a number with
  // its own notation (1E+17, -0), where String() keeps fixed notation up to 1e21.
  let result =
    typeof value === 'boolean'
      ? value
        ? 'True'
        : 'False'
      : typeof value === 'number'
        ? kind === 'single'
          ? single(value)
          : double(value)
        : String(value);

  if (format) {
    const date = asJsDate(value);
    if (typeof value === 'number' || typeof value === 'bigint' || value instanceof Decimal) {
      result = formatNumber(value, format, kind);
    } else if (date !== null) {
      result = formatDate(date, format);
    }
  }

  return pad(result, alignment);
}

/** Text in a field of `|alignment|` characters: a positive width aligns right, a negative left. */
function pad(result: string, alignment?: number): string {
  if (alignment) {
    const width = Math.abs(alignment);
    if (result.length < width) {
      const padding = ' '.repeat(width - result.length);
      return alignment > 0 ? padding + result : result + padding;
    }
  }

  return result;
}

/**
 * The number the formatter was handed, as what writing it takes: its exact digits, how a half
 * rounds, and whether a zero keeps its sign.
 */
interface Numeric {
  readonly exact: ExactDecimal;
  readonly tie: Tie;
  /** A double's and a float's zero keeps its sign (`-0.00`, and so does a negative value that
   * rounds to zero); a decimal's and an integer's does not (measured, #393). */
  readonly signedZero: boolean;
  /** How many significant digits .NET keeps before it draws a custom picture: 15 of a double's
   * and 7 of a float's, then the picture rounds those half away from zero. A decimal and an integer
   * are drawn from every digit they have. */
  readonly pictureDigits: number | null;
}

function numeric(value: number | bigint | Decimal, kind: NumberKind): Numeric {
  if (typeof value === 'bigint')
    return {
      exact: exactOfBigInt(value),
      tie: 'halfExpand',
      signedZero: false,
      pictureDigits: null,
    };
  if (value instanceof Decimal) {
    return {
      exact: exactOfScaled(value.mantissa, value.scale),
      tie: 'halfExpand',
      signedZero: false,
      pictureDigits: null,
    };
  }
  const integer = isInteger(kind);
  return {
    exact: exactOfDouble(value),
    tie: integer ? 'halfExpand' : 'halfEven',
    signedZero: !integer,
    pictureDigits: integer ? null : kind === 'single' ? 7 : 15,
  };
}

/**
 * `Intl.NumberFormat`'s options as NumberFormat v3 takes them (ES2023): a rounding mode, and a sign
 * shown for negative numbers but not for a zero. The formatter needs v3 for those and for reading a
 * STRING as an exact decimal (`format('9007199254740993')` keeps its last digit, where a number
 * would round to `…992`): V8 10.6 (Chrome and Edge 106), SpiderMonkey (Firefox 116), and
 * JavaScriptCore (Safari 15.4, Bun) have it, and the conformance suite runs on the last.
 */
type ExactOptions = Intl.NumberFormatOptions & { roundingMode?: Tie };

/** The digits `Intl` writes after the point at most; .NET takes a precision up to 999,999,999. */
const INTL_FRACTION_LIMIT = 100;

/** .NET's FormatException message for a specifier the type does not take. */
const BAD_SPECIFIER = 'Format specifier was invalid.';

const formatters = new Map<string, Intl.NumberFormat>();

/** The `Intl.NumberFormat` for these options in the locale in force, built once: building one
 * costs far more than formatting with it, and a page formats the same few shapes over and over. */
function numberFormat(options: ExactOptions): Intl.NumberFormat {
  const locale = activeFormatLocale();
  const key = `${locale ?? ''}|${JSON.stringify(options)}`;
  let formatter = formatters.get(key);
  if (formatter === undefined) {
    formatter = new Intl.NumberFormat(locale, options);
    formatters.set(key, formatter);
  }
  return formatter;
}

/**
 * Formats a number from its exact digits. `Intl` reads a STRING digit for digit where it would
 * read a number as its shortest text, so `(0.1).ToString("F20")` keeps the `555` .NET writes, and
 * it rounds by the tie rule of the value's type. The scale-and-round this replaced turned 0.265
 * into 0.26 under `F2`, where the double just above the half is 0.27, and threw past 15 digits.
 * `value` is what is written, when that is not the number itself (the invariant percent).
 */
function exactly(
  number: Numeric,
  options: ExactOptions,
  value: ExactDecimal = number.exact,
): string {
  const settings: ExactOptions = {
    ...options,
    roundingMode: number.tie,
    signDisplay: number.signedZero ? 'auto' : 'negative',
    // .NET groups every number longer than a group, where `Intl` leaves a four-digit one alone in
    // the cultures whose data asks for two digits in the first group: es-ES writes 1.234 (#445).
    useGrouping: options.useGrouping === false ? false : 'always',
  };
  const places = options.maximumFractionDigits ?? 0;
  if (places > INTL_FRACTION_LIMIT) return pastIntlsDigits(number, settings, value, places);
  return numberFormat(settings).format(plainText(value) as Intl.StringNumericLiteral);
}

/**
 * A precision past the digits `Intl` writes after the point: `(0.1).ToString("F101")` is legal
 * and exact in .NET, and it was cut to 100 digits here. The value is rounded here, by its type's
 * rule, and `Intl` lays out the rest: the sign, the groups, the currency or the percent, and the
 * separator the digits are written after.
 */
function pastIntlsDigits(
  number: Numeric,
  settings: ExactOptions,
  value: ExactDecimal,
  places: number,
): string {
  // A percent is rounded where it is written, after the times 100 `Intl` would have applied.
  const percent = settings.style === 'percent';
  const rounded = roundFraction(percent ? scaled(value, 2) : value, places, number.tie);
  const text = plainText({ ...rounded, negative: false });
  const point = text.indexOf('.');
  const whole = point < 0 ? text : text.slice(0, point);
  const fraction = (point < 0 ? '' : text.slice(point + 1)).padEnd(places, '0');
  // One digit after the point stands in for them: a 5 keeps a value that is not zero from reading
  // as one, so its sign shows under either sign display, and a 0 lets a zero keep its type's rule.
  const stand = exactOfDigits(rounded.negative, whole + (isZero(rounded) ? '0' : '5'), -1);
  const parts = numberFormat({
    ...settings,
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  }).formatToParts(plainText(percent ? scaled(stand, -2) : stand) as Intl.StringNumericLiteral);
  return parts.map((part) => (part.type === 'fraction' ? fraction : part.value)).join('');
}

const symbolsByLocale = new Map<string, PictureSymbols>();

/** The culture's number symbols, as `Intl` writes them in the locale in force: the same CLDR data
 * .NET's `NumberFormatInfo` is built from. `Intl` has no per mille sign, and CLDR's is `‰` in every
 * culture but the Arabic-script ones. */
function symbols(): PictureSymbols {
  const locale = activeFormatLocale();
  const key = locale ?? '';
  let found = symbolsByLocale.get(key);
  if (found === undefined) {
    const part = (parts: Intl.NumberFormatPart[], type: string, fallback: string): string =>
      parts.find((candidate) => candidate.type === type)?.value ?? fallback;
    const grouped = new Intl.NumberFormat(locale, { useGrouping: 'always' }).formatToParts(
      -12345678901234567890n,
    );
    // The last run of digits is the group next to the point, and the one before it the size every
    // other group repeats: en-IN groups 3 then 2.
    const runs = grouped
      .filter((candidate) => candidate.type === 'integer')
      .map((run) => run.value.length);
    const primary = runs[runs.length - 1];
    const secondary = runs[runs.length - 2];
    found = {
      decimal: part(new Intl.NumberFormat(locale).formatToParts(1.5), 'decimal', '.'),
      group: part(grouped, 'group', ','),
      groupSizes: runs.length < 2 ? [] : primary === secondary ? [primary] : [primary, secondary],
      minus: part(grouped, 'minusSign', '-'),
      plus: part(
        new Intl.NumberFormat(locale, { signDisplay: 'always' }).formatToParts(1),
        'plusSign',
        '+',
      ),
      percent: part(
        new Intl.NumberFormat(locale, { style: 'percent' }).formatToParts(1),
        'percentSign',
        '%',
      ),
      perMille: '‰',
    };
    symbolsByLocale.set(key, found);
  }
  return found;
}

/** The sign a rounded number is written with: a zero keeps one only where its type does. */
function signOf(number: Numeric, minus: string): string {
  return number.exact.negative && (number.signedZero || !isZero(number.exact)) ? minus : '';
}

/**
 * A CUSTOM numeric format is a picture of the number — `0.0`, `#,##0.00;(#,##0.00)`, `0%` —
 * rather than one of the single-letter standard specifiers, and it is drawn as .NET draws it
 * (`drawPicture`). It used to fall through to `value.toString()`, so `$"{bytes / 1024.0:0.0} KB"`
 * reached the browser as `0.72265625 KB`.
 *
 * .NET draws a double's picture from its first 15 significant digits (a float's 7), and the
 * picture rounds those half away from zero: `(1.005).ToString("0.00")` is `1.01`, where `F2` of
 * the same double is `1.00`. A decimal and an integer are drawn from every digit they have.
 */
function formatCustomNumber(number: Numeric, format: string): string {
  const exact =
    number.pictureDigits === null
      ? number.exact
      : fromSignificant(roundSignificant(number.exact, number.pictureDigits, 'halfEven'));
  const digits = isZero(exact) ? '' : exact.digits;
  const picture: PictureNumber = {
    negative: exact.negative,
    digits,
    scale: digits.length === 0 ? 0 : digits.length + exact.exponent,
    floating: number.signedZero,
  };
  return drawPicture(picture, format, symbols());
}

/**
 * Currency, the one specifier that needs a fact the browser cannot derive: `Intl` wants an ISO
 * CODE and a locale alone does not carry one. The code rides in the culture catalog (the build
 * reads it from .NET's own `RegionInfo`), and when there is none — the invariant culture — .NET
 * prints the generic ¤ sign, so that is what this prints too rather than guessing a country. With
 * no precision, the currency's own digits apply, as .NET's culture takes them from the same ISO
 * data (a yen has none).
 */
function formatCurrency(number: Numeric, precision: number | null): string {
  // An invariant conversion has no currency of its own, whichever culture is reading.
  const currency = invariantDepth > 0 ? null : activeCurrency();
  const digits: ExactOptions =
    precision === null
      ? {}
      : { minimumFractionDigits: precision, maximumFractionDigits: precision };
  if (currency !== null) return exactly(number, { style: 'currency', currency, ...digits });

  // .NET's invariant currency pattern is "¤n" with the invariant number conventions.
  const text = exactly(
    number,
    precision === null ? { minimumFractionDigits: 2, maximumFractionDigits: 2 } : digits,
  );
  const { minus } = symbols();
  return text.startsWith(minus) ? `${minus}¤${text.slice(minus.length)}` : `¤${text}`;
}

/**
 * `E` and `e`: one digit, the point, `precision` more (six by default), then the exponent with its
 * sign and at least three digits — `1.23E+004`. The digits are the exact value's, rounded by the
 * type's tie rule, so `(1.25).ToString("E1")` is `1.2E+000` and `(1.25m).ToString("E1")` is
 * `1.3E+000`. It used to fall through to `toString()`, and `{0:E2}` passed EQ2100 to print
 * `12345` where .NET prints `1.23E+004` (#393).
 */
function scientific(number: Numeric, precision: number, marker: 'E' | 'e'): string {
  const rounded = roundSignificant(number.exact, precision + 1, number.tie);
  const { decimal, minus } = symbols();
  const mantissa = rounded.digits[0] + (precision > 0 ? decimal + rounded.digits.slice(1) : '');
  const exponent = rounded.scientific;
  const power = String(Math.abs(exponent)).padStart(3, '0');
  return `${signOf(number, minus)}${mantissa}${marker}${exponent < 0 ? minus : '+'}${power}`;
}

/**
 * `G` with a precision: that many significant digits, their trailing zeros dropped, in fixed
 * notation while the exponent lies between -5 and the precision and in scientific notation past
 * it, where the exponent takes at least two digits — `(12345.678m).ToString("G2")` is `1.2E+04`.
 * It was not modelled, and wrote the value as JavaScript prints it.
 */
function generalWithPrecision(number: Numeric, precision: number, marker: 'E' | 'e'): string {
  const rounded = roundSignificant(number.exact, precision, number.tie);
  const { decimal, minus } = symbols();
  const digits = rounded.digits.replace(/0+$/, '') || '0';
  const exponent = rounded.scientific;
  const sign = signOf(number, minus);
  if (exponent > -5 && exponent < precision) {
    const whole = exponent >= 0 ? digits.slice(0, exponent + 1).padEnd(exponent + 1, '0') : '0';
    const fraction =
      exponent >= 0 ? digits.slice(exponent + 1) : '0'.repeat(-exponent - 1) + digits;
    return sign + whole + (fraction.length > 0 ? decimal + fraction : '');
  }
  const mantissa = digits[0] + (digits.length > 1 ? decimal + digits.slice(1) : '');
  const power = String(Math.abs(exponent)).padStart(2, '0');
  return `${sign}${mantissa}${marker}${exponent < 0 ? minus : '+'}${power}`;
}

/**
 * A double's infinities and NaN are words, not digits: the culture's symbols (`∞`), which `Intl`
 * writes from the same data .NET reads, and the invariant culture's own `Infinity` and `NaN`
 * where no culture is in force.
 */
function nonFinite(value: number): string {
  if (invariantDepth > 0 || formatLocale() === undefined)
    return Number.isNaN(value) ? 'NaN' : value > 0 ? 'Infinity' : '-Infinity';
  return numberFormat({}).format(value);
}

/**
 * The shortest digits that read back as the value, in .NET's notation (1E+21, -0), with the
 * active culture's decimal separator and minus sign (sv-SE writes `−1,5` and `1E−05`, with the
 * minus its data spells): what `G` and `R` write, and a `string.Format` placeholder with no
 * specifier, since .NET formats one with the value's `ToString(provider)`.
 */
function shortest(value: number, kind: NumberKind): string {
  if (!Number.isFinite(value)) return nonFinite(value);
  const text = kind === 'single' ? single(value) : double(value);
  const { decimal, minus } = symbols();
  return text.replace('.', decimal).replace(/-/g, minus);
}

/** A long's or a decimal's text with no specifier: all of its digits, a decimal's scale kept
 * (`12.50m` is `12.50`), in the culture's decimal separator. */
function plainDigits(value: bigint | Decimal): string {
  const text = value.toString();
  const { decimal, minus } = symbols();
  return text.replace('.', decimal).replace('-', minus);
}

/** A standard specifier is ONE letter and an optional precision; anything else is a picture. */
const STANDARD = /^([A-Za-z])(\d*)$/;

/** The largest precision .NET takes; a larger one is a FormatException. */
const MAX_PRECISION = 999_999_999;

/**
 * Whether `D`, `X` and `B` can take the value: an integer's, never a decimal's, a float's or a
 * fraction's, for which .NET throws. A whole number whose kind nobody passed (a value typed as an
 * object, a generic) is taken as the integer it holds, since the type that would say otherwise did
 * not travel with it.
 */
function integral(value: number | bigint | Decimal, kind: NumberKind): boolean {
  if (typeof value === 'bigint') return true;
  if (value instanceof Decimal) return false;
  return kind !== 'single' && Number.isInteger(value);
}

/**
 * The bits `X` and `B` write: a negative integer as its two's complement at its type's width. A
 * number whose kind nobody passed is taken as an int, the integer that travels as a number most
 * often, and one below an int's range as a long, the one that can hold it.
 */
function bitsOf(value: number | bigint, kind: NumberKind): bigint {
  if (typeof value === 'bigint') return BigInt.asUintN(64, value);
  if (value >= 0) return BigInt(value);
  const width = isInteger(kind) ? INTEGER_BITS[kind] : value >= -0x80000000 ? 32 : 64;
  return BigInt.asUintN(width, BigInt(value));
}

function formatNumber(
  value: number | bigint | Decimal,
  format: string,
  kind: NumberKind = 'double',
): string {
  if (typeof value === 'number' && !Number.isFinite(value)) return nonFinite(value);
  const number = numeric(value, kind);

  // Anything that is not one letter and its precision is a custom picture, even with no digit
  // place in it: `(5).ToString("Total")` is `Total`.
  const standard = STANDARD.exec(format);
  if (standard === null) return formatCustomNumber(number, format);

  const letter = standard[1];
  const digits = standard[2];
  // Leading zeros are allowed (`F0002` is `F2`), and .NET refuses a precision past its limit.
  const specified = digits.length > 0 ? Number(digits) : null;
  if (specified !== null && specified > MAX_PRECISION) throw new Error(BAD_SPECIFIER);
  const precision = specified ?? 2;

  switch (letter.toUpperCase()) {
    case 'C': // Currency
      return formatCurrency(number, specified);
    case 'N': // Number — grouped, culture separators
      return exactly(number, {
        minimumFractionDigits: precision,
        maximumFractionDigits: precision,
      });
    case 'P': // Percentage — the exact value times 100, rounded where the percent is written
      // The invariant culture writes `n %`, with a space `Intl`'s nearest locale leaves out.
      if (invariantDepth > 0 || formatLocale() === undefined) {
        const percent = scaled(number.exact, 2);
        return `${exactly(number, { minimumFractionDigits: precision, maximumFractionDigits: precision }, percent)} %`;
      }
      return exactly(number, {
        style: 'percent',
        minimumFractionDigits: precision,
        maximumFractionDigits: precision,
      });
    case 'F': // Fixed point — culture decimal separator, NEVER grouped
      // `toFixed` was the old answer and it is invariant: a pt-BR page showed "1234.50" beside
      // numbers that used a comma everywhere else on the same line.
      return exactly(number, {
        minimumFractionDigits: precision,
        maximumFractionDigits: precision,
        useGrouping: false,
      });
    case 'E': // Scientific — six digits after the point unless told otherwise
      return scientific(number, specified ?? 6, letter === 'e' ? 'e' : 'E');
    case 'D': {
      // Decimal: an integer's digits, padded, and the culture's minus sign (sv-SE's is U+2212)
      if (!integral(value, kind)) throw new Error(BAD_SPECIFIER);
      const whole = BigInt(value as number | bigint);
      const magnitude = (whole < 0n ? -whole : whole).toString();
      return (whole < 0n ? symbols().minus : '') + magnitude.padStart(specified ?? 1, '0');
    }
    case 'X': // Hex, and B, binary (.NET 8): a negative integer's two's complement at its width
    case 'B': {
      if (!integral(value, kind)) throw new Error(BAD_SPECIFIER);
      const text = bitsOf(value as number | bigint, kind).toString(
        letter.toUpperCase() === 'X' ? 16 : 2,
      );
      return (letter === 'X' ? text.toUpperCase() : text).padStart(specified ?? 1, '0');
    }
    case 'R': // Round-trip, which .NET Core 3.0 made the shortest digits that read back
      return typeof value === 'number' ? shortest(value, kind) : plainDigits(value);
    case 'G': // General: the shortest text with no precision, that many significant digits with one
      if (specified === null || specified === 0)
        return typeof value === 'number' ? shortest(value, kind) : plainDigits(value);
      return generalWithPrecision(number, specified, letter === 'g' ? 'e' : 'E');
    default:
      // A letter no number takes (`Z2`) is .NET's FormatException, where it wrote the value.
      throw new Error(BAD_SPECIFIER);
  }
}

/**
 * The standard date/time specifiers .NET spells with ONE letter, as the culture PATTERN ROLES they
 * actually stand for — `d` is not "a short date" in the abstract, it is this culture's
 * ShortDatePattern. Two roles joined by a space is how .NET composes `g`/`G`/`f`/`F`.
 */
const DATE_ROLES: Record<string, string[]> = {
  d: ['dateShort'],
  D: ['dateLong'],
  t: ['timeShort'],
  T: ['timeLong'],
  g: ['dateShort', 'timeShort'],
  G: ['dateShort', 'timeLong'],
  f: ['dateLong', 'timeShort'],
  F: ['dateLong', 'timeLong'],
  M: ['monthDay'],
  m: ['monthDay'],
  Y: ['yearMonth'],
  y: ['yearMonth'],
};

/**
 * The invariant culture's date and time patterns, as .NET's `CultureInfo.InvariantCulture` holds
 * them. An explicitly invariant conversion writes these whatever culture is reading, as it writes
 * the invariant number conventions and the generic ¤ for a currency: read from the active culture,
 * `string.Format(CultureInfo.InvariantCulture, "{0:G}", date)` followed the reader's patterns.
 */
const INVARIANT_PATTERNS: Readonly<Record<string, string>> = {
  dateShort: 'MM/dd/yyyy',
  dateLong: 'dddd, dd MMMM yyyy',
  timeShort: 'HH:mm',
  timeLong: 'HH:mm:ss',
  monthDay: 'MMMM dd',
  yearMonth: 'yyyy MMMM',
};

/** A pattern role in the culture the formatter is writing in. */
function patternFor(role: string): string | null {
  return invariantDepth > 0 ? (INVARIANT_PATTERNS[role] ?? null) : activePattern(role);
}

/** `Intl`'s fallback for a culture whose patterns did not travel (no catalog installed). Close,
 * not exact — which is why the patterns travel at all. */
const DATE_STYLES: Record<string, Intl.DateTimeFormatOptions> = {
  d: { dateStyle: 'short' },
  D: { dateStyle: 'full' },
  t: { timeStyle: 'short' },
  T: { timeStyle: 'medium' },
  g: { dateStyle: 'short', timeStyle: 'short' },
  G: { dateStyle: 'short', timeStyle: 'medium' },
  f: { dateStyle: 'long', timeStyle: 'short' },
  F: { dateStyle: 'full', timeStyle: 'medium' },
  M: { month: 'long', day: 'numeric' },
  m: { month: 'long', day: 'numeric' },
  Y: { year: 'numeric', month: 'long' },
  y: { year: 'numeric', month: 'long' },
};

/** One `Intl` part, for the NAMES a pattern cannot spell — months, weekdays, the AM/PM designator.
 * `Intl` is exactly right about these, in every culture, which is why they are not in the patterns. */
function namePart(value: Date, options: Intl.DateTimeFormatOptions, type: string): string {
  const parts = new Intl.DateTimeFormat(activeFormatLocale(), options).formatToParts(value);
  return parts.find((part) => part.type === type)?.value ?? '';
}

/**
 * Renders a .NET date/time PATTERN. The token set is the one the standard patterns of real
 * cultures use; a literal in single quotes travels verbatim (pt-BR's long date is
 * `dddd, d 'de' MMMM 'de' yyyy`), and anything unrecognized is a literal too.
 */
function renderPattern(value: Date, pattern: string): string {
  const hours24 = value.getHours();
  const hours12 = hours24 % 12 === 0 ? 12 : hours24 % 12;
  const out: string[] = [];

  for (let i = 0; i < pattern.length; ) {
    const ch = pattern[i];

    if (ch === "'") {
      const close = pattern.indexOf("'", i + 1);
      if (close < 0) {
        out.push(pattern.slice(i + 1));
        break;
      }
      out.push(pattern.slice(i + 1, close));
      i = close + 1;
      continue;
    }
    if (ch === '\\' && i + 1 < pattern.length) {
      out.push(pattern[i + 1]);
      i += 2;
      continue;
    }

    let run = 1;
    while (i + run < pattern.length && pattern[i + run] === ch) run++;
    const token = ch.repeat(run);

    switch (token) {
      case 'dddd':
        out.push(namePart(value, { weekday: 'long' }, 'weekday'));
        break;
      case 'ddd':
        out.push(namePart(value, { weekday: 'short' }, 'weekday'));
        break;
      case 'dd':
        out.push(String(value.getDate()).padStart(2, '0'));
        break;
      case 'd':
        out.push(String(value.getDate()));
        break;
      case 'MMMM':
        out.push(namePart(value, { month: 'long' }, 'month'));
        break;
      case 'MMM':
        out.push(namePart(value, { month: 'short' }, 'month'));
        break;
      case 'MM':
        out.push(String(value.getMonth() + 1).padStart(2, '0'));
        break;
      case 'M':
        out.push(String(value.getMonth() + 1));
        break;
      case 'yyyy':
        out.push(String(value.getFullYear()).padStart(4, '0'));
        break;
      case 'yyy':
        out.push(String(value.getFullYear()).padStart(3, '0'));
        break;
      case 'yy':
        out.push(String(value.getFullYear() % 100).padStart(2, '0'));
        break;
      case 'y':
        out.push(String(value.getFullYear() % 100));
        break;
      case 'HH':
        out.push(String(hours24).padStart(2, '0'));
        break;
      case 'H':
        out.push(String(hours24));
        break;
      case 'hh':
        out.push(String(hours12).padStart(2, '0'));
        break;
      case 'h':
        out.push(String(hours12));
        break;
      case 'mm':
        out.push(String(value.getMinutes()).padStart(2, '0'));
        break;
      case 'm':
        out.push(String(value.getMinutes()));
        break;
      case 'ss':
        out.push(String(value.getSeconds()).padStart(2, '0'));
        break;
      case 's':
        out.push(String(value.getSeconds()));
        break;
      case 'tt':
        out.push(namePart(value, { hour: 'numeric', hour12: true }, 'dayPeriod'));
        break;
      case 't':
        out.push(namePart(value, { hour: 'numeric', hour12: true }, 'dayPeriod').slice(0, 1));
        break;
      default:
        out.push(token);
        break;
    }
    i += run;
  }
  return out.join('');
}

function formatDate(value: Date, format: string): string {
  // The invariant round-trip patterns first: they are DEFINED to ignore the culture, which is the
  // whole reason a wire format uses them.
  if (format === 'O' || format === 'o') return value.toISOString();
  if (format === 's') return value.toISOString().slice(0, 19);

  if (format.length === 1 && DATE_ROLES[format] !== undefined) {
    const patterns = DATE_ROLES[format].map(patternFor);
    // Every role must have travelled; a half-known composite would print half a date.
    if (patterns.every((pattern) => pattern !== null))
      return patterns.map((pattern) => renderPattern(value, pattern as string)).join(' ');
    return new Intl.DateTimeFormat(activeFormatLocale(), DATE_STYLES[format]).format(value);
  }

  // A custom picture — `yyyy-MM-dd HH:mm`. Culture-independent by construction: the author wrote
  // the layout they want, digit for digit.
  const yyyy = value.getFullYear().toString();
  const MM = (value.getMonth() + 1).toString().padStart(2, '0');
  const dd = value.getDate().toString().padStart(2, '0');
  const HH = value.getHours().toString().padStart(2, '0');
  const mm = value.getMinutes().toString().padStart(2, '0');
  const ss = value.getSeconds().toString().padStart(2, '0');

  return format
    .replace(/yyyy/g, yyyy)
    .replace(/MM/g, MM)
    .replace(/dd/g, dd)
    .replace(/HH/g, HH)
    .replace(/mm/g, mm)
    .replace(/ss/g, ss);
}

const FORMAT_INDEX =
  'Index (zero based) must be greater than or equal to zero and less than the size of the argument list.';

/**
 * .NET `string.Format(template, ...args)`. Substitutes `{i}` / `{i:spec}` placeholders (the latter via
 * {@link format}, so `{0:F2}` formats arg 0 to 2 decimals) and unescapes `{{`/`}}` to `{`/`}`. Mirrors
 * the interpolation path (`$"{x:F2}"`), which already uses `format`.
 */
export function stringFormat(template: string, ...args: unknown[]): string {
  return template.replace(
    /\{\{|\}\}|\{(\d+)(?:,(-?\d+))?(?::([^}]*))?\}/g,
    (m, idx, width, spec) => {
      if (m === '{{') return '{';
      if (m === '}}') return '}';
      // A placeholder past the values is .NET's FormatException, where it was written as nothing.
      if (Number(idx) >= args.length) throw new Error(FORMAT_INDEX);
      const v = args[Number(idx)];
      // `{0,5}` aligns what the placeholder writes; it was left in the text as written.
      const alignment = width != null ? Number(width) : undefined;
      if (spec != null) return format(v, spec, alignment);
      return pad(general(v), alignment);
    },
  );
}

/** `string.Format(CultureInfo.InvariantCulture, template, …)`: every placeholder written in the
 * invariant culture, whoever reads the page (#377). */
export function stringFormatInvariant(template: string, ...args: unknown[]): string {
  invariantDepth++;
  try {
    return stringFormat(template, ...args);
  } finally {
    invariantDepth--;
  }
}

/**
 * A placeholder with NO specifier still formats per culture in .NET — `{0}` over 1234.5 is
 * "1234,5" in pt-BR — because it calls the value's `ToString(IFormatProvider)`. `String(v)` was
 * the old answer and it is invariant, so the one placeholder shape everybody writes was the one
 * that quietly disagreed with the server.
 */
function general(value: unknown): string {
  if (value === null || value === undefined) return '';
  // .NET's default ("G") is the shortest text that reads back, in its notation (1E+21, which
  // toLocaleString never writes), with the culture's decimal separator and no grouping; a float
  // boxed for the call keeps its own digits.
  if (value instanceof FormatNumber) return shortest(value.value, value.kind);
  if (typeof value === 'number') return shortest(value, 'double');
  if (typeof value === 'bigint' || value instanceof Decimal) return plainDigits(value);
  if (typeof value === 'boolean') return value ? 'True' : 'False';
  const date = asJsDate(value);
  if (date !== null) return formatDate(date, 'G');
  return String(value);
}

/**
 * Parse enum value from string (case-insensitive)
 * @param value The string value to parse
 * @param enumType The enum object
 * @returns The enum value if found, undefined otherwise
 */
export function parseEnum<T extends Record<string, any>>(
  value: string | number,
  enumType: T,
): T[keyof T] | undefined {
  if (typeof value === 'number') {
    return enumType[value] !== undefined ? enumType[value] : undefined;
  }

  const strValue = String(value);

  // Try exact match first (case-sensitive)
  if (enumType[strValue] !== undefined) {
    return enumType[strValue];
  }

  // Try case-insensitive match
  const keys = Object.keys(enumType);
  const matchedKey = keys.find((k) => k.toLowerCase() === strValue.toLowerCase());

  if (matchedKey) {
    return enumType[matchedKey];
  }

  // Try matching by value (reverse lookup for numeric enums)
  const values = Object.values(enumType);
  const matchedValue = values.find((v) => String(v).toLowerCase() === strValue.toLowerCase());

  if (matchedValue !== undefined) {
    return matchedValue as T[keyof T];
  }

  return undefined;
}
