/**
 * .NET-compat `DateTime` and `TimeSpan` — exact, timezone-free, tick-precise.
 *
 * JavaScript's `Date` conflates an instant with a timezone, uses a millisecond epoch, and formats
 * nothing like .NET. .NET models both types as a 64-bit count of **ticks** (100-nanosecond units):
 * `DateTime` since 0001-01-01 00:00:00, `TimeSpan` as a signed duration. We reproduce that with a
 * BigInt tick count, so arithmetic is exact (no float drift), `ToString()` matches .NET, and
 * `DateTime - DateTime` yields a `TimeSpan` to the tick.
 *
 * The transpiler emits `dateTime(...)` / `timeSpan(...)` for `new DateTime(...)` / `new TimeSpan(...)`
 * and routes operators (`+ - < > <= >= == !=`) to the methods here.
 *
 * Scope: construction, calendar/component properties, Add* arithmetic, comparison, parameterless
 * `ToString()` (+ a common-pattern `format`), parse of ISO-8601 and the invariant default. Kind
 * (Utc/Local/Unspecified) is not tracked — values are treated as wall-clock, matching `Unspecified`.
 */

import { activePattern } from './culture';

const TICKS_PER_MILLISECOND = 10_000n;
const TICKS_PER_SECOND = 10_000_000n;
const TICKS_PER_MINUTE = 600_000_000n;
const TICKS_PER_HOUR = 36_000_000_000n;
const TICKS_PER_DAY = 864_000_000_000n;

// Ticks of 9999-12-31 23:59:59.9999999 — .NET DateTime.MaxValue.
const MAX_DATETIME_TICKS = 3_155_378_975_999_999_999n;

// Days from 0001-01-01 to 1970-01-01 (Hinnant's algorithm is epoch-relative to 1970).
const DAYS_0001_TO_1970 = 719_162;

function idiv(a: number, b: number): number {
  return Math.trunc(a / b);
}

/** Days since 0001-01-01 (which is day 0), proleptic Gregorian. */
function daysFromCivil(year: number, month: number, day: number): number {
  const y = month <= 2 ? year - 1 : year;
  const era = idiv(y >= 0 ? y : y - 399, 400);
  const yoe = y - era * 400;
  const doy = idiv(153 * (month + (month > 2 ? -3 : 9)) + 2, 5) + day - 1;
  const doe = yoe * 365 + idiv(yoe, 4) - idiv(yoe, 100) + doy;
  return era * 146097 + doe - 719468 + DAYS_0001_TO_1970;
}

/** Inverse of {@link daysFromCivil}. */
function civilFromDays(daysSince0001: number): { year: number; month: number; day: number } {
  const z = daysSince0001 - DAYS_0001_TO_1970 + 719468;
  const era = idiv(z >= 0 ? z : z - 146096, 146097);
  const doe = z - era * 146097;
  const yoe = idiv(doe - idiv(doe, 1460) + idiv(doe, 36524) - idiv(doe, 146096), 365);
  const y = yoe + era * 400;
  const doy = doe - (365 * yoe + idiv(yoe, 4) - idiv(yoe, 100));
  const mp = idiv(5 * doy + 2, 153);
  const day = doy - idiv(153 * mp + 2, 5) + 1;
  const month = mp < 10 ? mp + 3 : mp - 9;
  return { year: month <= 2 ? y + 1 : y, month, day };
}

function isLeapYear(year: number): boolean {
  return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
}

const DAYS_IN_MONTH = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

function daysInMonth(year: number, month: number): number {
  return month === 2 && isLeapYear(year) ? 29 : DAYS_IN_MONTH[month - 1];
}

function pad(n: number, width: number): string {
  const s = Math.abs(n).toString();
  return s.length >= width ? s : '0'.repeat(width - s.length) + s;
}

// ---------------------------------------------------------------------------------------------
// TimeSpan
// ---------------------------------------------------------------------------------------------

export class TimeSpan {
  constructor(readonly ticks: bigint) {}

  // Component parts (whole, truncated toward zero — matching .NET).
  get days(): number {
    return Number(this.ticks / TICKS_PER_DAY);
  }
  get hours(): number {
    return Number((this.ticks / TICKS_PER_HOUR) % 24n);
  }
  get minutes(): number {
    return Number((this.ticks / TICKS_PER_MINUTE) % 60n);
  }
  get seconds(): number {
    return Number((this.ticks / TICKS_PER_SECOND) % 60n);
  }
  get milliseconds(): number {
    return Number((this.ticks / TICKS_PER_MILLISECOND) % 1000n);
  }

  // Fractional totals.
  get totalDays(): number {
    return Number(this.ticks) / Number(TICKS_PER_DAY);
  }
  get totalHours(): number {
    return Number(this.ticks) / Number(TICKS_PER_HOUR);
  }
  get totalMinutes(): number {
    return Number(this.ticks) / Number(TICKS_PER_MINUTE);
  }
  get totalSeconds(): number {
    return Number(this.ticks) / Number(TICKS_PER_SECOND);
  }
  get totalMilliseconds(): number {
    return Number(this.ticks) / Number(TICKS_PER_MILLISECOND);
  }

