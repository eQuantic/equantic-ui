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
import { round } from './dotnet-math';

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

/**
 * @param value The value to format
 * @param format The format string (e.g. "C2", "N0", "yyyy-MM-dd")
 * @param alignment Optional alignment width
 * @param invariant Format against the INVARIANT culture rather than the active one — what
 *   `ToString(CultureInfo.InvariantCulture)` asks for, and the shape a number written for a
 *   machine (a CSS length, a key, a wire value) has to keep whoever is reading the page.
 */
export function format(
  value: any,
  format: string | null,
  alignment?: number,
  invariant?: boolean,
): string {
  if (value === null || value === undefined) return '';
  if (invariant) invariantDepth++;
  try {
    return formatCore(value, format, alignment);
  } finally {
    if (invariant) invariantDepth--;
  }
}

function formatCore(value: any, format: string | null, alignment?: number): string {
  // .NET spells a bool `True`/`False`; JavaScript lowercases it. Everything else reads the same.
  let result = typeof value === 'boolean' ? (value ? 'True' : 'False') : String(value);

  if (format) {
    const date = asJsDate(value);
    if (typeof value === 'number') {
      result = formatNumber(value, format);
    } else if (date !== null) {
      result = formatDate(date, format);
    }
  }

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
 * A CUSTOM numeric format is a picture of the number — `0.0`, `#,##0.00`, `000` — rather than one
 * of the single-letter standard specifiers. `0` is a digit that is always shown (padded), `#` is
 * one shown only if it is there, and a `,` among them asks for group separators.
 *
 * It used to fall through to `value.toString()`, so `$"{bytes / 1024.0:0.0} KB"` — ordinary C# —
 * reached the browser as `0.72265625 KB`. The C# side had already done its part: eqc emits the
 * specifier faithfully, and this is where it was dropped.
 */
function formatCustomNumber(value: number, format: string): string {
  const point = format.indexOf('.');
  const wholePart = point < 0 ? format : format.slice(0, point);
  const fractionPart = point < 0 ? '' : format.slice(point + 1);

  const minimumFractionDigits = (fractionPart.match(/0/g) ?? []).length;
  const maximumFractionDigits = (fractionPart.match(/[0#]/g) ?? []).length;
  const minimumIntegerDigits = Math.max(1, (wholePart.match(/0/g) ?? []).length);

  return value.toLocaleString(activeFormatLocale(), {
    minimumIntegerDigits,
    minimumFractionDigits,
    maximumFractionDigits: Math.max(minimumFractionDigits, maximumFractionDigits),
    useGrouping: wholePart.includes(','),
  });
}

/**
 * Currency, the one specifier that needs a fact the browser cannot derive: `Intl` wants an ISO
 * CODE and a locale alone does not carry one. The code rides in the culture catalog (the build
 * reads it from .NET's own `RegionInfo`), and when there is none — the invariant culture — .NET
 * prints the generic ¤ sign, so that is what this prints too rather than guessing a country.
 */
function formatCurrency(value: number, precision: number): string {
  const currency = activeCurrency();
  const locale = activeFormatLocale();
  const rounded = round(value, precision);
  if (currency !== null) {
    return rounded.toLocaleString(locale, {
      style: 'currency',
      currency,
      minimumFractionDigits: precision,
      maximumFractionDigits: precision,
    });
  }

  // .NET's invariant currency pattern is "¤n" with the invariant number conventions.
  const number = rounded.toLocaleString(locale, {
    minimumFractionDigits: precision,
    maximumFractionDigits: precision,
  });
  return rounded < 0 ? `-¤${number.replace('-', '')}` : `¤${number}`;
}

function formatNumber(value: number, format: string): string {
  // A standard specifier is a LETTER (optionally followed by a precision); anything drawn with
  // digit placeholders is a custom picture and is read as one.
  if (!/^[A-Za-z]/.test(format)) {
    return /[0#]/.test(format) ? formatCustomNumber(value, format) : value.toString();
  }

  const specifier = format[0].toUpperCase();
  const digits = format.slice(1);
  const precision = digits.length > 0 ? parseInt(digits) : 2;
  const locale = activeFormatLocale();

  switch (specifier) {
    case 'C': // Currency
      return formatCurrency(value, precision);
    case 'N': // Number — grouped, culture separators
      // Pre-rounded with .NET's rule: formatting a midpoint rounds to EVEN there
      // (1234.5:N0 is "1,234", 0.125:N2 is "0.12") and half-away-from-zero in Intl. The server
      // and the browser printing different digits for the same value is the exact class of
      // divergence this subset exists to remove.
      return round(value, precision).toLocaleString(locale, {
        minimumFractionDigits: precision,
        maximumFractionDigits: precision,
      });
    case 'P': // Percentage — Intl multiplies by 100, exactly as .NET's P does
      // .NET rounds the PERCENT value, so the rounding happens after the ×100.
      return (round(value * 100, precision) / 100).toLocaleString(locale, {
        style: 'percent',
        minimumFractionDigits: precision,
        maximumFractionDigits: precision,
      });
    case 'F': // Fixed point — culture decimal separator, NEVER grouped
      // `toFixed` was the old answer and it is invariant: a pt-BR page showed "1234.50" beside
      // numbers that used a comma everywhere else on the same line.
      return round(value, precision).toLocaleString(locale, {
        minimumFractionDigits: precision,
        maximumFractionDigits: precision,
        useGrouping: false,
      });
    case 'D': // Decimal (integer pad) — culture-independent by definition in .NET
      return (
        (value < 0 ? '-' : '') +
        Math.abs(Math.round(value))
          .toString()
          .padStart(digits.length > 0 ? precision : 1, '0')
      );
    case 'X': // Hex — culture-independent
      return Math.floor(value)
        .toString(16)
        .toUpperCase()
        .padStart(digits.length > 0 ? precision : 1, '0');
    default:
      return value.toString();
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
    const patterns = DATE_ROLES[format].map(activePattern);
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

/**
 * .NET `string.Format(template, ...args)`. Substitutes `{i}` / `{i:spec}` placeholders (the latter via
 * {@link format}, so `{0:F2}` formats arg 0 to 2 decimals) and unescapes `{{`/`}}` to `{`/`}`. Mirrors
 * the interpolation path (`$"{x:F2}"`), which already uses `format`.
 */
export function stringFormat(template: string, ...args: unknown[]): string {
  return template.replace(/\{\{|\}\}|\{(\d+)(?::([^}]*))?\}/g, (m, idx, spec) => {
    if (m === '{{') return '{';
    if (m === '}}') return '}';
    const v = args[Number(idx)];
    if (spec != null) return format(v, spec);
    return general(v);
  });
}

/**
 * A placeholder with NO specifier still formats per culture in .NET — `{0}` over 1234.5 is
 * "1234,5" in pt-BR — because it calls the value's `ToString(IFormatProvider)`. `String(v)` was
 * the old answer and it is invariant, so the one placeholder shape everybody writes was the one
 * that quietly disagreed with the server.
 */
function general(value: unknown): string {
  if (value === null || value === undefined) return '';
  if (typeof value === 'number') {
    // .NET's default ("G") does not group; it only swaps the decimal separator.
    return value.toLocaleString(activeFormatLocale(), {
      useGrouping: false,
      maximumFractionDigits: 20,
    });
  }
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
