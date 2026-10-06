/**
 * C#-style formatting, resolved against the CULTURE the app is running in (Track L D7/D13).
 *
 * The rule this file exists to keep: a value written as text in C# — `{0:N2}`, `$"{x}"`, `"v=" + x`,
 * `d.ToString()` — must read the same on the server and in the browser. The server has real .NET;
 * here there is `Intl` for the layout of `N`, `F`, `C` and `P`, and, for everything this file draws
 * by hand, the culture's own `NumberFormatInfo` and `DateTimeFormatInfo`, which the server writes
 * from .NET and hands every page (`CultureFormat`, #471). Anything outside what is pinned is refused
 * where the developer can see it, never approximated at runtime.
 *
 * Every formatter reads the ACTIVE format culture, and a page with none installed is in the
 * invariant culture: it used to pass `undefined` to `Intl`, which is "whatever locale the browser is
 * in", so a pt-BR request rendered "1,234.50" on a US laptop and "1.234,50" on a Brazilian one, from
 * the same server response.
 */

import { double, single } from './real-text';
import { exception } from './exceptions';
import {
  activeFormatData,
  formatLocale,
  INVARIANT_FORMAT,
  type CultureFormat,
  type DatePatternName,
  type DateTimeFormatData,
  type NumberFormatData,
} from './culture';

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
 * Whether the text being written is the invariant culture's: an explicitly invariant conversion —
 * `value.ToString("0.##", CultureInfo.InvariantCulture)`, which an author writes precisely so the
 * number does NOT follow whoever is reading it — or a page with no culture installed, which is in
 * the invariant culture as a .NET thread with none is (#471).
 */
function isInvariant(): boolean {
  return invariantDepth > 0 || formatLocale() === undefined;
}

/** The locale every `Intl` call in this file reads: the active format culture, or the invariant
 * culture's nearest, never the host's own. */
function activeFormatLocale(): string {
  return isInvariant() ? INVARIANT_LOCALE : (formatLocale() ?? INVARIANT_LOCALE);
}

/** The active culture's format data: the invariant culture's in an invariant conversion, and null
 * for a culture whose data did not travel, which this file reads through `Intl` instead. */
function cultureFormat(): CultureFormat | null {
  return invariantDepth > 0 ? INVARIANT_FORMAT : activeFormatData();
}
import { DateOnly, DateTime as DotNetDateTime, DateTimeOffset, TimeOnly } from './datetime';
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

/** Which C# number a JavaScript number stands for, by the name of its .NET type. A number cannot
 * say it is a double, a single or an int, so whoever knows passes it: the compiler, from the static
 * type, at every call it writes. A float's digits are not the double's (#378), an integer rounds a
 * formatted half away from zero where a double rounds it to even (#393), and only an integer takes
 * `D`, `X` and `B` (#455). A value whose type did not travel (one typed as an object, a generic) is
 * `unknown`, and read by what it holds. A long and a decimal say what they are on their own, as a
 * BigInt and a Decimal. */
export type NumberKind = 'unknown' | 'double' | 'single' | IntegerKind;

/** An integer that travels as a JavaScript number. Its width is what `X` and `B` write a negative
 * one at, as its two's complement: `((short)-1).ToString("X")` is `FFFF`, an int's is `FFFFFFFF`
 * (#445), and a `nint`'s is the platform's, sixteen F's on the 64-bit hosts .NET serves from (#455).
 * An unsigned one is never negative, and its bits are its digits. */
export type IntegerKind =
  | 'sbyte'
  | 'byte'
  | 'int16'
  | 'uint16'
  | 'int32'
  | 'uint32'
  | 'nint'
  | 'nuint';

const INTEGER_BITS: Readonly<Record<IntegerKind, number>> = {
  sbyte: 8,
  byte: 8,
  int16: 16,
  uint16: 16,
  int32: 32,
  uint32: 32,
  nint: 64,
  nuint: 64,
};

function isInteger(kind: NumberKind): kind is IntegerKind {
  return Object.prototype.hasOwnProperty.call(INTEGER_BITS, kind);
}

/**
 * A number on its way into `string.Format`, whose arguments are objects in C#: boxed with its kind,
 * which the formatter reads and nothing else ever sees. Anywhere else a boxed number is the plain
 * number, and its kind is lost (#378).
 */
export class FormatNumber {
  constructor(
    readonly value: number,
    readonly kind: NumberKind,
  ) {}
}

/** Boxes a number of the given kind for `string.Format`; null stays null. */
export function asNumber(
  value: number | null | undefined,
  kind: NumberKind,
): FormatNumber | null | undefined {
  return value == null ? value : new FormatNumber(value, kind);
}

/**
 * @param value The value to format
 * @param format The format string (e.g. "C2", "N0", "yyyy-MM-dd"); none, or an empty one, is the
 *   value's own text, as its `ToString()` writes it in the culture in force
 * @param alignment Optional alignment width
 * @param invariant Format against the INVARIANT culture rather than the active one — what
 *   `ToString(CultureInfo.InvariantCulture)` asks for, and the shape a number written for a
 *   machine (a CSS length, a key, a wire value) has to keep whoever is reading the page.
 * @param kind A number's .NET type, where the compiler knows it ({@link NumberKind}).
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
    return pad(formatCore(value, format, kind ?? 'unknown'), alignment);
  } finally {
    if (invariant) invariantDepth--;
  }
}

function formatCore(value: any, format: string | null, kind: NumberKind): string {
  // No format is the value's own text: a number's and a date's in the culture in force, as .NET
  // writes `$"{x}"`, `"v=" + x` and `x.ToString()` (#454), where this wrote the invariant digits.
  if (!format) return general(value, kind);
  if (typeof value === 'number' || typeof value === 'bigint' || value instanceof Decimal)
    return formatNumber(value, format, kind);
  const moment = momentOf(value);
  if (moment !== null) return formatDate(moment, format);
  // A value that takes no format writes its own text, as one that is not IFormattable does in .NET.
  return general(value, kind);
}

/**
 * A member a record's text prints: its C# name, read under its twin's name (the name with its first
 * letter lowered, as `TwinName` names it), with what the type says that the value cannot: a number's
 * kind (a float's own digits, an integer's unsigned zero), or a function that writes a value the
 * formatter cannot read (an enum's member name, a data twin's own record text).
 */
export type PrintedMember = string | readonly [string, NumberKind | ((value: any) => string)];

/**
 * The text .NET writes for a record, `Name { A = 1, B = x }` as `PrintMembers` lists it: the members
 * .NET prints, in its order, each written as a concatenation writes it (#454), a number in the
 * culture in force, a null as nothing, a bool as `True`. A record the browser holds as a class and one
 * it holds as plain data (`[TwinIsData]`, whose own string would be `[object Object]`) both write it
 * here. A null value is the empty string, as `$"{value}"` is.
 */
export function recordText(value: unknown, name: string, members: readonly PrintedMember[]): string {
  if (value === null || value === undefined) return '';
  const data = value as Record<string, unknown>;
  const written = members.map((member) => {
    const [label, how] = typeof member === 'string' ? [member, undefined] : member;
    const held = data[label.charAt(0).toLowerCase() + label.slice(1)];
    return `${label} = ${typeof how === 'function' ? how(held) : format(held, null, undefined, undefined, how)}`;
  });
  return written.length === 0 ? `${name} { }` : `${name} { ${written.join(', ')} }`;
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
const symbolsByData = new WeakMap<CultureFormat, PictureSymbols>();

/**
 * The culture's number symbols, as its `NumberFormatInfo` holds them: the data the server wrote
 * from .NET (#455: `ar-SA`'s per mille is `؉`, and `ar`'s minus sign carries a left-to-right mark
 * that `Intl` keeps outside its sign), or, for a culture whose data did not travel, what `Intl`
 * writes in its locale, which is the same CLDR .NET's is built from.
 */
function symbols(): PictureSymbols {
  const data = cultureFormat();
  if (data !== null) {
    let known = symbolsByData.get(data);
    if (known === undefined) {
      const number = data.numberFormat;
      known = {
        decimal: number.numberDecimalSeparator,
        group: number.numberGroupSeparator,
        groupSizes: number.numberGroupSizes,
        minus: number.negativeSign,
        plus: number.positiveSign,
        percent: number.percentSymbol,
        perMille: number.perMilleSymbol,
      };
      symbolsByData.set(data, known);
    }
    return known;
  }
  const locale = activeFormatLocale();
  const key = locale;
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
      // `Intl` has no per mille sign; CLDR's is `‰` in every culture but the Arabic-script ones,
      // whose data travels from the server.
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

/** The four specifiers .NET lays out from the culture's patterns (`Number.Formatting.cs`). */
type Laid = 'N' | 'F' | 'C' | 'P';

/**
 * .NET's tables for a negative `N`, a currency and a percent, which a culture's `NumberFormatInfo`
 * indexes: `#` is the number, `-` the culture's negative sign, `$` its currency symbol and `%` its
 * percent symbol, and anything else is written as it stands. Their spaces are U+0020, which is what
 * .NET writes where CLDR, and `Intl` with it, has a no-break space (#634).
 */
const NEGATIVE_NUMBER = ['(#)', '-#', '- #', '#-', '# -'];
const POSITIVE_CURRENCY = ['$#', '#$', '$ #', '# $'];
const NEGATIVE_CURRENCY = [
  '($#)', '-$#', '$-#', '$#-', '(#$)', '-#$', '#-$', '#$-', '-# $',
  '-$ #', '# $-', '$ #-', '$ -#', '#- $', '($ #)', '(# $)', '$- #',
];
const POSITIVE_PERCENT = ['# %', '#%', '%#', '% #'];
const NEGATIVE_PERCENT = [
  '-# %', '-#%', '-%#', '%-#', '%#-', '#-%', '#%-', '-% #', '# %-', '% #-', '% -#', '#- %',
];

/** How a specifier lays a number out: the culture's digits with no precision, its separators, the
 * group sizes (none for `F`), and its patterns for a positive and a negative value. */
interface Layout {
  readonly digits: number;
  readonly decimal: string;
  readonly group: string;
  readonly sizes: readonly number[];
  readonly positive: string;
  readonly negative: string;
}

function layoutOf(letter: Laid, info: NumberFormatData): Layout {
  switch (letter) {
    case 'C':
      return {
        digits: info.currencyDecimalDigits,
        decimal: info.currencyDecimalSeparator,
        group: info.currencyGroupSeparator,
        sizes: info.currencyGroupSizes,
        positive: POSITIVE_CURRENCY[info.currencyPositivePattern],
        negative: NEGATIVE_CURRENCY[info.currencyNegativePattern],
      };
    case 'P':
      return {
        digits: info.percentDecimalDigits,
        decimal: info.percentDecimalSeparator,
        group: info.percentGroupSeparator,
        sizes: info.percentGroupSizes,
        positive: POSITIVE_PERCENT[info.percentPositivePattern],
        negative: NEGATIVE_PERCENT[info.percentNegativePattern],
      };
    case 'N':
      return {
        digits: info.numberDecimalDigits,
        decimal: info.numberDecimalSeparator,
        group: info.numberGroupSeparator,
        sizes: info.numberGroupSizes,
        positive: '#',
        negative: NEGATIVE_NUMBER[info.numberNegativePattern],
      };
    case 'F':
      return {
        digits: info.numberDecimalDigits,
        decimal: info.numberDecimalSeparator,
        group: '',
        sizes: [],
        positive: '#',
        negative: '-#',
      };
  }
}

/**
 * `N`, `F`, `C` and `P` as .NET's `Number.Formatting` lays them out from the culture's
 * `NumberFormatInfo` (#634): the value, a percent's times 100, rounded to the precision asked or to
 * the culture's own digits by the type's tie rule, its whole part grouped by the specifier's group
 * sizes, and set in the culture's pattern for its sign. `Intl` laid them out, and its ICU is not
 * .NET's: it wrote ar-EG's own digits where .NET writes ASCII ones, a no-break space where .NET's
 * pattern has a plain one, and two digits with no precision where .NET reads the culture's, three on
 * ICU.
 */
function laidOut(number: Numeric, letter: Laid, specified: number | null, info: NumberFormatData): string {
  const layout = layoutOf(letter, info);
  const places = specified ?? layout.digits;
  const rounded = roundFraction(letter === 'P' ? scaled(number.exact, 2) : number.exact, places, number.tie);
  // A zero keeps its sign where its type does: a double's `-0.00`, never an integer's or a decimal's.
  const negative = rounded.negative && (number.signedZero || !isZero(rounded));
  const text = plainText({ ...rounded, negative: false });
  const point = text.indexOf('.');
  const whole = groupDigits(point < 0 ? text : text.slice(0, point), layout.sizes, layout.group);
  const fraction = point < 0 ? '' : text.slice(point + 1);
  const digits = places > 0 ? whole + layout.decimal + fraction.padEnd(places, '0') : whole;
  let out = '';
  for (const part of negative ? layout.negative : layout.positive) {
    if (part === '#') out += digits;
    else if (part === '-') out += info.negativeSign;
    else if (part === '$') out += info.currencySymbol;
    else if (part === '%') out += info.percentSymbol;
    else out += part;
  }
  return out;
}

/** A whole number's digits in groups, as .NET's `FormatFixed` writes them: the size next to the
 * point first, each after it once, the last one repeating, and a 0 that ends the grouping. */
function groupDigits(whole: string, sizes: readonly number[], separator: string): string {
  if (sizes.length === 0) return whole;
  const groups: string[] = [];
  let end = whole.length;
  let at = 0;
  let size = sizes[0];
  while (size > 0 && end > size) {
    groups.push(whole.slice(end - size, end));
    end -= size;
    if (at < sizes.length - 1) size = sizes[++at];
  }
  groups.push(whole.slice(0, end));
  return groups.reverse().join(separator);
}

/**
 * `N`, `F`, `C` and `P` for a culture whose data did not travel (a switch with no server to ask),
 * laid out by `Intl`: close, not exact, which is why the data travels. Nothing names the culture's
 * currency there, and `Intl` wants an ISO code, so `C` writes .NET's generic ¤ in the invariant
 * culture's patterns, "¤n" and "(¤n)", rather than guess a country.
 */
function laidOutByIntl(number: Numeric, letter: Laid, specified: number | null): string {
  const precision = specified ?? 2;
  const digits: ExactOptions = { minimumFractionDigits: precision, maximumFractionDigits: precision };
  switch (letter) {
    case 'N':
      return exactly(number, digits);
    case 'F':
      return exactly(number, { ...digits, useGrouping: false });
    case 'P':
      return exactly(number, { style: 'percent', ...digits });
    case 'C': {
      const text = exactly(number, digits);
      const { minus } = symbols();
      return text.startsWith(minus) ? `(¤${text.slice(minus.length)})` : `¤${text}`;
    }
  }
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
  const { decimal, minus, plus } = symbols();
  const mantissa = rounded.digits[0] + (precision > 0 ? decimal + rounded.digits.slice(1) : '');
  const exponent = rounded.scientific;
  const power = String(Math.abs(exponent)).padStart(3, '0');
  // The exponent's sign is the culture's too: `ar` writes `1.23E\u200e+003`.
  return `${signOf(number, minus)}${mantissa}${marker}${exponent < 0 ? minus : plus}${power}`;
}

/**
 * `G` with a precision: that many significant digits, their trailing zeros dropped, in fixed
 * notation while the exponent lies between -5 and the precision and in scientific notation past
 * it, where the exponent takes at least two digits — `(12345.678m).ToString("G2")` is `1.2E+04`.
 * It was not modelled, and wrote the value as JavaScript prints it.
 */
function generalWithPrecision(number: Numeric, precision: number, marker: 'E' | 'e'): string {
  const rounded = roundSignificant(number.exact, precision, number.tie);
  const { decimal, minus, plus } = symbols();
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
  return `${sign}${mantissa}${marker}${exponent < 0 ? minus : plus}${power}`;
}

/**
 * A double's infinities and NaN are words, not digits: the culture's own symbols (`∞`, and the
 * invariant culture's `Infinity` and `NaN`), from its data, or as `Intl` writes them in a culture
 * whose data did not travel.
 */
function nonFinite(value: number): string {
  const data = cultureFormat();
  if (data === null) return numberFormat({}).format(value);
  const number = data.numberFormat;
  if (Number.isNaN(value)) return number.nanSymbol;
  return value > 0 ? number.positiveInfinitySymbol : number.negativeInfinitySymbol;
}

/**
 * The shortest digits that read back as the value, in .NET's notation (1E+21, -0), with the
 * active culture's decimal separator and signs (sv-SE writes `−1,5` and `1E−05`, with the minus
 * its data spells, and `ar` writes `1E\u200e+21`): what `G` and `R` write, and the text of a
 * number with no specifier at all. An integer is its digits, and a zero has no sign: `-1 / 2`
 * truncates to -0 in JavaScript, which only a double's text keeps, and to 0 in C#.
 */
function shortest(value: number, kind: NumberKind): string {
  if (!Number.isFinite(value)) return nonFinite(value);
  if (isInteger(kind)) return inCulture(String(value));
  return inCulture(kind === 'single' ? single(value) : double(value));
}

/** A long's or a decimal's text with no specifier: all of its digits, a decimal's scale kept
 * (`12.50m` is `12.50`), in the culture's decimal separator and minus sign. */
function plainDigits(value: bigint | Decimal): string {
  return inCulture(value.toString());
}

/** A number's invariant text (`-1.5E+21`) in the culture's symbols: its point, its minus signs
 * and the plus of its exponent. The text has at most one of each but the minus. */
function inCulture(text: string): string {
  const { decimal, minus, plus } = symbols();
  if (decimal === '.' && minus === '-' && plus === '+') return text;
  return text.replace('.', decimal).replace(/-/g, minus).replace('+', plus);
}

/** A standard specifier is ONE letter and an optional precision; anything else is a picture. */
const STANDARD = /^([A-Za-z])(\d*)$/;

/** The largest precision .NET takes; a larger one is a FormatException. */
const MAX_PRECISION = 999_999_999;

/**
 * Whether `D`, `X` and `B` can take the value: an integer's, never a decimal's, a double's or a
 * float's, whatever it holds, for which .NET throws: `(2.0).ToString("D")` prints nothing (#455).
 * A whole number whose type did not travel (a value typed as an object, a generic) is taken as the
 * integer it holds, since the type that would say otherwise is not there to ask.
 */
function integral(value: number | bigint | Decimal, kind: NumberKind): boolean {
  if (typeof value === 'bigint') return true;
  if (value instanceof Decimal) return false;
  if (isInteger(kind)) return true;
  return kind === 'unknown' && Number.isInteger(value);
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

function formatNumber(value: number | bigint | Decimal, format: string, kind: NumberKind): string {
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
  if (specified !== null && specified > MAX_PRECISION) throw exception('System.FormatException', BAD_SPECIFIER);

  const upper = letter.toUpperCase();
  if (upper === 'N' || upper === 'F' || upper === 'C' || upper === 'P') {
    const data = cultureFormat();
    return data !== null
      ? laidOut(number, upper, specified, data.numberFormat)
      : laidOutByIntl(number, upper, specified);
  }

  switch (upper) {
    case 'E': // Scientific — six digits after the point unless told otherwise
      return scientific(number, specified ?? 6, letter === 'e' ? 'e' : 'E');
    case 'D': {
      // Decimal: an integer's digits, padded, and the culture's minus sign (sv-SE's is U+2212)
      if (!integral(value, kind)) throw exception('System.FormatException', BAD_SPECIFIER);
      const whole = BigInt(value as number | bigint);
      const magnitude = (whole < 0n ? -whole : whole).toString();
      return (whole < 0n ? symbols().minus : '') + magnitude.padStart(specified ?? 1, '0');
    }
    case 'X': // Hex, and B, binary (.NET 8): a negative integer's two's complement at its width
    case 'B': {
      if (!integral(value, kind)) throw exception('System.FormatException', BAD_SPECIFIER);
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
      throw exception('System.FormatException', BAD_SPECIFIER);
  }
}

/** .NET's FormatException for a date format its type does not take or a picture it cannot read. */
const BAD_DATE_FORMAT = 'Input string was not in a correct format.';

/** The error .NET throws for a date format it refuses: its FormatException. */
function formatError(message: string): Error {
  return exception('System.FormatException', message);
}

/** The .NET type a date's text is drawn for: what it may be asked for, and what it writes with none. */
type DateType = 'dateTime' | 'dateOnly' | 'timeOnly' | 'dateTimeOffset';

/**
 * A date as the formatter draws it. `date` is a native Date whose UTC fields ARE the wall-clock
 * parts to print, so no time zone can move them: a LOCAL Date built from a compat value's parts was
 * normalised by the host's zone, and in a spring-forward gap 2026-03-08 02:30 in New York became
 * 03:30 (found in review, #472). Every reader below asks the UTC fields, and `Intl` is told the zone
 * is UTC.
 */
interface Moment {
  readonly type: DateType;
  readonly date: Date;
  /** The fraction of a second as the seven digits `f` and `o` write, asked only by what writes it. */
  readonly fraction: () => string;
  /** A DateTimeOffset's offset from UTC, in minutes. Null for the other types, whose offset — what
   * `z` writes — is the host's at that time, as .NET's is for a DateTime of no kind. */
  readonly offset: number | null;
}

const TICKS_PER_SECOND = 10_000_000n;
const TICKS_PER_MINUTE = 600_000_000n;

/** The fraction of a second in a tick count, as seven digits. */
function sevenDigits(ticks: bigint): string {
  return (ticks % TICKS_PER_SECOND).toString().padStart(7, '0');
}

/**
 * The value as a moment, or null when it is no date. A native Date is an instant, and its local
 * parts are the ones it always printed; the compat `DateTime` keeps no zone, as a .NET DateTime of
 * no kind; a `DateOnly` is its day at midnight and a `TimeOnly` its time on the first day, which is
 * what .NET formats them as; and a `DateTimeOffset` is its own clock with its offset (#469). Without
 * this, `{Moment:d}` over a compat value fell through to `String(value)`, the invariant default.
 */
function momentOf(value: unknown): Moment | null {
  if (value instanceof Date) {
    const date = wallClock(
      value.getFullYear(),
      value.getMonth(),
      value.getDate(),
      value.getHours(),
      value.getMinutes(),
      value.getSeconds(),
      value.getMilliseconds(),
    );
    // A native Date has its milliseconds, which is all it has.
    const fraction = () => String(value.getMilliseconds()).padStart(3, '0') + '0000';
    return { type: 'dateTime', date, fraction, offset: null };
  }
  if (value instanceof DotNetDateTime)
    return {
      type: 'dateTime',
      date: wallClock(value.year, value.month - 1, value.day, value.hour, value.minute, value.second, value.millisecond),
      fraction: () => sevenDigits(value.ticks),
      offset: null,
    };
  if (value instanceof DateOnly)
    return {
      type: 'dateOnly',
      date: wallClock(value.year, value.month - 1, value.day, 0, 0, 0, 0),
      fraction: () => '0000000',
      offset: null,
    };
  if (value instanceof TimeOnly)
    return {
      type: 'timeOnly',
      date: wallClock(1, 0, 1, value.hour, value.minute, value.second, value.millisecond),
      fraction: () => sevenDigits(value.ticks),
      offset: null,
    };
  if (value instanceof DateTimeOffset)
    return {
      type: 'dateTimeOffset',
      date: wallClock(value.year, value.month - 1, value.day, value.hour, value.minute, value.second, value.millisecond),
      fraction: () => sevenDigits(value.localTicks),
      offset: Number(value.offsetTicks / TICKS_PER_MINUTE),
    };
  return null;
}

/** The wall-clock parts as a Date's UTC fields. `Date.UTC` reads a year from 0 to 99 as 1900 plus
 * it, so the year is set again: DateTime.MinValue printed 1901. */
function wallClock(
  year: number,
  month: number,
  day: number,
  hour: number,
  minute: number,
  second: number,
  millisecond: number,
): Date {
  const date = new Date(Date.UTC(year, month, day, hour, minute, second, millisecond));
  date.setUTCFullYear(year);
  return date;
}

/**
 * The standard date/time specifiers .NET spells with ONE letter, as the culture PATTERNS they
 * actually stand for — `d` is not "a short date" in the abstract, it is this culture's
 * ShortDatePattern. Two patterns joined by a space is how .NET composes `g`/`G`/`f`/`F`.
 */
const DATE_ROLES: Readonly<Record<string, readonly DatePatternName[]>> = {
  d: ['shortDatePattern'],
  D: ['longDatePattern'],
  t: ['shortTimePattern'],
  T: ['longTimePattern'],
  g: ['shortDatePattern', 'shortTimePattern'],
  G: ['shortDatePattern', 'longTimePattern'],
  f: ['longDatePattern', 'shortTimePattern'],
  F: ['longDatePattern', 'longTimePattern'],
  M: ['monthDayPattern'],
  m: ['monthDayPattern'],
  Y: ['yearMonthPattern'],
  y: ['yearMonthPattern'],
};

/**
 * The one-letter specifiers each type takes; any other one letter is .NET's FormatException, which
 * is how a one-letter format is read whatever its letter (`d.ToString("z")` throws). A `DateOnly`
 * takes the date's and a `TimeOnly` the time's (#469), and a `DateTimeOffset` everything but `U`.
 */
const STANDARD_DATE: Readonly<Record<DateType, string>> = {
  dateTime: 'dDfFgGmMoOrRstTuUyY',
  dateOnly: 'dDmMoOrRyY',
  timeOnly: 'tToOrR',
  dateTimeOffset: 'dDfFgGmMoOrRstTuyY',
};

/**
 * What a custom picture of a `DateOnly` or a `TimeOnly` may not name, outside its quotes: a time's
 * parts for the first and a date's for the second, as .NET refuses them. `K` and `g` are taken by
 * a time, which .NET formats as the time on the first day.
 */
const REFUSED_PARTS: Readonly<Partial<Record<DateType, string>>> = {
  dateOnly: ':tfFhHmszK',
  timeOnly: 'dMy/zk',
};

/** The date and time patterns of the culture writing the date, or null when its data did not
 * travel, where `Intl`'s presets stand in. */
function dateTimeFormat(): DateTimeFormatData | null {
  return cultureFormat()?.dateTimeFormat ?? null;
}

/** `Intl`'s presets, for a culture whose patterns did not travel. Close, not exact — which is why
 * the patterns travel at all. */
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
 * `Intl` is exactly right about these, in every culture, which is why they are not in the data. With
 * no culture in force the names are the invariant culture's, as the patterns are: the host's own
 * locale wrote `quinta-feira` into an invariant layout on a Portuguese machine. */
function namePart(value: Date, options: Intl.DateTimeFormatOptions, type: string): string {
  const parts = new Intl.DateTimeFormat(activeFormatLocale(), {
    ...options,
    timeZone: 'UTC',
  }).formatToParts(value);
  return parts.find((part) => part.type === type)?.value ?? '';
}

/** What a culture whose data did not travel writes for `/`, `:` and `g`, as `Intl` writes them: the
 * literal between the day and the month of a numeric date, the one after the hour, and the era. */
interface IntlDateSymbols {
  readonly dateSeparator: string;
  readonly timeSeparator: string;
  readonly eraName: string;
}

const dateSymbolsByLocale = new Map<string, IntlDateSymbols>();

function intlDateSymbols(): IntlDateSymbols {
  const locale = activeFormatLocale();
  let found = dateSymbolsByLocale.get(locale);
  if (found === undefined) {
    const sample = new Date(Date.UTC(2026, 8, 24, 10, 30));
    const parts = (options: Intl.DateTimeFormatOptions): Intl.DateTimeFormatPart[] =>
      new Intl.DateTimeFormat(locale, { ...options, timeZone: 'UTC' }).formatToParts(sample);
    const after = (list: Intl.DateTimeFormatPart[], type: string, fallback: string): string => {
      const at = list.findIndex((part) => part.type === type);
      return at >= 0 && list[at + 1]?.type === 'literal' ? list[at + 1].value : fallback;
    };
    const date = parts({ year: 'numeric', month: '2-digit', day: '2-digit' });
    const first = date.find((part) => part.type !== 'literal')?.type ?? 'day';
    found = {
      dateSeparator: after(date, first, '/'),
      timeSeparator: after(parts({ hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }), 'hour', ':'),
      eraName:
        parts({ era: 'short', year: 'numeric' }).find((part) => part.type === 'era')?.value ?? '',
    };
    dateSymbolsByLocale.set(locale, found);
  }
  return found;
}

/** The culture's date separator, time separator and era, from its data or, failing it, `Intl`. */
function dateSymbol(name: keyof IntlDateSymbols): string {
  const data = dateTimeFormat();
  return data !== null ? data[name] : intlDateSymbols()[name];
}

/** How many times the character at `at` repeats from there: the length of the token it starts. */
function runOf(pattern: string, at: number): number {
  let run = 1;
  while (at + run < pattern.length && pattern[at + run] === pattern[at]) run++;
  return run;
}

/** A number written with at least `length` digits, as .NET's `FormatDigits` writes it: two at
 * most unless asked for more. */
function digits(value: number, length: number, unlimited = false): string {
  return String(value).padStart(unlimited ? length : Math.min(length, 2), '0');
}

/** The moment's offset from UTC, in minutes: a DateTimeOffset's own, and for every other type the
 * host's at that time, as .NET reads it for a DateTime of no kind. */
function offsetMinutes(moment: Moment): number {
  return moment.offset ?? Math.round(localOffset(moment.date.getTime()) / 60_000);
}

/** An offset as `z` (`+1`), `zz` (`+01`) and `zzz` (`+01:00`) write it: always signed, its minutes
 * after a colon that is not the culture's time separator. */
function offsetText(minutes: number, length: number): string {
  const sign = minutes < 0 ? '-' : '+';
  const hours = Math.trunc(Math.abs(minutes) / 60);
  if (length <= 1) return sign + String(hours);
  const whole = sign + String(hours).padStart(2, '0');
  return length < 3 ? whole : `${whole}:${String(Math.abs(minutes) % 60).padStart(2, '0')}`;
}

/**
 * Renders a .NET date/time PATTERN as .NET's `FormatCustomized` does, token by token: a run of one
 * letter is one token whose length decides what it writes (`yyyyy` is a five-digit year, `hhh` a
 * two-digit hour, `MMMMM` the month's name), `/` and `:` are the culture's date and time separators
 * (de-DE writes `dd/MM/yyyy` as `24.09.2026`), `z` the offset, `K` a DateTimeOffset's offset and
 * nothing for a DateTime of no kind, `g` the era, a quoted run and a `\`-escaped character are text,
 * and `%` makes the character after it a token of its own. They were written as they stand (#470).
 */
function renderPattern(moment: Moment, pattern: string): string {
  const value = moment.date;
  const hours24 = value.getUTCHours();
  const hours12 = hours24 % 12 === 0 ? 12 : hours24 % 12;
  // The culture's names, when its data travelled; `Intl`'s otherwise.
  const names = dateTimeFormat();
  let out = '';

  /** Writes the token a run of `ch` that long, starting at `at`, stands for. */
  const token = (ch: string, run: number, at: number): void => {
    switch (ch) {
      case 'g':
        out += dateSymbol('eraName');
        return;
      case 'h':
        out += digits(hours12, run);
        return;
      case 'H':
        out += digits(hours24, run);
        return;
      case 'm':
        out += digits(value.getUTCMinutes(), run);
        return;
      case 's':
        out += digits(value.getUTCSeconds(), run);
        return;
      case 'f':
      case 'F': {
        if (run > 7) throw formatError(BAD_DATE_FORMAT);
        const written = moment.fraction().slice(0, run);
        if (ch === 'f') {
          out += written;
          return;
        }
        // `F` drops the zeros that end it, and with them the point in front when nothing is left.
        const kept = written.replace(/0+$/, '');
        if (kept.length > 0) out += kept;
        else if (out.endsWith('.')) out = out.slice(0, -1);
        return;
      }
      case 't': {
        const morning = hours24 < 12;
        const designator =
          names !== null
            ? morning
              ? names.amDesignator
              : names.pmDesignator
            : namePart(value, { hour: 'numeric', hour12: true }, 'dayPeriod');
        out += run === 1 ? designator.slice(0, 1) : designator;
        return;
      }
      case 'd': {
        if (run <= 2) {
          out += digits(value.getUTCDate(), run);
          return;
        }
        const day = value.getUTCDay();
        out +=
          names !== null
            ? (run === 3 ? names.abbreviatedDayNames : names.dayNames)[day]
            : namePart(value, { weekday: run === 3 ? 'short' : 'long' }, 'weekday');
        return;
      }
      case 'M': {
        const month = value.getUTCMonth();
        if (run <= 2) {
          out += digits(month + 1, run);
          return;
        }
        if (names === null) {
          out += namePart(value, { month: run === 3 ? 'short' : 'long' }, 'month');
          return;
        }
        // A month beside its day is named in the genitive where the culture has one, as .NET
        // decides it: a `d` or a `dd` before the month or, failing that, after it.
        const genitive = besideItsDay(pattern, at, run);
        const list =
          run === 3
            ? genitive
              ? names.abbreviatedMonthGenitiveNames
              : names.abbreviatedMonthNames
            : genitive
              ? names.monthGenitiveNames
              : names.monthNames;
        out += list[month];
        return;
      }
      case 'y': {
        const year = value.getUTCFullYear();
        out += run <= 2 ? digits(year % 100, run) : digits(year, run, true);
        return;
      }
      case 'z':
        out += offsetText(offsetMinutes(moment), run);
        return;
      case 'K':
        // A DateTime keeps no kind, and .NET writes nothing for one of no kind.
        if (moment.type === 'dateTimeOffset') out += offsetText(moment.offset ?? 0, 3);
        return;
      case ':':
        out += dateSymbol('timeSeparator');
        return;
      case '/':
        out += dateSymbol('dateSeparator');
        return;
      default:
        out += ch;
    }
  };

  for (let i = 0; i < pattern.length; ) {
    const ch = pattern[i];
    if (ch === "'" || ch === '"') {
      // A quoted run is text, a `\` inside it escaping the next character, as .NET reads it.
      let at = i + 1;
      let closed = false;
      while (at < pattern.length) {
        const inside = pattern[at++];
        if (inside === ch) {
          closed = true;
          break;
        }
        if (inside === '\\') {
          if (at >= pattern.length) throw formatError(BAD_DATE_FORMAT);
          out += pattern[at++];
        } else {
          out += inside;
        }
      }
      if (!closed) throw formatError(`Cannot find a matching quote character for the character '${ch}'.`);
      i = at;
      continue;
    }
    if (ch === '\\') {
      if (i + 1 >= pattern.length) throw formatError(BAD_DATE_FORMAT);
      out += pattern[i + 1];
      i += 2;
      continue;
    }
    if (ch === '%') {
      // The character after it is a token of its own; `%%` and a `%` that ends the picture are refused.
      const next = pattern[i + 1];
      if (next === undefined || next === '%') throw formatError(BAD_DATE_FORMAT);
      token(next, 1, i + 1);
      i += 2;
      continue;
    }
    // `K`, `/`, `:` and a character that is no token stand alone; a token letter takes its run.
    const run = 'ghHmsfFtdMyz'.includes(ch) ? runOf(pattern, i) : 1;
    token(ch, run, i);
    i += run;
  }
  return out;
}

/**
 * Whether a month name at `at`, `run` long, is beside its day, which is when .NET names it in the
 * genitive (`IsUseGenitiveForm`): the nearest `d` before it is a `d` or a `dd`, or else the nearest
 * one after it is. Read over the picture as written, quotes included, as .NET reads it.
 */
function besideItsDay(pattern: string, at: number, run: number): boolean {
  let i = at - 1;
  while (i >= 0 && pattern[i] !== 'd') i--;
  if (i >= 0) {
    let repeat = 0;
    while (--i >= 0 && pattern[i] === 'd') repeat++;
    if (repeat <= 1) return true;
  }
  i = at + run;
  while (i < pattern.length && pattern[i] !== 'd') i++;
  if (i < pattern.length) {
    let repeat = 0;
    while (++i < pattern.length && pattern[i] === 'd') repeat++;
    if (repeat <= 1) return true;
  }
  return false;
}

/** The invariant culture's abbreviated names, which `R` writes whatever culture is reading. */
const INVARIANT_DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const INVARIANT_MONTHS = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
];

/** `yyyy-MM-dd` and `HH:mm:ss` from a date's own parts: no time zone shifts them. */
function sortableParts(value: Date): { date: string; time: string } {
  const two = (part: number): string => String(part).padStart(2, '0');
  return {
    date: `${String(value.getUTCFullYear()).padStart(4, '0')}-${two(value.getUTCMonth() + 1)}-${two(value.getUTCDate())}`,
    time: `${two(value.getUTCHours())}:${two(value.getUTCMinutes())}:${two(value.getUTCSeconds())}`,
  };
}

const DAY_MS = 86_400_000;

/**
 * The host zone's offset, in milliseconds east of UTC, for wall-clock parts held as a Date's UTC
 * fields (`wall` is their time value), read as .NET's TimeZoneInfo.GetUtcOffset reads it: the
 * one offset that names those parts, or, for a time a transition skips or repeats, the standard one,
 * the smaller of the two around it. The Date constructor took the daylight instant of a repeated
 * hour, so 2026-11-01 01:30 in New York printed 05:30 UTC where .NET prints 06:30 (found in
 * Copilot's second round, #472).
 */
function localOffset(wall: number): number {
  const offsetAt = (instant: number) => -new Date(instant).getTimezoneOffset() * 60_000;
  const before = offsetAt(wall - DAY_MS);
  const after = offsetAt(wall + DAY_MS);
  const naming = [before, after].filter((offset) => offsetAt(wall - offset) === offset);
  return naming.length === 1 ? naming[0] : Math.min(before, after);
}

/** The same moment at UTC: a DateTimeOffset's clock less its own offset, and any other's read as
 * local time and moved, as .NET's ToUniversalTime moves a DateTime of no kind. */
function universal(moment: Moment): Moment {
  const wall = moment.date.getTime();
  const shift = moment.offset !== null ? moment.offset * 60_000 : localOffset(wall);
  return { ...moment, date: new Date(wall - shift), offset: 0 };
}

/**
 * Formats a date through a standard specifier or a custom picture, as .NET formats the type it is
 * (#388, #469). A DateTime's kind is not tracked (a wall-clock value, .NET's `Unspecified`), so the
 * round-trip, sortable and RFC 1123 forms write its own parts and only `U` converts, reading it as
 * local time as .NET does; a DateTimeOffset's `o` and `K` write its offset, and its `u` and `R`
 * convert it by it. No format, or an empty one, is the type's own text: `G` of a DateTime, `d` of a
 * DateOnly, `t` of a TimeOnly, and a DateTimeOffset's `G` with its offset after it.
 */
function formatDate(moment: Moment, format: string | null): string {
  if (!format) {
    switch (moment.type) {
      case 'dateOnly':
        return standardDate(moment, 'd');
      case 'timeOnly':
        return standardDate(moment, 't');
      case 'dateTimeOffset': {
        // .NET's DateTimeOffsetPattern: the short date, the long time, and the offset unless the
        // long time already writes one.
        const general = standardDate(moment, 'G');
        const longTime = dateTimeFormat()?.longTimePattern ?? '';
        return /z/.test(longTime.replace(/'[^']*'|"[^"]*"/g, '')) ? general : `${general} ${renderPattern(moment, 'zzz')}`;
      }
      default:
        return standardDate(moment, 'G');
    }
  }
  if (format.length === 1) return standardDate(moment, format);

  // A custom picture — `yyyy-MM-dd HH:mm`, `dd MMM yyyy`, `HH:mm:ss.fff` — drawn token by token as
  // a culture's own patterns are, once its type has been checked to take every part it names.
  const refused = REFUSED_PARTS[moment.type];
  if (refused !== undefined) checkParts(format, refused);
  return renderPattern(moment, format);
}

/** Refuses a picture that names a part its type does not have, outside its quotes and escapes. */
function checkParts(picture: string, refused: string): void {
  for (let i = 0; i < picture.length; i++) {
    const ch = picture[i];
    if (ch === '\\') {
      i++;
    } else if (ch === "'" || ch === '"') {
      const close = picture.indexOf(ch, i + 1);
      if (close < 0) throw formatError(`Cannot find a matching quote character for the character '${ch}'.`);
      i = close;
    } else if (refused.includes(ch)) {
      throw formatError(BAD_DATE_FORMAT);
    }
  }
}

/** A one-letter specifier, which is a STANDARD one whatever its letter: the forms defined to ignore
 * the culture, `U`, and the culture's patterns. A letter the type does not take is .NET's
 * FormatException. */
function standardDate(moment: Moment, letter: string): string {
  if (!STANDARD_DATE[moment.type].includes(letter)) throw formatError(BAD_DATE_FORMAT);
  const value = moment.date;
  // The invariant forms first: they are DEFINED to ignore the culture, which is the whole reason a
  // wire format uses them. They wrote `toISOString()`, which is UTC, so a page off UTC shifted the
  // hour and `o` spelled a `Z` a wall-clock value does not have.
  switch (letter) {
    case 'O':
    case 'o': {
      const { date, time } = sortableParts(value);
      if (moment.type === 'dateOnly') return date;
      if (moment.type === 'timeOnly') return `${time}.${moment.fraction()}`;
      const offset = moment.type === 'dateTimeOffset' ? offsetText(moment.offset ?? 0, 3) : '';
      return `${date}T${time}.${moment.fraction()}${offset}`;
    }
    case 's': {
      const { date, time } = sortableParts(value);
      return `${date}T${time}`;
    }
    case 'u': {
      const shown = moment.type === 'dateTimeOffset' ? universal(moment).date : value;
      const { date, time } = sortableParts(shown);
      return `${date} ${time}Z`;
    }
    case 'R':
    case 'r': {
      if (moment.type === 'timeOnly') return sortableParts(value).time;
      const shown = moment.type === 'dateTimeOffset' ? universal(moment).date : value;
      const { date, time } = sortableParts(shown);
      const day = `${INVARIANT_DAYS[shown.getUTCDay()]}, ${date.slice(8)} ${INVARIANT_MONTHS[shown.getUTCMonth()]} ${date.slice(0, 4)}`;
      return moment.type === 'dateOnly' ? day : `${day} ${time} GMT`;
    }
    case 'U':
      // The full date and time of the value read as local time and moved to UTC, as .NET's
      // ToUniversalTime moves an unspecified one.
      return standardDate(universal(moment), 'F');
  }

  const data = dateTimeFormat();
  const roles = DATE_ROLES[letter];
  if (data !== null) return roles.map((role) => renderPattern(moment, data[role])).join(' ');
  return new Intl.DateTimeFormat(activeFormatLocale(), {
    ...DATE_STYLES[letter],
    timeZone: 'UTC',
  }).format(value);
}

const FORMAT_INDEX =
  'Index (zero based) must be greater than or equal to zero and less than the size of the argument list.';

/**
 * .NET `string.Format(template, ...args)`. Substitutes `{i}` / `{i:spec}` placeholders (the latter via
 * {@link format}, so `{0:F2}` formats arg 0 to 2 decimals) and unescapes `{{`/`}}` to `{`/`}`. Mirrors
 * the interpolation path (`$"{x:F2}"`), which already uses `format`.
 */
export function stringFormat(template: string, ...args: unknown[]): string {
  // A null template is .NET's ArgumentNullException, by its parameter's name: read through null, it
  // was a NullReferenceException.
  if (template == null) throw exception('System.ArgumentNullException', "Value cannot be null. (Parameter 'format')");
  return template.replace(
    /\{\{|\}\}|\{(\d+)(?:,(-?\d+))?(?::([^}]*))?\}/g,
    (m, idx, width, spec) => {
      if (m === '{{') return '{';
      if (m === '}}') return '}';
      // A placeholder past the values is .NET's FormatException, where it was written as nothing.
      if (Number(idx) >= args.length) throw exception('System.FormatException', FORMAT_INDEX);
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
 * A value's own text, with no format: what its `ToString()` writes in the culture in force, which
 * is what .NET writes for `{0}`, `$"{x}"`, `"v=" + x` and `x.ToString()` alike (#454). A number is
 * the shortest text that reads back, in .NET's notation (1E+21, which toLocaleString never writes),
 * in the culture's symbols and with no grouping, a float in its own digits; a negative integer's
 * minus sign is the culture's (sv-SE writes U+2212); a date is its type's own pattern. `String(v)`
 * was the old answer and it is invariant, so the one shape everybody writes was the one that quietly
 * disagreed with the server.
 */
function general(value: unknown, kind: NumberKind = 'unknown'): string {
  if (value === null || value === undefined) return '';
  if (value instanceof FormatNumber) return shortest(value.value, value.kind);
  if (typeof value === 'number') return shortest(value, kind);
  if (typeof value === 'bigint' || value instanceof Decimal) return plainDigits(value);
  if (typeof value === 'boolean') return value ? 'True' : 'False';
  const moment = momentOf(value);
  if (moment !== null) return formatDate(moment, null);
  return String(value);
}