  add(other: TimeSpan): TimeSpan {
    return new TimeSpan(this.ticks + other.ticks);
  }
  sub(other: TimeSpan): TimeSpan {
    return new TimeSpan(this.ticks - other.ticks);
  }
  negate(): TimeSpan {
    return new TimeSpan(-this.ticks);
  }
  duration(): TimeSpan {
    return new TimeSpan(this.ticks < 0n ? -this.ticks : this.ticks);
  }

  compareTo(other: TimeSpan): number {
    return this.ticks < other.ticks ? -1 : this.ticks > other.ticks ? 1 : 0;
  }
  equals(other: TimeSpan): boolean {
    return this.ticks === other.ticks;
  }

  /** .NET "c" (constant) format: `[-][d.]hh:mm:ss[.fffffff]`. */
  toString(): string {
    const sign = this.ticks < 0n ? '-' : '';
    const abs = this.ticks < 0n ? -this.ticks : this.ticks;
    const d = Number(abs / TICKS_PER_DAY);
    const h = Number((abs / TICKS_PER_HOUR) % 24n);
    const m = Number((abs / TICKS_PER_MINUTE) % 60n);
    const s = Number((abs / TICKS_PER_SECOND) % 60n);
    const frac = abs % TICKS_PER_SECOND;

    let result = sign;
    if (d > 0) result += d + '.';
    result += `${pad(h, 2)}:${pad(m, 2)}:${pad(s, 2)}`;
    if (frac > 0n) result += '.' + frac.toString().padStart(7, '0');
    return result;
  }

  /** System.Text.Json serializes TimeSpan in the "c" format — same as toString. */
  toJSON(): string {
    return this.toString();
  }
}

/**
 * A count that .NET types as `long` and C# lets you write as an int literal. Both cross: a
 * transpiled `long` is a bigint (`TimeSpan.FromSeconds(90)` binds .NET 9's long overload), while a
 * `double` count stays a number. Beyond 2^53 a bigint count loses precision here — the same span is
 * past `TimeSpan.MaxValue` in .NET, which throws, so no representable span is affected.
 */
function asNumber(value: bigint | number): number {
  return typeof value === 'bigint' ? Number(value) : value;
}

function ticksFromUnit(value: bigint | number, ticksPerUnit: bigint): bigint {
  // .NET scales through milliseconds and rounds to the nearest tick.
  return (
    BigInt(Math.round(asNumber(value) * Number(ticksPerUnit / TICKS_PER_MILLISECOND))) *
    TICKS_PER_MILLISECOND
  );
}

export interface TimeSpanFactory {
  (ticks: bigint | number): TimeSpan;
  (hours: number, minutes: number, seconds: number): TimeSpan;
  (days: number, hours: number, minutes: number, seconds: number): TimeSpan;
  (days: number, hours: number, minutes: number, seconds: number, milliseconds: number): TimeSpan;
  fromDays(value: bigint | number): TimeSpan;
  fromHours(value: bigint | number): TimeSpan;
  fromMinutes(value: bigint | number): TimeSpan;
  fromSeconds(value: bigint | number): TimeSpan;
  fromMilliseconds(value: bigint | number): TimeSpan;
  fromTicks(value: bigint | number): TimeSpan;
  parse(text: string): TimeSpan;
  readonly zero: TimeSpan;
  minValue(): TimeSpan;
  maxValue(): TimeSpan;
}

/** Mirrors C# `new TimeSpan(...)` overloads (dispatched by arity) plus the static factories. */
function timeSpanImpl(...args: number[] | bigint[]): TimeSpan {
  if (args.length === 1) {
    const v = args[0];
    return new TimeSpan(typeof v === 'bigint' ? v : BigInt(Math.trunc(v)));
  }
  // (h, m, s) | (d, h, m, s) | (d, h, m, s, ms)
  const n = args as number[];
  let days = 0,
    hours = 0,
    minutes = 0,
    seconds = 0,
    ms = 0;
  if (n.length === 3) [hours, minutes, seconds] = n;
  else if (n.length === 4) [days, hours, minutes, seconds] = n;
  else [days, hours, minutes, seconds, ms] = n;
  const ticks =
    BigInt(days) * TICKS_PER_DAY +
    BigInt(hours) * TICKS_PER_HOUR +
    BigInt(minutes) * TICKS_PER_MINUTE +
    BigInt(seconds) * TICKS_PER_SECOND +
    BigInt(ms) * TICKS_PER_MILLISECOND;
  return new TimeSpan(ticks);
}

// Callable base + attached static factories (fromDays/parse/…): the impl is the overloaded call
// signature and the statics are assigned just below, so the two-step `unknown` cast is required.
export const timeSpan = timeSpanImpl as unknown as TimeSpanFactory;
timeSpan.fromDays = (v) => new TimeSpan(ticksFromUnit(v, TICKS_PER_DAY));
timeSpan.fromHours = (v) => new TimeSpan(ticksFromUnit(v, TICKS_PER_HOUR));
timeSpan.fromMinutes = (v) => new TimeSpan(ticksFromUnit(v, TICKS_PER_MINUTE));
timeSpan.fromSeconds = (v) => new TimeSpan(ticksFromUnit(v, TICKS_PER_SECOND));
timeSpan.fromMilliseconds = (v) =>
  new TimeSpan(BigInt(Math.round(asNumber(v))) * TICKS_PER_MILLISECOND);
