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
 * The forms: a date of two or three numbers, separated by a culture's separators or white space, a
 * date with a month's name, in the order the culture's month-day and year-month patterns say a month
 * and a lone number mean, a day's name before either, which must be the date's day, a clock of one to
 * three parts, a designator, a zone, `GMT` among them, and the ISO form, a date whose year comes first,
 * a `T` and a clock of two digits a part (#807). A name is read in the culture's language and in
 * English, in any case, as .NET reads it in every culture.
 *
 * The numbers of a date are counted in the culture's calendar (#1191): `15/1/2567` is the 15th of January
 * 2024 in th-TH, whose Thai Buddhist years are 543 ahead, `1402/10/25` the same day in fa-IR and
 * `4/7/1445` the next one in ar-SA, a date of two numbers is of this year as that calendar counts it, and
 * a year of two digits is read against that calendar's `TwoDigitYearMax`. The ISO form, a year-first date
 * before a `T`, is the Gregorian calendar's in every culture, as .NET's `ParseISO8601` reads it.
 */
import { calendarOf, civilFromDays, daysFromCivil, GREGORIAN, type Calendar } from './calendar';
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
 * time its calendar does not support), an offset past fourteen hours (`zone`), and a day's name that
 * is not the date's day (`weekday`).
 */
export type Refusal =
  | { refused: 'format' }
  | { refused: 'word'; index: number }
  | { refused: 'clock' }
  | { refused: 'zone' }
  | { refused: 'weekday' };

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
  /** Whether the short date pattern writes its year first. */
  yearFirst: boolean;
  /** The culture's own date separator. */
  dateSeparator: string;
  /** Whether the month-day pattern and the year-month pattern write the month first. */
  monthBeforeDay: boolean;
  monthBeforeYear: boolean;
  /** Each name and the month it names, the longest first, so `January` is tried before `Jan`. */
  months: { text: string; value: number }[];
  /** Each name and the day it names, as `System.DayOfWeek` counts, the longest first. */
  days: { text: string; value: number }[];
  /** The words the culture's date patterns write between a date's parts, the longest first. */
  words: string[];
  /** The calendar the culture counts a date's numbers in. */
  calendar: Calendar;
  /** The calendar's era, its name and its abbreviation, in lower case, the longest first. */
  eras: string[];
}

/** The English names, which .NET reads in every culture, beside the culture's own. */
const ENGLISH_MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December',
];
const ENGLISH_DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

/** Each name of the lists as it is spelled, a culture's point included (de-DE's `Sept.` is no `Sept`), in
 * lower case, the longest first. */
function spellings(lists: readonly (readonly string[])[], base: number): { text: string; value: number }[] {
  const found = new Map<string, number>();
  for (const list of lists) {
    list.forEach((name, index) => {
      const text = name.toLowerCase();
      if (text.length > 0 && !found.has(text)) found.set(text, index + base);
    });
  }
  return [...found]
    .map(([text, value]) => ({ text, value }))
    .sort((a, b) => b.text.length - a.text.length);
}

/**
 * The words a culture's patterns write between a moment's parts, .NET's date words: pt-BR's `de`, ru-RU's
 * `г.`, ja-JP's `年`, and bg-BG's `ч.` after a clock where Windows' data writes it. A quoted literal of any
 * pattern .NET scans is one (`DateTimeFormatInfoScanner.GetDateWordsOfDTFI`: the long and short dates, the
 * year and month, the month and day, the long and short times), and so is what is left of a DATE pattern with
 * its parts and its punctuation taken out. A point is no part of one: {@link Cursor.word} reads it after any.
 */
