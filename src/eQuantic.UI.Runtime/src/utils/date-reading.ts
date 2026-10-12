/**
 * A date, a time of day and a zone read from text in a culture, as .NET 10's `DateTimeParse` reads the
 * numeric forms a culture writes (#806). The twins read the invariant shapes whatever culture the page
 * was in, so `01/02/2024` was the 2nd of January on a pt-BR page, where .NET reads the 1st of
 * February, and a value written with `ToString()` did not read back.
 *
 * What a culture changes, measured on .NET 10 in nine cultures: the ORDER of the day, the month and
 * the year (its short date pattern's), the separator of its date and of its clock beside the ones
 * every culture reads (`/`, `-` and `.`; `:`), its AM and PM designators beside `AM` and `PM`, a
 * literal its date ends with (ko-KR's point), and the marks of right-to-left direction ar writes after
 * a number. A culture that writes its clock with points (fi-FI, da-DK, id-ID) reads three short
 * numbers joined by points as a clock, `10.11.12` being 10:11:12 there and the 10th of November 2012
 * in de-DE. What it does not change: a fraction of a second is written with a point or a comma in
 * every culture, and AM and PM are read in every one.
 *
 * The forms are the numeric ones: three numbers for a date, a clock of one to three parts, a
 * designator, a zone, and the ISO form, a date whose year comes first, a `T` and a clock of two digits
 * a part. A month's name, a date of two numbers and a clock with no date as today's are .NET's and
 * not read here yet (#807).
 */
import type { DateTimeFormatData } from './culture';
import { round } from './dotnet-math';
import { isWhiteSpace } from './white-space';

/** A calendar date, as the text names it: not yet checked to exist. */
export interface ReadDate {
  year: number;
  month: number;
  day: number;
}

/** A time of day, as the text names it, the designator already applied. */
export interface ReadClock {
  hour: number;
  minute: number;
  second: number;
  /** The fraction of a second, in ticks, rounded as .NET rounds it. */
  fraction: number;
}

/** What a text names: a date, a clock and a zone, each where the text has one. */
export interface Moment {
  date: ReadDate | null;
  clock: ReadClock | null;
  /** `utc` for a `Z`, or the offset east of UTC in ticks. */
  zone: 'utc' | bigint | null;
}

/**
 * Why a text names no moment, each a message of its own in .NET: no shape it reads (`format`), a word
 * it does not know at an index (`word`), a clock that does not exist (`clock`, which .NET words as a
 * time its calendar does not support), and an offset past fourteen hours (`zone`).
 */
export type Refusal =
  | { refused: 'format' }
  | { refused: 'word'; index: number }
  | { refused: 'clock' }
  | { refused: 'zone' };

const TICKS_PER_HOUR = 36_000_000_000n;
const TICKS_PER_MINUTE = 600_000_000n;

/** U+200F RIGHT-TO-LEFT MARK, which ar writes after each number of a date and .NET reads past there. */
const RLM = '\u200f';

/** U+200E LEFT-TO-RIGHT MARK, which a culture's separator may carry and the reader compares without. */
const LRM = '\u200e';

/** The refusal of a text no shape reads, which .NET words plainly. */
const FORMAT: Refusal = { refused: 'format' };

/** A culture's separators and designators, as the reader compares them. */
interface Shapes {
  dateSeparators: string[];
  timeSeparators: string[];
  /**
   * Whether the culture writes a point between the parts of its clock (fi-FI, da-DK, id-ID): three
   * numbers of two digits or fewer joined by points are then a clock, where .NET reads them as one.
   */
  pointIsClock: boolean;
  /** Each designator and what it says, the longest first, so `ap.` is tried before `AM` would be. */
  designators: { text: string; pm: boolean }[];
  /** What the short date pattern writes after its last part (ko-KR's `.`), or nothing. */
  dateEnd: string;
  pattern: string;
}