timeSpan.fromTicks = (v) => new TimeSpan(typeof v === 'bigint' ? v : BigInt(Math.trunc(v)));
timeSpan.parse = (text: string): TimeSpan => {
  // .NET "c" format: [-][d.]hh:mm:ss[.fffffff]
  const m = /^(-)?(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d+))?$/.exec(text.trim());
  if (!m) throw new Error(`Unrecognized TimeSpan format: '${text}'`);
  const frac = m[6] ? BigInt(m[6].padEnd(7, '0').slice(0, 7)) : 0n;
  const ticks =
    BigInt(m[2] ?? 0) * TICKS_PER_DAY +
    BigInt(m[3]) * TICKS_PER_HOUR +
    BigInt(m[4]) * TICKS_PER_MINUTE +
    BigInt(m[5]) * TICKS_PER_SECOND +
    frac;
  return new TimeSpan(m[1] ? -ticks : ticks);
};
(timeSpan as { zero: TimeSpan }).zero = new TimeSpan(0n);
timeSpan.minValue = () => new TimeSpan(-9_223_372_036_854_775_808n);
timeSpan.maxValue = () => new TimeSpan(9_223_372_036_854_775_807n);

// ---------------------------------------------------------------------------------------------
// DateTime
// ---------------------------------------------------------------------------------------------

export class DateTime {
  constructor(readonly ticks: bigint) {}

  private get totalDays(): number {
    return Number(this.ticks / TICKS_PER_DAY);
  }
  private civil() {
    return civilFromDays(this.totalDays);
  }

  get year(): number {
    return this.civil().year;
  }
  get month(): number {
    return this.civil().month;
  }
  get day(): number {
    return this.civil().day;
  }
  get hour(): number {
    return Number((this.ticks / TICKS_PER_HOUR) % 24n);
  }
  get minute(): number {
    return Number((this.ticks / TICKS_PER_MINUTE) % 60n);
  }
  get second(): number {
    return Number((this.ticks / TICKS_PER_SECOND) % 60n);
  }
  get millisecond(): number {
    return Number((this.ticks / TICKS_PER_MILLISECOND) % 1000n);
  }
  /** Sunday = 0 (matches System.DayOfWeek). 0001-01-01 is a Monday. */
  get dayOfWeek(): number {
    return Number((BigInt(this.totalDays) + 1n) % 7n);
  }
  get dayOfYear(): number {
    const { year } = this.civil();
    return this.totalDays - daysFromCivil(year, 1, 1) + 1;
  }
  get date(): DateTime {
    return new DateTime(BigInt(this.totalDays) * TICKS_PER_DAY);
  }
  get timeOfDay(): TimeSpan {
    return new TimeSpan(this.ticks % TICKS_PER_DAY);
  }

  addTicks(t: bigint): DateTime {
    return new DateTime(this.ticks + t);
  }
  // Fractional Add* round to the nearest millisecond, like .NET.
  addDays(value: number): DateTime {
    return this.addTicks(ticksFromUnit(value, TICKS_PER_DAY));
  }
  addHours(value: number): DateTime {
    return this.addTicks(ticksFromUnit(value, TICKS_PER_HOUR));
  }
  addMinutes(value: number): DateTime {
    return this.addTicks(ticksFromUnit(value, TICKS_PER_MINUTE));
  }
  addSeconds(value: number): DateTime {
    return this.addTicks(ticksFromUnit(value, TICKS_PER_SECOND));
  }
  addMilliseconds(value: number): DateTime {
    return this.addTicks(BigInt(Math.round(value)) * TICKS_PER_MILLISECOND);
  }

  addMonths(months: number): DateTime {
    const c = this.civil();
    const i = c.month - 1 + months;
    let year: number, month: number;
    if (i >= 0) {
      month = (i % 12) + 1;
      year = c.year + idiv(i, 12);
    } else {
      month = 12 + ((i + 1) % 12);
      year = c.year + idiv(i - 11, 12);
    }
    const day = Math.min(c.day, daysInMonth(year, month));
    const datePart = BigInt(daysFromCivil(year, month, day)) * TICKS_PER_DAY;
    return new DateTime(datePart + (this.ticks % TICKS_PER_DAY));
  }
  addYears(years: number): DateTime {
    return this.addMonths(years * 12);
  }

  /** DateTime - DateTime -> TimeSpan. */
  diff(other: DateTime): TimeSpan {
    return new TimeSpan(this.ticks - other.ticks);
  }
  /** DateTime + TimeSpan -> DateTime. */
  add(span: TimeSpan): DateTime {
    return new DateTime(this.ticks + span.ticks);
  }
  /** DateTime - TimeSpan -> DateTime. */
  subtract(span: TimeSpan): DateTime {
    return new DateTime(this.ticks - span.ticks);
  }