function dateWordsOf(culture: DateTimeFormatData): string[] {
  const words = new Set<string>();
  const add = (word: string): void => {
    const text = word.trim().replace(/\.$/, '').toLowerCase();
    if (text.length > 0) words.add(text);
  };
  const quotedOf = (pattern: string): string =>
    pattern.replace(/'([^']*)'/g, (_, quoted: string) => {
      add(quoted);
      return ' ';
    });
  const datePatterns = [culture.longDatePattern, culture.shortDatePattern, culture.monthDayPattern, culture.yearMonthPattern];
  // What is left unquoted is a word where it starts with a letter, as .NET's scanner takes one: ar-SA's
  // Arabic comma, `d MMM، yyyy`, is none, and .NET refuses a date written with it (#1191).
  for (const pattern of datePatterns)
    quotedOf(pattern)
      .split(/[dMyg\s,./-]+/)
      .filter((word) => isLetter(word[0]))
      .forEach(add);
  // A time pattern's own letters are its parts (`H:mm:ss`), never a word: only what it quotes is one.
  for (const pattern of [culture.longTimePattern, culture.shortTimePattern]) quotedOf(pattern);
  return [...words].sort((a, b) => b.length - a.length);
}

/** Whether a pattern writes `first` before `second`, as .NET's `GetMonthDayOrder` reads one. */
function comesFirst(pattern: string, first: string, second: string): boolean {
  const at = pattern.indexOf(first);
  const other = pattern.indexOf(second);
  return at >= 0 && (other < 0 || at < other);
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
    calendar: calendarOf(culture.calendar),
    // An era spelled as a designator is the designator, as .NET reads it: Linux's ar-SA counts in the
    // Gregorian calendar, whose era `م` is its PM too, and `15/1/2024 م` is noon.
    eras: [...new Set([culture.eraName, culture.abbreviatedEraName].map((era) => era.trim().toLowerCase()))]
      .filter((era) => era.length > 0 && !designators.some((designator) => designator.text.toLowerCase() === era))
      .sort((a, b) => b.length - a.length),
    dateSeparators: ['/', '-', ...point, dateSeparator].filter((s) => s.length > 0),
    timeSeparators: [':', own(culture.timeSeparator)].filter((s) => s.length > 0),
    pointIsClock,
    designators,
    dateEnd: own(pattern.slice(last + 1).replace(/'/g, '')),
    pattern,
    yearFirst: comesFirst(pattern.toLowerCase(), 'y', 'd') && comesFirst(pattern.toLowerCase(), 'y', 'm'),
    dateSeparator,
    monthBeforeDay: comesFirst(culture.monthDayPattern, 'M', 'd'),
    monthBeforeYear: comesFirst(culture.yearMonthPattern, 'M', 'y'),
    months: spellings(
      [
        culture.monthNames,
        culture.abbreviatedMonthNames,
        culture.monthGenitiveNames,
        culture.abbreviatedMonthGenitiveNames,
        ENGLISH_MONTHS,
        ENGLISH_MONTHS.map((month) => month.slice(0, 3)),
      ],
      1,
    ),
    days: spellings(
      [culture.dayNames, culture.abbreviatedDayNames, ENGLISH_DAYS, ENGLISH_DAYS.map((day) => day.slice(0, 3))],
      0,
    ),
    words: dateWordsOf(culture),
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

  /** One of `names` here, in any case and ending where a word ends, consumed: what it names, or null. */
  name(names: readonly { text: string; value: number }[]): number | null {
    for (const { text, value } of names) {
      const after = this.at + text.length;
      if (this.text.slice(this.at, after).toLowerCase() === text && !isLetter(this.text[after])) {
        this.at = after;
        return value;
      }
    }
    return null;
  }

  /** One of a culture's date `words` here, in any case and ending where a word ends, with the point it may end
   * with, consumed. A letter right after it is no boundary, in a script with no case too: .NET's lexer refuses
   * ja-JP's `2024年1月15日月曜日`, which Linux's and Windows' data write for its long date, and reads
   * `2024年1月15日 月曜日`. */
  word(words: readonly string[]): boolean {
    for (const word of words) {
      const after = this.at + word.length;
      if (this.text.slice(this.at, after).toLowerCase() === word && !isLetter(this.text[after])) {
        this.at = after;
        this.take(['.']);
        return true;
      }
    }
    return false;
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
 * is written first, a `T` and the ISO clock, wherever the date stands. A zone may lead the text, and a
 * day's name may stand once between any of the parts.
 */
export function readMoment(text: string, culture: DateTimeFormatData): Moment | Refusal {
  const shapes = shapesOf(culture);
  // The ISO form is read in the Gregorian calendar before the culture's is asked, as .NET reads it: a
  // year ar-SA's calendar does not count, 2024, is no refusal before its `T`.
  if (shapes.calendar !== GREGORIAN) {
    const iso = readIsoFirst(text, { ...shapes, calendar: GREGORIAN });
    if (iso !== null) return iso;
  }
  const cursor = new Cursor(text);
  cursor.space();

  // A day's name, ONCE, wherever .NET's lexer meets one: before the date or after it, before the clock
  // or after it, after the zone or the designator (#1842). A second is the plain refusal where it
  // stands; whether it is the date's day is asked last, once the date and the clock are known to exist.
  let weekday: number | null = null;
  const noteWeekday = (named: number | null): boolean => {
    if (named === null) return true;
    if (weekday !== null) return false;
    weekday = named;
    return true;
  };
  const takeWeekday = (): boolean => {
    const before = cursor.at;
    cursor.space();
    const named = cursor.name(shapes.days);
    if (named === null) {
      cursor.at = before;
      return true;
    }
    cursor.space();
    cursor.take([',']);
    return noteWeekday(named);
  };

  let zone: Moment['zone'] = null;
  // An offset past fourteen hours is refused last, after the rest of the text, the date, the clock and the
  // day's name, as .NET checks it, and not at all where an ISO form writes its own zone.
  let zoneRefusal: Refusal | null = null;
  // A zone, once: a second is the plain refusal, as .NET's lexer refuses `Z 2024-01-15 Z`.
  const readZoneHere = (): Refusal | null => {
    const before = cursor.at;
    cursor.space();
    const read = readZone(cursor);
    if (read === null) cursor.at = before;
    else if (zone !== null || zoneRefusal !== null) return FORMAT;
    else if (typeof read !== 'object') zone = read;
    else if (read.refused === 'zone') zoneRefusal = read;
    else return read;
    return null;
  };
  // The ISO form's moment with what came before it: its zone where it writes one, the one before it
  // otherwise, or the refusal of an offset before it past fourteen hours.
  const afterIso = (iso: Moment | Refusal): Moment | Refusal => {
    if (refused(iso) || iso.zone !== null) return iso;
    return zoneRefusal ?? { ...iso, zone };
  };
  // The ISO form where a date may start, read in the Gregorian calendar whatever the culture's: its clock
  // takes the place of any clock and designator before it, and its zone that of a zone before it, as
  // .NET's `ParseISO8601` reads it after whatever it met first (#1841). Null where none starts here.
  const readIsoHere = (): Moment | Refusal | null => {
    const before = cursor.at;
    cursor.space();
    const gregorian = shapes.calendar === GREGORIAN ? shapes : { ...shapes, calendar: GREGORIAN };
    const leading = readDate(cursor, gregorian);
    if (typeof leading === 'object' && leading.isoDate && noteWeekday(leading.weekday)) {
      cursor.space();
      if (isoMarker(text, cursor.at) === 'clock') {
        cursor.at++;
        return afterIso(readIso(cursor, leading.date));
      }
      if (leading.weekday !== null) weekday = null;
    }
    cursor.at = before;
    return null;
  };

  if (!takeWeekday()) return FORMAT;
  const leadingZone = readZoneHere();
  if (leadingZone) return leadingZone;
  if (!takeWeekday()) return FORMAT;
  // A second zone right after the first, which no date or clock stands between: `Z GMT 2024-01-15`.
  if (zone !== null || zoneRefusal !== null) {
    const second = readZoneHere();
    if (second) return second;
  }
  const isoFirst = readIsoHere();
  if (isoFirst !== null) return isoFirst;

  let date: ReadDate | null = null;
  cursor.space();
  const leading = readDate(cursor, shapes);
  if (leading === 'invalid') return FORMAT;
  if (leading !== 'none') {
    if (!noteWeekday(leading.weekday)) return FORMAT;
    const before = cursor.at;
    cursor.space();
    const marker = isoMarker(text, cursor.at);
    if (marker === 'clock') {
      // A `T` before a clock is the ISO form's only after its date, three numbers with the year first:
      // after any other date .NET refuses the text plainly (`2024-02T10:00`, `1/15/2024T10:00`). In another
      // calendar, a date the Gregorian one does not count is no ISO date either.
      if (!leading.isoDate || shapes.calendar !== GREGORIAN) return FORMAT;
      cursor.at++;
      return afterIso(readIso(cursor, leading.date));
    }
    // Numbers that name no date are one only before an ISO clock, which refuses them in other words; a
    // `T` that starts a word after them is the plain refusal too, where after a date it is the word's.
    if (leading.date === null) return FORMAT;
    date = leading.date;
    cursor.at = before;
  }

  cursor.space();
  const raw = readClockAndDesignator(cursor, shapes);
  if (raw === 'invalid') return FORMAT;
  if (!takeWeekday()) return FORMAT;

  if (date !== null || raw !== null) {
    const refusal = readZoneHere();
    if (refusal) return refusal;
    if (!takeWeekday()) return FORMAT;
  }

  // A date after the clock, and a zone after it where none came before.
  if (date === null && raw !== null) {
    const iso = readIsoHere();
    if (iso !== null) return iso;
    const before = cursor.at;
    cursor.space();
    const trailing = readDate(cursor, shapes);
    if (trailing === 'invalid') return FORMAT;
    if (trailing === 'none') cursor.at = before;
    else if (trailing.date === null) return FORMAT;
    else {
      if (!noteWeekday(trailing.weekday)) return FORMAT;
      // A `T` before a clock after a date that is not the ISO form's is the plain refusal, as it is
      // after a leading one (the ISO form itself was read just above).
      const after = cursor.at;
      cursor.space();
      if (isoMarker(text, cursor.at) === 'clock') return FORMAT;
      cursor.at = after;
      date = trailing.date;
      const refusal = readZoneHere();
      if (refusal) return refusal;
      if (!takeWeekday()) return FORMAT;
    }
  }

  // A designator after the zone, where none stood beside the clock.
  if (raw !== null && raw.pm === null) {
    const before = cursor.at;
    cursor.space();
    raw.pm = readDesignator(cursor, shapes);
    if (raw.pm === null) cursor.at = before;
    else if (!takeWeekday()) return FORMAT;
  }

  // A word a time pattern writes after the clock: hsb-DE's `hodź.`, and bg-BG's `ч.` in Windows' data.
  if (raw !== null) {
    const before = cursor.at;
    cursor.space();
    if (!cursor.word(shapes.words)) cursor.at = before;
  }

  cursor.space();
  if (!cursor.done || (date === null && raw === null)) {
    // A word is named where .NET's lexer meets it, before a clock out of range is.
    return isLetter(text[cursor.at]) ? { refused: 'word', index: cursor.at } : FORMAT;
  }
  // A designator is a clock only beside a date: `PM` alone names no moment.
  if (raw !== null && raw.parts.length === 0 && date === null) return FORMAT;
  const clock = raw === null ? null : clockOf(raw);
  if (clock !== null && refused(clock)) return clock;
  // The day's name is asked last, as .NET asks it once the day and the clock exist: of the date, or of
  // today where the text names none.
  if (weekday !== null && weekdayOf(date ?? localToday()) !== weekday) return { refused: 'weekday' };
  if (zoneRefusal !== null) return zoneRefusal;
  return { date, clock, zone };
}

/** Today's date where the browser is, which .NET takes for a text that names a clock and no date. */
function localToday(): ReadDate {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1, day: now.getDate() };
}

/** A text that starts with the ISO form, read in `shapes`, which count in the Gregorian calendar, or null
 * where it does not: a year-first date, then a `T`. */
function readIsoFirst(text: string, shapes: Shapes): Moment | Refusal | null {
  const cursor = new Cursor(text);
  cursor.space();
  const leading = readDate(cursor, shapes);
  if (typeof leading === 'string' || !leading.isoDate) return null;
  cursor.space();
  if (isoMarker(text, cursor.at) !== 'clock') return null;
  cursor.at++;
  return readIso(cursor, leading.date);
}

/**
 * What a `T` at `at` is, as .NET's lexer takes it: `clock` where it marks the ISO clock, which is wherever
 * no letter follows it (`2024-01-15T10:00`, and `2024-01-15T`, whose clock is then refused), `word` where a
 * letter does, which makes it the first letter of a word (`2024-01-15Tx` names an unknown word at the `T`),
 * and null where there is no `T`.
 */
function isoMarker(text: string, at: number): 'clock' | 'word' | null {
  if (text[at] !== 'T' && text[at] !== 't') return null;
  return isLetter(text[at + 1]) ? 'word' : 'clock';
}

/**
 * The rest of an ISO text after its `T`, as .NET's `ParseISO8601` reads it: hours and minutes of two
 * digits each and seconds of two, joined by colons with white space allowed beside them, a fraction
 * after the seconds, then a zone. No designator, and nothing after: what is left is the plain refusal.
 */
function readIso(cursor: Cursor, date: ReadDate | null): Moment | Refusal {
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
  // A zone that is no zone is the plain refusal where it stands; one past fourteen hours is .NET's last
  // check, after the rest of the text, the date and the clock (`2024-02-30T10:00+25:00` names the calendar).
  if (zone !== null && typeof zone === 'object' && zone.refused !== 'zone') return zone;
  cursor.space();
  if (!cursor.done) return FORMAT;
  // The date and the clock are checked together once the text is read, as .NET's `ParseISO8601` checks them:
  // a day that does not exist is refused as a clock that does not exist is, in the calendar's words (#1616).
  if (date === null || hour > 23 || minute > 59 || second > 59) return { refused: 'clock' };
  if (zone !== null && typeof zone === 'object') return zone;
  return { date, clock: { hour, minute, second, fraction }, zone };
}

/**
 * A date of three numbers: `none` where the text has none here, which leaves the cursor where it was,
 * `invalid` where its numbers make no date in the culture's order. In a culture whose clock is written
 * with points, three numbers of two digits or fewer joined by points are its clock, not a date.
 */
function readDate(cursor: Cursor, shapes: Shapes): ReadingOfADate {
  const start = cursor.at;
  let weekday = cursor.name(shapes.days);
  if (weekday !== null) {
    cursor.space();
    cursor.take([',']);
    cursor.space();
  }
  // The era before the date, as .NET reads it there, as often as it is written: `พ.ศ. 15/1/2567` (#1191).
  while (cursor.word(shapes.eras)) cursor.space();
  const named = readNamedDate(cursor, shapes);
  const read = named === 'none' ? readNumericDate(cursor, shapes) : named;
  if (read === 'none') {
    cursor.at = start;
    return 'none';
  }
  if (read === 'invalid') return read;
  // A day's name may follow the date too, as ko-KR's and ja-JP's long dates write it.
  if (weekday === null) {
    const after = cursor.at;
    cursor.take(['.']);
    cursor.space();
    cursor.take([',']);
    cursor.space();
    weekday = cursor.name(shapes.days);
    if (weekday === null) cursor.at = after;
  }
  return { ...read, weekday };
}

/** A date as a reader of one answers it: its date, or null for year-first numbers that name none, which only
 * the ISO form reads on (#1616), whether it is the ISO form's date, three numbers with the year first, and
 * the day's name written beside it, which the reader asks of the date last (#1842). */
type ReadingOfADate =
  | { date: ReadDate | null; isoDate: boolean; weekday: number | null }
  | 'none'
  | 'invalid';

/** The day of the week of a date, as `System.DayOfWeek` counts. */
function weekdayOf({ year, month, day }: ReadDate): number {
  const date = new Date(Date.UTC(2000, month - 1, day));
  date.setUTCFullYear(year);
  return date.getUTCDay();
}

/** Whether what follows a number makes it an hour rather than a date's: a separator only a clock has
 * (a point is a date's too, in fi-FI), or a designator, which a clock's hour alone may carry. */
function startsAClock(cursor: Cursor, shapes: Shapes): boolean {
  const before = cursor.at;
  const clock = cursor.take(shapes.timeSeparators.filter((s) => !shapes.dateSeparators.includes(s))) !== null;
  if (!clock) cursor.space();
  const designated = !clock && readDesignator(cursor, shapes) !== null;
  cursor.at = before;
  return clock || designated;
}

/**
 * A date with a month's name and up to two numbers, in any order, as .NET's `GetDayOfMN`, `GetDayOfNM`,
 * `GetDayOfMNN` and `GetDayOfYMN` read one: a number of three digits or more is the year, a month and a
 * year are the month's first day, a month and one short number are a day of this year unless the
 * culture's patterns make it a year, and two short numbers are told apart by the short date pattern.
 * A month alone is no date.
 */
function readNamedDate(cursor: Cursor, shapes: Shapes): ReadingOfADate {
  const start = cursor.at;
  let month: number | null = null;
  let numberFirst = false;
  const numbers: string[] = [];
  for (let i = 0; i < 3; i++) {
    const before = cursor.at;
    if (i > 0) {
      cursor.space();
      cursor.take([',', '-', '/', '.']);
      cursor.space();
      if (cursor.word(shapes.words)) cursor.space();
    }
    if (month === null) {
      month = cursor.name(shapes.months);
      if (month !== null) {
        cursor.take(['.']);
        continue;
      }
    }
    const number = cursor.digits();
    if (number === null || (number.length <= 2 && startsAClock(cursor, shapes))) {
      cursor.at = before;
      break;
    }
    if (month === null) numberFirst = true;
    numbers.push(number);
  }
  if (month === null) {
    cursor.at = start;
    return 'none';
  }
  endOfDate(cursor, shapes);
  if (numbers.some((number) => number.length > 8)) return 'invalid';
  const date = namedDateOf(month, numbers, numberFirst, shapes);
  return date === null ? 'invalid' : { date, isoDate: false, weekday: null };
}

/**
 * What a date may end with: a word, ru-RU's `г.` and ja-JP's `日`, beside its last part or after white space,
 * then the calendar's era, then the literal the culture's date ends with, which it writes after the day (ko-KR's `2024. 1. 15.`),
 * or the point any date may end with, but where a point is what separates the culture's date.
 */
function endOfDate(cursor: Cursor, shapes: Shapes): void {
  const before = cursor.at;
  cursor.space();
  if (!cursor.word(shapes.words)) cursor.at = before;
  // The era after the date, which a culture's short date writes where its calendar is not the Gregorian
  // one (ar-SA's on Windows, `3/7/1445 هـ`), and which .NET reads after any date, as often as it is
  // written (#1191).
  for (;;) {
    const beforeEra = cursor.at;
    cursor.space();
    if (!cursor.word(shapes.eras)) {
      cursor.at = beforeEra;
      break;
    }
  }
  const end = shapes.dateEnd.length > 0 ? shapes.dateEnd : shapes.dateSeparator === '.' ? null : '.';
  const last = cursor.at;
  cursor.space();
  if (end === null || cursor.take([end]) === null) cursor.at = last;
}

/** Which of a month's numbers is the day and which the year. */
function namedDateOf(month: number, numbers: readonly string[], numberFirst: boolean, shapes: Shapes): ReadDate | null {
  const valid = (date: ReadDate): ReadDate | null => gregorianOf(date, shapes);
  const yearOf = (part: string): number => yearIn(part, shapes);
  const [one, two] = numbers;
  if (one === undefined) return null;
  if (two === undefined) {
    if (looksLikeYear(one)) return valid({ year: yearOf(one), month, day: 1 });
    // Each order has a culture that reads its number as a year: `Jan 5` is 2005 in de-DE, `5 Jan` in the invariant one.
    const isYear = numberFirst ? shapes.monthBeforeDay && !shapes.monthBeforeYear : !shapes.monthBeforeDay && shapes.monthBeforeYear;
    return valid(isYear ? { year: yearOf(one), month, day: 1 } : { year: thisYear(shapes), month, day: +one });
  }
  if (looksLikeYear(one) || looksLikeYear(two)) {
    if (looksLikeYear(one) && looksLikeYear(two)) return null;
    return looksLikeYear(one) ? valid({ year: yearOf(one), month, day: +two }) : valid({ year: yearOf(two), month, day: +one });
  }
  const yearThenDay = valid({ year: yearOf(one), month, day: +two });
  const dayThenYear = valid({ year: yearOf(two), month, day: +one });
  return shapes.yearFirst ? (yearThenDay ?? dayThenYear) : (dayThenYear ?? yearThenDay);
}

/** A date of numbers: three, or two, which are a month and a day of this year in the order of the
 * culture's month-day pattern, or a month and a year. White space alone may separate them. */
function readNumericDate(cursor: Cursor, shapes: Shapes): ReadingOfADate {
  const start = cursor.at;
  const none = (): 'none' => {
    cursor.at = start;
    return 'none';
  };
  const parts: string[] = [];
  let points = true;
  for (let i = 0; i < 3; i++) {
    const before = cursor.at;
    let written = true;
    let joinedByAPoint = true;
    if (i > 0) {
      const spaced = cursor.space();
      const separator = cursor.take(shapes.dateSeparators);
      written = separator !== null || cursor.word(shapes.words);
      if (!written && !spaced) {
        if (i === 2) {
          cursor.at = before;
          break;
        }
        return none();
      }
      joinedByAPoint = separator === '.';
      cursor.space();
    }
    const part = cursor.digits();
    // A number white space alone sets apart may be a clock's hour; one a separator joins is the date's.
    if (part === null || (!written && part.length <= 2 && startsAClock(cursor, shapes))) {
      if (i === 2) {
        cursor.at = before;
        break;
      }
      return none();
    }
    points &&= joinedByAPoint;
    parts.push(part);
  }
  if (shapes.pointIsClock && points && parts.every((part) => !looksLikeYear(part))) return none();
  // .NET reads a number of eight digits at most.
  if (parts.some((part) => part.length > 8)) return 'invalid';
  // A number of three digits or more is a year to .NET's lexer, and a date has one year: `2024-01-001` and
  // `1/001/2024` name no date (#1843).
  if (parts.filter(looksLikeYear).length > 1) return 'invalid';
  endOfDate(cursor, shapes);
  const date = parts.length === 2 ? dateOfTwo(parts, shapes) : dateOf(parts, shapes);
  // The ISO form's date is three numbers, the year first. Ones that name no date (`2024-02-30`) are kept as
  // such: before a `T` they are the ISO form, which .NET reads through before it refuses the day (#1616).
  const iso = parts.length === 3 && looksLikeYear(parts[0]);
  if (date === null) return iso ? { date: null, isoDate: true, weekday: null } : 'invalid';
  return { date, isoDate: iso, weekday: null };
}

/** Two numbers, as .NET's `GetDayOfNN` and `GetDayOfYN` read them. */
function dateOfTwo([one, two]: readonly string[], shapes: Shapes): ReadDate | null {
  const year = thisYear(shapes);
  const date = looksLikeYear(one)
    ? { year: yearIn(one, shapes), month: +two, day: 1 }
    : looksLikeYear(two)
      ? { year: yearIn(two, shapes), month: +one, day: 1 }
      : shapes.monthBeforeDay
        ? { year, month: +one, day: +two }
        : { year, month: +two, day: +one };
  return gregorianOf(date, shapes);
}

/**
 * Which number is the day, the month and the year, read off the culture's short date pattern, as
 * .NET reads them: three digits or more is a year wherever it is, and a year-first culture takes the
 * leading number when the last is not year-shaped; a year that leads is followed by the month, then
 * the day, in every culture .NET ships. Probed rather than reasoned: en-US reads `2026/7/17` in the ISO
 * order, and ja-JP reads `17/7/26` as 2017-07-26.
 */
function dateOf(parts: readonly string[], shapes: Shapes): ReadDate | null {
  const lower = shapes.pattern.toLowerCase();
  const dayAt = lower.indexOf('d');
  const monthAt = lower.indexOf('m');
  const yearAt = lower.indexOf('y');
  const dayFirst = dayAt >= 0 && monthAt >= 0 && dayAt < monthAt;
  const yearFirst =
    yearAt >= 0 && (dayAt < 0 || yearAt < dayAt) && (monthAt < 0 || yearAt < monthAt);
  const [one, two, three] = parts;
  const yearLeads = looksLikeYear(one) || (yearFirst && !looksLikeYear(three));
  const date = yearLeads
    ? { year: yearIn(one, shapes), month: +two, day: +three }
    : { year: yearIn(three, shapes), month: dayFirst ? +two : +one, day: dayFirst ? +one : +two };
  return gregorianOf(date, shapes);
}

/** Three digits or more can only be a year: no month or day reaches 100. */
function looksLikeYear(part: string): boolean {
  return part.length >= 3;
}

/**
 * A year as .NET reads it: one or two digits are resolved against the calendar's `TwoDigitYearMax`,
 * 2049 in the Gregorian calendar, so 00 to 49 land in this century and 50 to 99 in the last, and 2572
 * in th-TH's, so 67 is 2567; three digits or more are the year they write, so `1/2/003` is the year 3.
 */
function yearIn(part: string, shapes: Shapes): number {
  return part.length <= 2 ? shapes.calendar.fourDigitYear(+part) : +part;
}

/** This year, as the culture's calendar counts it, where a date names no year of its own: .NET reads
 * the year of the local day. */
function thisYear(shapes: Shapes): number {
  const now = new Date();
  return shapes.calendar.dateOf(daysFromCivil(now.getFullYear(), now.getMonth() + 1, now.getDate()))?.year ?? 0;
}

/** The Gregorian date a date of the culture's calendar names, or null where it names none: the 31st of
 * February is read and is none, and so is the 30th of a Persian Esfand that is not a leap year's. */
function gregorianOf(date: ReadDate, shapes: Shapes): ReadDate | null {
  const day = shapes.calendar.dayOf(date);
  return day === null ? null : civilFromDays(day);
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
  if (pm !== null) {
    cursor.space();
    // A designator before a date is that date's noon or midnight, as .NET reads `PM 1/15/2024`: the
    // date's first number is no hour.
    const at = cursor.at;
    const date = readDate(cursor, shapes);
    cursor.at = at;
    if (typeof date === 'object') return { parts: Object.assign([], { fraction: 0 }), pm };
  }
  const parts = readClock(cursor, shapes);
  if (parts === null) {
    if (pm === null) return null;
    // A designator with no clock: noon or midnight of a date beside it, as .NET reads `1/15/2024 PM`,
    // and no moment alone, which the caller refuses where no date comes.
    cursor.at = start;
    readDesignator(cursor, shapes);
    return { parts: Object.assign([], { fraction: 0 }), pm };
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
  // A designator alone is the first hour of its half of the day.
  if (parts.length === 0) return { hour: pm ? 12 : 0, minute: 0, second: 0, fraction: 0 };
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
  // `GMT`, in any case, is the zero offset, and takes none after it.
  if (cursor.text.slice(cursor.at, cursor.at + 3).toUpperCase() === 'GMT' && !isLetter(cursor.text[cursor.at + 3])) {
    cursor.at += 3;
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