function shapesOf(culture: DateTimeFormatData): Shapes {
  const own = (separator: string) => separator.split(RLM).join('').split(LRM).join('').trim();
  const designators = [
    { text: culture.amDesignator, pm: false },
    { text: culture.pmDesignator, pm: true },
    { text: 'AM', pm: false },
    { text: 'PM', pm: true },
  ]
    .filter((designator) => designator.text.length > 0)
    .sort((a, b) => b.text.length - a.text.length);
  const pattern = culture.shortDatePattern;
  const last = Math.max(
    pattern.lastIndexOf('d'),
    pattern.lastIndexOf('M'),
    pattern.lastIndexOf('y'),
  );
  const dateSeparator = own(culture.dateSeparator);
  const pointIsClock = own(culture.timeSeparator) === '.';
  // A point separates a date in every culture but one whose clock it separates and whose date it does
  // not: id-ID refuses `10.11.2012`, where fi-FI, which writes its date with points too, reads it.
  const point = pointIsClock && dateSeparator !== '.' ? [] : ['.'];
  return {
    dateSeparators: ['/', '-', ...point, dateSeparator].filter((s) => s.length > 0),
    timeSeparators: [':', own(culture.timeSeparator)].filter((s) => s.length > 0),
    pointIsClock,
    designators,
    dateEnd: own(pattern.slice(last + 1).replace(/'/g, '')),
    pattern,
  };
}

/** The reader's place in a text. */
class Cursor {
  at = 0;
  constructor(readonly text: string) {}

  get done(): boolean {
    return this.at >= this.text.length;
  }

  /** White space, as .NET's `char.IsWhiteSpace` reads it; whether there was any. */
  space(): boolean {
    const start = this.at;
    while (!this.done && isWhiteSpace(this.text[this.at])) this.at++;
    return this.at > start;
  }

  /** One of `options` here, the first that matches, consumed. */
  take(options: readonly string[]): string | null {
    for (const option of options) {
      if (this.text.startsWith(option, this.at)) {
        this.at += option.length;
        return option;
      }
    }
    return null;
  }

  /** The ASCII digits here, the only ones .NET reads, and the marks of direction after them. */
  digits(): string | null {
    const start = this.at;
    while (!this.done && this.text[this.at] >= '0' && this.text[this.at] <= '9') this.at++;
    if (this.at === start) return null;
    const read = this.text.slice(start, this.at);
    while (this.text[this.at] === RLM) this.at++;
    return read;
  }
}

/** Whether a character is a letter, which is where .NET reports a word it does not know. */
function isLetter(character: string | undefined): boolean {
  return character !== undefined && /\p{L}/u.test(character);
}

/** Whether a reader's answer is a refusal. */
function refused<T extends object>(read: T | Refusal): read is Refusal {
  return 'refused' in read;
}

/**
 * The moment a text names, read in `culture`, or why it names none: a date, then a clock and a zone,
 * or a clock and a zone, then a date, as .NET's lexer takes them in either order; or a date whose year
 * is written first, a `T` and the ISO clock.
 */
export function readMoment(text: string, culture: DateTimeFormatData): Moment | Refusal {
  const shapes = shapesOf(culture);
  const cursor = new Cursor(text);
  cursor.space();

  let date: ReadDate | null = null;
  const leading = readDate(cursor, shapes);
  if (leading === 'invalid') return FORMAT;
  if (leading !== 'none') {
    date = leading.date;
    const before = cursor.at;
    cursor.space();
    if (leading.yearFirst && (text[cursor.at] === 'T' || text[cursor.at] === 't')) {
      cursor.at++;
      return readIso(cursor, date);
    }
    cursor.at = before;
  }

  cursor.space();
  const raw = readClockAndDesignator(cursor, shapes);
  if (raw === 'invalid') return FORMAT;

  let zone: Moment['zone'] = null;
  const readZoneHere = (): Refusal | null => {
    const before = cursor.at;
    cursor.space();
    const read = readZone(cursor);
    if (read === null) cursor.at = before;
    else if (typeof read === 'object') return read;
    else zone = read;
    return null;
  };
  if (date !== null || raw !== null) {
    const refusal = readZoneHere();
    if (refusal) return refusal;
  }

  // A date after the clock, and a zone after it where none came before.
  if (date === null && raw !== null) {
    const before = cursor.at;
    cursor.space();
    const trailing = readDate(cursor, shapes);
    if (trailing === 'invalid') return FORMAT;
    if (trailing === 'none') cursor.at = before;
    else {
      date = trailing.date;
      if (zone === null) {
        const refusal = readZoneHere();
        if (refusal) return refusal;
      }
    }
  }

  // A designator after the zone, where none stood beside the clock.
  if (raw !== null && raw.pm === null) {
    const before = cursor.at;
    cursor.space();
    raw.pm = readDesignator(cursor, shapes);
    if (raw.pm === null) cursor.at = before;
  }

  cursor.space();
  if (!cursor.done || (date === null && raw === null)) {
    // A word is named where .NET's lexer meets it, before a clock out of range is.
    return isLetter(text[cursor.at]) ? { refused: 'word', index: cursor.at } : FORMAT;
  }
  const clock = raw === null ? null : clockOf(raw);
  if (clock !== null && refused(clock)) return clock;
  return { date, clock, zone };
}

/**
 * The rest of an ISO text after its `T`, as .NET's `ParseISO8601` reads it: hours and minutes of two
 * digits each and seconds of two, joined by colons with white space allowed beside them, a fraction
 * after the seconds, then a zone. No designator, and nothing after: what is left is the plain refusal.
 */
function readIso(cursor: Cursor, date: ReadDate): Moment | Refusal {
  const two = (): number | null => {
    cursor.space();
    const digits = cursor.digits();
    return digits !== null && digits.length === 2 ? +digits : null;
  };
  const colon = (): boolean => {
    const before = cursor.at;
    cursor.space();
    if (cursor.text[cursor.at] === ':') {
      cursor.at++;
      return true;
    }
    cursor.at = before;
    return false;
  };
  const hour = two();
  if (hour === null || !colon()) return FORMAT;
  const minute = two();
  if (minute === null) return FORMAT;
  let second = 0;
  let fraction = 0;
  if (colon()) {
    const read = two();
    if (read === null) return FORMAT;
    second = read;
    const mark = cursor.text[cursor.at];
    if (mark === '.' || mark === ',') {
      cursor.at++;
      const digits = cursor.digits();
      if (digits === null) return FORMAT;
      fraction = fractionTicks(digits);
    }
  }
  cursor.space();
  const zone = readZone(cursor);
  if (zone !== null && typeof zone === 'object') return zone;
  cursor.space();
  if (!cursor.done) return FORMAT;
  if (hour > 23 || minute > 59 || second > 59) return { refused: 'clock' };
  return { date, clock: { hour, minute, second, fraction }, zone };
}

/**
 * A date of three numbers: `none` where the text has none here, which leaves the cursor where it was,
 * `invalid` where its numbers make no date in the culture's order. In a culture whose clock is written
 * with points, three numbers of two digits or fewer joined by points are its clock, not a date.
 */
function readDate(
  cursor: Cursor,
  shapes: Shapes,
): { date: ReadDate; yearFirst: boolean } | 'none' | 'invalid' {
  const start = cursor.at;
  const none = (): 'none' => {
    cursor.at = start;
    return 'none';
  };
  const parts: string[] = [];
  let points = true;
  for (let i = 0; i < 3; i++) {
    if (i > 0) {
      cursor.space();
      const separator = cursor.take(shapes.dateSeparators);
      if (separator === null) return none();
      points &&= separator === '.';
      cursor.space();
    }
    const part = cursor.digits();
    if (part === null) return none();
    parts.push(part);
  }
  if (shapes.pointIsClock && points && parts.every((part) => !looksLikeYear(part))) return none();
  // .NET reads a number of eight digits at most.
  if (parts.some((part) => part.length > 8)) return 'invalid';
  // The literal the culture's date ends with, which it writes after the day (ko-KR's `2024. 1. 15.`).
  if (shapes.dateEnd.length > 0) {
    const before = cursor.at;
    cursor.space();
    if (cursor.take([shapes.dateEnd]) === null) cursor.at = before;
  }
  const date = dateOf(parts, shapes.pattern);
  return date === null ? 'invalid' : { date, yearFirst: looksLikeYear(parts[0]) };
}

/**
 * Which number is the day, the month and the year, read off the culture's short date pattern, as
 * .NET reads them: three digits or more is a year wherever it is, and a year-first culture takes the
 * leading number when the last is not year-shaped; a year that leads is followed by the month, then
 * the day, in every culture .NET ships. Probed rather than reasoned: en-US reads `2026/7/17` in the ISO
 * order, and ja-JP reads `17/7/26` as 2017-07-26.
 */
function dateOf(parts: readonly string[], pattern: string): ReadDate | null {
  const lower = pattern.toLowerCase();
  const dayAt = lower.indexOf('d');
  const monthAt = lower.indexOf('m');
  const yearAt = lower.indexOf('y');
  const dayFirst = dayAt >= 0 && monthAt >= 0 && dayAt < monthAt;
  const yearFirst =
    yearAt >= 0 && (dayAt < 0 || yearAt < dayAt) && (monthAt < 0 || yearAt < monthAt);
  const [one, two, three] = parts;
  const yearLeads = looksLikeYear(one) || (yearFirst && !looksLikeYear(three));
  const date = yearLeads
    ? { year: yearOf(one), month: +two, day: +three }
    : { year: yearOf(three), month: dayFirst ? +two : +one, day: dayFirst ? +one : +two };
  return exists(date) ? date : null;
}

/** Three digits or more can only be a year: no month or day reaches 100. */
function looksLikeYear(part: string): boolean {
  return part.length >= 3;
}

/**
 * A year as .NET reads it: one or two digits are resolved against the calendar's `TwoDigitYearMax`,
 * 2049 for every culture the framework ships, so 00 to 49 land in this century and 50 to 99 in the
 * last; three digits or more are the year they write, so `1/2/003` is the year 3.
 */
function yearOf(part: string): number {
  const year = +part;
  return part.length <= 2 ? (year <= 49 ? 2000 + year : 1900 + year) : year;
}

/** Whether a date exists in the Gregorian calendar: the 31st of February is read and is none. */
function exists({ year, month, day }: ReadDate): boolean {
  if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1) return false;
  const leap = (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
  const length = month === 2 ? (leap ? 29 : 28) : [4, 6, 9, 11].includes(month) ? 30 : 31;
  return day <= length;
}

/** A designator here, and whether it says PM, or null. It ends where a word would: `AMx` is none. */
function readDesignator(cursor: Cursor, shapes: Shapes): boolean | null {
  for (const designator of shapes.designators) {
    const end = cursor.at + designator.text.length;
    if (
      cursor.text.slice(cursor.at, end).toLowerCase() === designator.text.toLowerCase() &&
      !isLetter(cursor.text[end])
    ) {
      cursor.at = end;
      return designator.pm;
    }
  }
  return null;
}

/** The parts of a clock and the designator beside it, before they are checked as a time of day. */
interface RawClock {
  parts: number[] & { fraction: number };
  pm: boolean | null;
}

/**
 * A clock, with the designator before or after it: null where there is neither, `invalid` where they
 * are no clock .NET reads. Whether it is a time of day is {@link clockOf}'s, once the text is read
 * through, since .NET names a word it does not know before it refuses a clock out of range.
 */
function readClockAndDesignator(cursor: Cursor, shapes: Shapes): RawClock | 'invalid' | null {
  const start = cursor.at;
  let pm = readDesignator(cursor, shapes);
  if (pm !== null) cursor.space();
  const parts = readClock(cursor, shapes);
  if (parts === null) {
    if (pm === null) return null;
    cursor.at = start;
    return 'invalid';
  }
  if (parts === 'invalid') return 'invalid';
  if (pm === null) {
    const before = cursor.at;
    cursor.space();
    pm = readDesignator(cursor, shapes);
    if (pm === null) cursor.at = before;
  }
  return { parts, pm };
}

/** The time of day a clock and its designator name, or why they name none. */
function clockOf({ parts, pm }: RawClock): ReadClock | Refusal {
  // The hours alone are a clock only with a designator.
  if (parts.length === 1 && pm === null) return FORMAT;
  let [hour] = parts;
  const [, minute = 0, second = 0] = parts;
  if (pm === false) {
    if (hour > 12) return FORMAT;
    if (hour === 12) hour = 0;
  } else if (pm === true && hour < 12) hour += 12;
  if (hour > 23 || minute > 59 || second > 59) return { refused: 'clock' };
  return { hour, minute, second, fraction: parts.fraction };
}

/**
 * The parts of a clock, one to three numbers of one or two digits, and a fraction after the seconds,
 * written with a point or a comma in every culture. Null where the text has no number here.
 */
function readClock(
  cursor: Cursor,
  shapes: Shapes,
): (number[] & { fraction: number }) | 'invalid' | null {
  const first = cursor.digits();
  if (first === null) return null;
  if (first.length > 2) return 'invalid';
  const parts = Object.assign([+first], { fraction: 0 });
  while (parts.length < 3) {
    const before = cursor.at;
    cursor.space();
    if (cursor.take(shapes.timeSeparators) === null) {
      cursor.at = before;
      break;
    }
    cursor.space();
    const part = cursor.digits();
    if (part === null || part.length > 2) return 'invalid';
    parts.push(+part);
  }
  if (parts.length === 3) {
    const mark = cursor.text[cursor.at];
    // A point or a comma may end the seconds with no digit after it, as .NET reads `09:30:15.`.
    if (mark === '.' || mark === ',') {
      cursor.at++;
      parts.fraction = fractionTicks(cursor.digits() ?? '');
    }
  }
  return parts;
}

/**
 * The ticks of a fraction of a second, as .NET's `ParseFraction` reads its digits: each one added at a
 * tenth of the place before, in doubles, and the sum rounded to the tick, half to even. Read as a
 * decimal instead, `.12345678` lost its eighth digit and `.00000005` its rounding.
 */
function fractionTicks(digits: string): number {
  let fraction = 0;
  let place = 0.1;
  for (const digit of digits) {
    fraction += +digit * place;
    place *= 0.1;
  }
  return round(fraction * 10_000_000);
}

/**
 * A zone: `Z` in either case, or a sign and an offset of hours, `-3`, `-03`, `-300`, `-0300`, `-03:00`
 * and `-3:5`. Null where there is none; a refusal where it is past fourteen hours, or its minutes are
 * not ones.
 */
function readZone(cursor: Cursor): 'utc' | bigint | Refusal | null {
  const mark = cursor.text[cursor.at];
  if ((mark === 'Z' || mark === 'z') && !isLetter(cursor.text[cursor.at + 1])) {
    cursor.at++;
    return 'utc';
  }
  if (mark !== '+' && mark !== '-') return null;
  const before = cursor.at;
  cursor.at++;
  const digits = cursor.digits();
  if (digits === null) {
    cursor.at = before;
    return null;
  }
  let hours: number;
  let minutes = 0;
  if (digits.length <= 2) {
    hours = +digits;
    if (cursor.text[cursor.at] === ':') {
      cursor.at++;
      const more = cursor.digits();
      if (more === null || more.length > 2) return { refused: 'format' };
      minutes = +more;
    }
  } else if (digits.length <= 4) {
    // The last two digits are the minutes: `+123` is an hour and 23 minutes.
    hours = +digits.slice(0, -2);
    minutes = +digits.slice(-2);
  } else return { refused: 'format' };
  if (minutes > 59) return { refused: 'format' };
  if (hours * 60 + minutes > 14 * 60) return { refused: 'zone' };
  const offset = BigInt(hours) * TICKS_PER_HOUR + BigInt(minutes) * TICKS_PER_MINUTE;
  return mark === '-' ? -offset : offset;
}