  compareTo(other: DateTime): number {
    return this.ticks < other.ticks ? -1 : this.ticks > other.ticks ? 1 : 0;
  }
  equals(other: DateTime): boolean {
    return this.ticks === other.ticks;
  }

  /** .NET invariant default: `MM/dd/yyyy HH:mm:ss`. With a pattern, see {@link format}. */
  toString(pattern?: string): string {
    if (pattern) return this.format(pattern);
    const c = this.civil();
    return `${pad(c.month, 2)}/${pad(c.day, 2)}/${pad(c.year, 4)} ${pad(this.hour, 2)}:${pad(this.minute, 2)}:${pad(this.second, 2)}`;
  }

  /** Common custom format tokens: yyyy yy MM M dd d HH H mm m ss s. Literals pass through. */
  format(pattern: string): string {
    const c = this.civil();
    const map: Record<string, string> = {
      yyyy: pad(c.year, 4),
      yy: pad(c.year % 100, 2),
      MM: pad(c.month, 2),
      M: String(c.month),
      dd: pad(c.day, 2),
      d: String(c.day),
      HH: pad(this.hour, 2),
      H: String(this.hour),
      mm: pad(this.minute, 2),
      m: String(this.minute),
      ss: pad(this.second, 2),
      s: String(this.second),
    };
    return pattern.replace(/yyyy|yy|MM|M|dd|d|HH|H|mm|m|ss|s/g, (t) => map[t]);
  }

  /** ISO-8601 (what System.Text.Json reads). Fractional ticks appended only when non-zero. */
  toJSON(): string {
    const c = this.civil();
    let s = `${pad(c.year, 4)}-${pad(c.month, 2)}-${pad(c.day, 2)}T${pad(this.hour, 2)}:${pad(this.minute, 2)}:${pad(this.second, 2)}`;
    const frac = this.ticks % TICKS_PER_SECOND;
    if (frac > 0n) s += '.' + frac.toString().padStart(7, '0').replace(/0+$/, '');
    return s;
  }
}

export interface DateTimeFactory {
  (ticks: bigint): DateTime;
  (year: number, month: number, day: number): DateTime;
  (
    year: number,
    month: number,
    day: number,
    hour: number,
    minute: number,
    second: number,
  ): DateTime;
  (
    year: number,
    month: number,
    day: number,
    hour: number,
    minute: number,
    second: number,
    millisecond: number,
  ): DateTime;
  now(): DateTime;
  utcNow(): DateTime;
  today(): DateTime;
  minValue(): DateTime;
  maxValue(): DateTime;
  parse(text: string): DateTime;
  daysInMonth(year: number, month: number): number;
  isLeapYear(year: number): boolean;
}

function fromComponents(
  year: number,
  month: number,
  day: number,
  hour = 0,
  minute = 0,
  second = 0,
  ms = 0,
): DateTime {
  const ticks =
    BigInt(daysFromCivil(year, month, day)) * TICKS_PER_DAY +
    BigInt(hour) * TICKS_PER_HOUR +
    BigInt(minute) * TICKS_PER_MINUTE +
    BigInt(second) * TICKS_PER_SECOND +
    BigInt(ms) * TICKS_PER_MILLISECOND;
  return new DateTime(ticks);
}

/** Mirrors C# `new DateTime(...)` overloads (dispatched by arity) plus the static members. */
function dateTimeImpl(...args: number[] | [bigint]): DateTime {
  if (args.length === 1 && typeof args[0] === 'bigint') return new DateTime(args[0]);
  const n = args as number[];
  return fromComponents(n[0], n[1], n[2], n[3] ?? 0, n[4] ?? 0, n[5] ?? 0, n[6] ?? 0);
}

function fromJsDate(d: Date, utc: boolean): DateTime {
  return utc
    ? fromComponents(
        d.getUTCFullYear(),
        d.getUTCMonth() + 1,
        d.getUTCDate(),
        d.getUTCHours(),
        d.getUTCMinutes(),
        d.getUTCSeconds(),
        d.getUTCMilliseconds(),
      )
    : fromComponents(
        d.getFullYear(),
        d.getMonth() + 1,
        d.getDate(),
        d.getHours(),
        d.getMinutes(),
        d.getSeconds(),
        d.getMilliseconds(),
      );
}

export const dateTime = dateTimeImpl as DateTimeFactory;
dateTime.now = () => fromJsDate(new Date(), false);
dateTime.utcNow = () => fromJsDate(new Date(), true);
dateTime.today = () => dateTime.now().date;
dateTime.minValue = () => new DateTime(0n);
dateTime.maxValue = () => new DateTime(MAX_DATETIME_TICKS);
dateTime.daysInMonth = daysInMonth;
dateTime.isLeapYear = isLeapYear;
dateTime.parse = (text: string): DateTime => {
  const t = text.trim();
  // ISO-8601: yyyy-MM-dd[Thh:mm:ss[.fffffff]] (the wire form).
  let m = /^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?)?/.exec(t);
  if (m) {
    const frac = m[7] ? BigInt(m[7].padEnd(7, '0').slice(0, 7)) : 0n;
    return new DateTime(
      fromComponents(+m[1], +m[2], +m[3], +(m[4] ?? 0), +(m[5] ?? 0), +(m[6] ?? 0)).ticks + frac,
    );
  }
  // Invariant default: MM/dd/yyyy[ HH:mm:ss]
  m = /^(\d{1,2})\/(\d{1,2})\/(\d{4})(?:\s+(\d{1,2}):(\d{2}):(\d{2}))?/.exec(t);
  if (m) return fromComponents(+m[3], +m[1], +m[2], +(m[4] ?? 0), +(m[5] ?? 0), +(m[6] ?? 0));
  throw new Error(`Unrecognized DateTime format: '${text}'`);
};

// ---------------------------------------------------------------------------------------------
// DateOnly (.NET 6+) — a calendar date with no time-of-day.
// ---------------------------------------------------------------------------------------------

export class DateOnly {
  /** Days since 0001-01-01 (0001-01-01 = 0), matching .NET DateOnly.DayNumber. */
  constructor(readonly dayNumber: number) {}

  private civil() {
    return civilFromDays(this.dayNumber);
  }

  get year(): number {
    return this.civil().year;
  }
  get month(): number {
    return this.civil().month;
  }
  get day(): number {
    return this.civil().day;
  }
  get dayOfWeek(): number {
    return Number((BigInt(this.dayNumber) + 1n) % 7n);
  } // Sunday = 0
  get dayOfYear(): number {
    return this.dayNumber - daysFromCivil(this.civil().year, 1, 1) + 1;
  }

  addDays(value: number): DateOnly {
    return new DateOnly(this.dayNumber + Math.trunc(value));
  }
  addMonths(months: number): DateOnly {
    const c = this.civil();
    const i = c.month - 1 + months;
    let year: number, month: number;
    if (i >= 0) {
      month = (i % 12) + 1;
      year = c.year + idiv(i, 12);
    } else {
      month = 12 + ((i + 1) % 12);
      year = c.year + idiv(i - 11, 12);
    }
    return dateOnly(year, month, Math.min(c.day, daysInMonth(year, month)));
  }
  addYears(years: number): DateOnly {
    return this.addMonths(years * 12);
  }

  compareTo(other: DateOnly): number {
    return this.dayNumber < other.dayNumber ? -1 : this.dayNumber > other.dayNumber ? 1 : 0;
  }
  equals(other: DateOnly): boolean {
    return this.dayNumber === other.dayNumber;
  }

  /** .NET invariant short date: `MM/dd/yyyy`. */
  toString(pattern?: string): string {
    const c = this.civil();
    if (pattern) {
      const map: Record<string, string> = {
        yyyy: pad(c.year, 4),
        yy: pad(c.year % 100, 2),
        MM: pad(c.month, 2),
        M: String(c.month),
        dd: pad(c.day, 2),
        d: String(c.day),
      };
      return pattern.replace(/yyyy|yy|MM|M|dd|d/g, (t) => map[t]);
    }
    return `${pad(c.month, 2)}/${pad(c.day, 2)}/${pad(c.year, 4)}`;
  }

  /** ISO `yyyy-MM-dd` (what System.Text.Json reads/writes for DateOnly). */
  toJSON(): string {
    const c = this.civil();
    return `${pad(c.year, 4)}-${pad(c.month, 2)}-${pad(c.day, 2)}`;
  }
}

export interface DateOnlyFactory {
  (year: number, month: number, day: number): DateOnly;
  fromDayNumber(dayNumber: number): DateOnly;
  fromDateTime(dt: DateTime): DateOnly;
  minValue(): DateOnly;
  maxValue(): DateOnly;
  parse(text: string): DateOnly;
  /** The date a reader TYPED, by the active culture's field order, or null when it is not one. */
  tryParse(text: string): DateOnly | null;
}

export const dateOnly = ((year: number, month: number, day: number) =>
  new DateOnly(daysFromCivil(year, month, day))) as DateOnlyFactory;
dateOnly.fromDayNumber = (n) => new DateOnly(n);
dateOnly.fromDateTime = (dt) => new DateOnly(Number(dt.ticks / TICKS_PER_DAY));
dateOnly.minValue = () => new DateOnly(0);
dateOnly.maxValue = () => new DateOnly(daysFromCivil(9999, 12, 31));
dateOnly.parse = (text: string): DateOnly => {
  const parsed = tryParseDateOnly(text);
  if (parsed === null) throw new Error(`Unrecognized DateOnly format: '${text}'`);
  return parsed;
};

/**
 * A typed date, by the ACTIVE CULTURE's field order — the half of parsing that cannot be guessed.
 * `07/08` is July 8th to a reader in Chicago and August 7th to one in São Paulo, and a parser that
 * picks one silently is wrong twelve times a year for half the world.
 *
 * The order comes from the culture's own short-date pattern (`$dateShort`, shipped beside the
 * string catalog — see culture.ts), which is `DateTimeFormatInfo`'s, so the client reads a typed
 * date exactly as the server would. ISO is accepted everywhere: it is unambiguous by construction
 * and it is what a date input hands over.
 */
function tryParseDateOnly(text: string): DateOnly | null {
  const t = text.trim();
  if (t.length === 0) return null;

  const iso = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(t);
  if (iso) return valid(+iso[1], +iso[2], +iso[3]);

  const parts = /^(\d{1,4})[/.-](\d{1,2})[/.-](\d{1,4})$/.exec(t);
  if (!parts) return null;

  // Which slot holds what, read off the pattern: "M/d/yyyy" is month-first, "dd/MM/yyyy" is
  // day-first. Without a catalog (a page with no server behind it) the invariant order applies,
  // which is what .NET's own invariant culture does.
  const pattern = (activePattern('dateShort') ?? 'M/d/yyyy').toLowerCase();
  const dayAt = pattern.indexOf('d');
  const monthAt = pattern.indexOf('m');
  const dayFirst = dayAt >= 0 && monthAt >= 0 && dayAt < monthAt;

  const [, one, two, three] = parts;
  // A four-digit part is the year wherever it sits — yyyy/MM/dd is a pattern too.
  if (one.length === 4) return valid(+one, dayFirst ? +three : +two, dayFirst ? +two : +three);
  return valid(+three, dayFirst ? +two : +one, dayFirst ? +one : +two);
}

/** The date, or null when those numbers are not one — 31 February parses and does not exist. */
function valid(year: number, month: number, day: number): DateOnly | null {
  if (month < 1 || month > 12 || day < 1 || year < 1 || year > 9999) return null;
  const built = dateOnly(year, month, day);
  return built.year === year && built.month === month && built.day === day ? built : null;
}

dateOnly.tryParse = tryParseDateOnly;

// ---------------------------------------------------------------------------------------------
// TimeOnly (.NET 6+) — a time of day with no date. Backed by ticks in [0, TicksPerDay).
// ---------------------------------------------------------------------------------------------

export class TimeOnly {
  constructor(readonly ticks: bigint) {}

  get hour(): number {
    return Number((this.ticks / TICKS_PER_HOUR) % 24n);
  }
  get minute(): number {
    return Number((this.ticks / TICKS_PER_MINUTE) % 60n);
  }
  get second(): number {
    return Number((this.ticks / TICKS_PER_SECOND) % 60n);
  }
  get millisecond(): number {
    return Number((this.ticks / TICKS_PER_MILLISECOND) % 1000n);
  }

  /** Wraps within a 24h day, matching .NET TimeOnly.Add*. */
  private wrap(deltaTicks: bigint): TimeOnly {
    let t = (this.ticks + deltaTicks) % TICKS_PER_DAY;
    if (t < 0n) t += TICKS_PER_DAY;
    return new TimeOnly(t);
  }
  addHours(value: number): TimeOnly {
    return this.wrap(ticksFromUnit(value, TICKS_PER_HOUR));
  }
  addMinutes(value: number): TimeOnly {
    return this.wrap(ticksFromUnit(value, TICKS_PER_MINUTE));
  }

  compareTo(other: TimeOnly): number {
    return this.ticks < other.ticks ? -1 : this.ticks > other.ticks ? 1 : 0;
  }
  equals(other: TimeOnly): boolean {
    return this.ticks === other.ticks;
  }

  /** .NET invariant short time: `HH:mm`. */
  toString(): string {
    return `${pad(this.hour, 2)}:${pad(this.minute, 2)}`;
  }

  /** ISO `HH:mm:ss[.fffffff]` (what System.Text.Json reads/writes for TimeOnly). */
  toJSON(): string {
    let s = `${pad(this.hour, 2)}:${pad(this.minute, 2)}:${pad(this.second, 2)}`;
    const frac = this.ticks % TICKS_PER_SECOND;
    if (frac > 0n) s += '.' + frac.toString().padStart(7, '0').replace(/0+$/, '');
    return s;
  }
}

export interface TimeOnlyFactory {
  (hour: number, minute: number): TimeOnly;
  (hour: number, minute: number, second: number): TimeOnly;
  (hour: number, minute: number, second: number, millisecond: number): TimeOnly;
  (ticks: bigint): TimeOnly;
  fromTimeSpan(span: TimeSpan): TimeOnly;
  /** The time of day of a moment — .NET's TimeOnly.FromDateTime. */
  fromDateTime(value: DateTime): TimeOnly;
  minValue(): TimeOnly;
  maxValue(): TimeOnly;
  parse(text: string): TimeOnly;
}

function timeOnlyImpl(...args: number[] | [bigint]): TimeOnly {
  if (args.length === 1 && typeof args[0] === 'bigint') return new TimeOnly(args[0]);
  const n = args as number[];
  const ticks =
    BigInt(n[0]) * TICKS_PER_HOUR +
    BigInt(n[1]) * TICKS_PER_MINUTE +
    BigInt(n[2] ?? 0) * TICKS_PER_SECOND +
    BigInt(n[3] ?? 0) * TICKS_PER_MILLISECOND;
  return new TimeOnly(ticks);
}

export const timeOnly = timeOnlyImpl as TimeOnlyFactory;
timeOnly.fromTimeSpan = (span) =>
  new TimeOnly(((span.ticks % TICKS_PER_DAY) + TICKS_PER_DAY) % TICKS_PER_DAY);
timeOnly.fromDateTime = (value: DateTime) =>
  new TimeOnly(((value.ticks % TICKS_PER_DAY) + TICKS_PER_DAY) % TICKS_PER_DAY);
timeOnly.minValue = () => new TimeOnly(0n);
timeOnly.maxValue = () => new TimeOnly(TICKS_PER_DAY - 1n);
timeOnly.parse = (text: string): TimeOnly => {
  const m = /^(\d{1,2}):(\d{2})(?::(\d{2})(?:\.(\d+))?)?/.exec(text.trim());
  if (!m) throw new Error(`Unrecognized TimeOnly format: '${text}'`);
  const frac = m[4] ? BigInt(m[4].padEnd(7, '0').slice(0, 7)) : 0n;
  return new TimeOnly(
    BigInt(+m[1]) * TICKS_PER_HOUR +
      BigInt(+m[2]) * TICKS_PER_MINUTE +
      BigInt(+(m[3] ?? 0)) * TICKS_PER_SECOND +
      frac,
  );
};

// ---------------------------------------------------------------------------------------------
// DateTimeOffset — a wall-clock DateTime plus an offset from UTC. Compared/equated by the instant.
// ---------------------------------------------------------------------------------------------

const UNIX_EPOCH_TICKS = BigInt(daysFromCivil(1970, 1, 1)) * TICKS_PER_DAY;

function offsetString(offsetTicks: bigint): string {
  const neg = offsetTicks < 0n;
  const abs = neg ? -offsetTicks : offsetTicks;
  return `${neg ? '-' : '+'}${pad(Number(abs / TICKS_PER_HOUR), 2)}:${pad(Number((abs / TICKS_PER_MINUTE) % 60n), 2)}`;
}

export class DateTimeOffset {
  /** @param localTicks wall-clock ticks; @param offsetTicks offset from UTC in ticks. */
  constructor(
    readonly localTicks: bigint,
    readonly offsetTicks: bigint,
  ) {}

  /** The instant in UTC ticks (local − offset). Comparison/equality use this. */
  get utcTicks(): bigint {
    return this.localTicks - this.offsetTicks;
  }

  private civil() {
    return civilFromDays(Number(this.localTicks / TICKS_PER_DAY));
  }
  get year(): number {
    return this.civil().year;
  }
  get month(): number {
    return this.civil().month;
  }
  get day(): number {
    return this.civil().day;
  }
  get hour(): number {
    return Number((this.localTicks / TICKS_PER_HOUR) % 24n);
  }
  get minute(): number {
    return Number((this.localTicks / TICKS_PER_MINUTE) % 60n);
  }
  get second(): number {
    return Number((this.localTicks / TICKS_PER_SECOND) % 60n);
  }
  get millisecond(): number {
    return Number((this.localTicks / TICKS_PER_MILLISECOND) % 1000n);
  }
  get dayOfWeek(): number {
    return Number((BigInt(Number(this.localTicks / TICKS_PER_DAY)) + 1n) % 7n);
  }
  get ticks(): bigint {
    return this.localTicks;
  }
  get offset(): TimeSpan {
    return new TimeSpan(this.offsetTicks);
  }
  get dateTime(): DateTime {
    return new DateTime(this.localTicks);
  }
  get localDateTime(): DateTime {
    return new DateTime(this.localTicks);
  }
  get utcDateTime(): DateTime {
    return new DateTime(this.utcTicks);
  }
  get date(): DateTime {
    return new DateTime(BigInt(Number(this.localTicks / TICKS_PER_DAY)) * TICKS_PER_DAY);
  }
  get timeOfDay(): TimeSpan {
    return new TimeSpan(this.localTicks % TICKS_PER_DAY);
  }

  addTicks(t: bigint): DateTimeOffset {
    return new DateTimeOffset(this.localTicks + t, this.offsetTicks);
  }
  addDays(v: number): DateTimeOffset {
    return this.addTicks(ticksFromUnit(v, TICKS_PER_DAY));
  }
  addHours(v: number): DateTimeOffset {
    return this.addTicks(ticksFromUnit(v, TICKS_PER_HOUR));
  }
  addMinutes(v: number): DateTimeOffset {
    return this.addTicks(ticksFromUnit(v, TICKS_PER_MINUTE));
  }
  addSeconds(v: number): DateTimeOffset {
    return this.addTicks(ticksFromUnit(v, TICKS_PER_SECOND));
  }
  addMonths(months: number): DateTimeOffset {
    return new DateTimeOffset(
      new DateTime(this.localTicks).addMonths(months).ticks,
      this.offsetTicks,
    );
  }
  addYears(years: number): DateTimeOffset {
    return this.addMonths(years * 12);
  }

  /** Same instant, expressed at a different offset. */
  toOffset(offset: TimeSpan): DateTimeOffset {
    return new DateTimeOffset(this.utcTicks + offset.ticks, offset.ticks);
  }

  diff(other: DateTimeOffset): TimeSpan {
    return new TimeSpan(this.utcTicks - other.utcTicks);
  }
  add(span: TimeSpan): DateTimeOffset {
    return this.addTicks(span.ticks);
  }
  subtract(span: TimeSpan): DateTimeOffset {
    return this.addTicks(-span.ticks);
  }

  compareTo(other: DateTimeOffset): number {
    return this.utcTicks < other.utcTicks ? -1 : this.utcTicks > other.utcTicks ? 1 : 0;
  }
  equals(other: DateTimeOffset): boolean {
    return this.utcTicks === other.utcTicks;
  }

  toUnixTimeSeconds(): number {
    return Number((this.utcTicks - UNIX_EPOCH_TICKS) / TICKS_PER_SECOND);
  }
  toUnixTimeMilliseconds(): number {
    return Number((this.utcTicks - UNIX_EPOCH_TICKS) / TICKS_PER_MILLISECOND);
  }

  /** .NET invariant: `MM/dd/yyyy HH:mm:ss zzz`. */
  toString(): string {
    return `${new DateTime(this.localTicks).toString()} ${offsetString(this.offsetTicks)}`;
  }
  /** ISO-8601 with offset, e.g. `2024-01-15T13:30:00-03:00`. */
  toJSON(): string {
    return `${new DateTime(this.localTicks).toJSON()}${offsetString(this.offsetTicks)}`;
  }
}

export interface DateTimeOffsetFactory {
  (dateTime: DateTime, offset: TimeSpan): DateTimeOffset;
  (ticks: bigint, offset: TimeSpan): DateTimeOffset;
  (
    year: number,
    month: number,
    day: number,
    hour: number,
    minute: number,
    second: number,
    offset: TimeSpan,
  ): DateTimeOffset;
  fromUnixTimeSeconds(seconds: bigint | number): DateTimeOffset;
  fromUnixTimeMilliseconds(ms: bigint | number): DateTimeOffset;
  now(): DateTimeOffset;
  utcNow(): DateTimeOffset;
  parse(text: string): DateTimeOffset;
}

function dateTimeOffsetImpl(...args: unknown[]): DateTimeOffset {
  if (args.length === 2 && args[0] instanceof DateTime)
    return new DateTimeOffset(args[0].ticks, (args[1] as TimeSpan).ticks);
  if (args.length === 2 && typeof args[0] === 'bigint')
    return new DateTimeOffset(args[0], (args[1] as TimeSpan).ticks);
  if (args.length === 1 && args[0] instanceof DateTime)
    return new DateTimeOffset(args[0].ticks, 0n); // Unspecified → UTC
  const n = args as unknown[];
  const local = fromComponents(
    n[0] as number,
    n[1] as number,
    n[2] as number,
    n[3] as number,
    n[4] as number,
    n[5] as number,
  );
  return new DateTimeOffset(local.ticks, (n[6] as TimeSpan).ticks);
}

export const dateTimeOffset = dateTimeOffsetImpl as DateTimeOffsetFactory;
/** A `long` count kept EXACT — these two take a long in .NET, so a bigint must not round-trip
 * through a double on its way to ticks. */
function asBigInt(value: bigint | number): bigint {
  return typeof value === 'bigint' ? value : BigInt(Math.trunc(value));
}
dateTimeOffset.fromUnixTimeSeconds = (s) =>
  new DateTimeOffset(UNIX_EPOCH_TICKS + asBigInt(s) * TICKS_PER_SECOND, 0n);
dateTimeOffset.fromUnixTimeMilliseconds = (ms) =>
  new DateTimeOffset(UNIX_EPOCH_TICKS + asBigInt(ms) * TICKS_PER_MILLISECOND, 0n);
dateTimeOffset.now = () => new DateTimeOffset(dateTime.now().ticks, 0n);
dateTimeOffset.utcNow = () => new DateTimeOffset(dateTime.utcNow().ticks, 0n);
dateTimeOffset.parse = (text: string): DateTimeOffset => {
  const t = text.trim();
  // yyyy-MM-ddTHH:mm:ss[.fff][(+|-)HH:mm | Z]
  const m =
    /^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?\s*(Z|[+-]\d{2}:?\d{2})?$/.exec(
      t,
    );
  if (!m) throw new Error(`Unrecognized DateTimeOffset format: '${text}'`);
  const frac = m[7] ? BigInt(m[7].padEnd(7, '0').slice(0, 7)) : 0n;
  const local = fromComponents(+m[1], +m[2], +m[3], +m[4], +m[5], +m[6]).ticks + frac;
  let offsetTicks = 0n;
  if (m[8] && m[8] !== 'Z') {
    const neg = m[8][0] === '-';
    const digits = m[8].slice(1).replace(':', '');
    offsetTicks =
      (BigInt(digits.slice(0, 2)) * TICKS_PER_HOUR + BigInt(digits.slice(2)) * TICKS_PER_MINUTE) *
      (neg ? -1n : 1n);
  }
  return new DateTimeOffset(local, offsetTicks);
};
