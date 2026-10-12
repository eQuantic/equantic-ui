/**
 * A time span read from text with no format, as .NET 10's `TimeSpanParse` reads one (#857). The twin
 * read one shape, `[-][d.]hh:mm:ss[.f]`, so `10` and `1:2` were refused where .NET reads ten days and
 * an hour and two minutes, and it checked no part against its bounds: `24:00:00` was a day where .NET
 * reads twenty-four, `00:60:00` an hour where .NET refuses it, and `99999999.00:00:00` a value .NET
 * has none for.
 *
 * .NET's reader splits the text, trimmed of white space, into numbers of ASCII digits and the runs
 * of anything else between them, and then reads them by how many numbers there are: one is days, two
 * are hours and minutes, three are hours, minutes and seconds, or days, hours and minutes, or hours,
 * minutes and a fraction; four add the seconds or the fraction; five are all of them. Which of those
 * the text is, the runs between the numbers decide, matched against two patterns, each positive and
 * then negative: the invariant one, `d.hh:mm:ss.fffffff`, and the culture's own, `d:hh:mm:ss`, then
 * its decimal separator and the fraction. So `1:02:03:04` is a day and two hours in every culture,
 * a fraction is written with a point in every culture and with the culture's separator in the
 * culture's pattern, and `24:00:00`, which no clock reads, is twenty-four days by the culture's.
 *
 * Every number is checked against its bound (days to 10,675,199, hours to 23, minutes and seconds to
 * 59, a fraction of more than seven digits rounded to seven) and the whole against a `TimeSpan`'s
 * range, and a text that names a span out of range is an overflow, not a format .NET does not read.
 * A fraction past seven digits whose leading zeros and digits come to fifteen or more is the one
 * case where .NET itself fails inside: its table of powers of ten is read past its end, and the
 * reader answers that as .NET does, with an `IndexOutOfRangeException`.
 */
import { isWhiteSpace } from './white-space';

/** Why a text names no span: no shape the reader knows (`format`), a part or the whole past its
 * bounds (`overflow`), or a fraction .NET's own reader fails on (`index`), each its own exception and
 * message in .NET. */
export type SpanRefusal = 'format' | 'overflow' | 'index';

/** What separates the parts of a pattern: before the days, between the days and the hours, the hours
 * and the minutes, the minutes and the seconds, the seconds and the fraction, and after it. */
interface Literals {
  start: string;
  dayHour: string;
  hourMinute: string;
  minuteSecond: string;
  secondFraction: string;
  end: string;
}

/** A number of the text: its value past its leading zeros, and how many zeros lead it. */
interface Num {
  num: number;
  zeroes: number;
}

const MAX_FRACTION_DIGITS = 7;
const MAX_DAYS = 10_675_199;
const MAX_HOURS = 23;
const MAX_MINUTES = 59;
const MAX_SECONDS = 59;
const MAX_FRACTION = 9_999_999;
/** The largest number a part may hold before the reader calls it an overflow: 0xFFFFFFF. */
const MAX_NUMBER = 0x0fff_ffff;
/** `long.MaxValue / TimeSpan.TicksPerMillisecond` and its negative twin, as C# divides them. */
const MAX_MILLISECONDS = 922_337_203_685_477n;
const MIN_MILLISECONDS = -922_337_203_685_477n;
const POWERS_OF_TEN = [1, 10, 100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000];

const ZERO: Num = { num: 0, zeroes: 0 };

/** The invariant pattern, `c`'s: `[-][d.]hh:mm:ss[.fffffff]`. */
function invariant(negative: boolean): Literals {
  return {
    start: negative ? '-' : '',
    dayHour: '.',
    hourMinute: ':',
    minuteSecond: ':',
    secondFraction: '.',
    end: '',
  };
}

/** The culture's pattern, `d':'h':'mm':'ss'` and its decimal separator, as .NET writes it for every
 * culture: the days end with a colon, and the fraction starts with the culture's separator. */
function localized(negative: boolean, decimalSeparator: string): Literals {
  return {
    start: negative ? '-' : '',
    dayHour: ':',
    hourMinute: ':',
    minuteSecond: ':',
    secondFraction: decimalSeparator,
    end: '',
  };
}

/** The text's numbers and the runs between them, as .NET's tokenizer reads them. */
interface Raw {
  numbers: Num[];
  literals: string[];
}

const MAX_TOKENS = 11;
const MAX_LITERALS = 6;
const MAX_NUMBERS = 5;

function isDigit(code: number): boolean {
  return code >= 0x30 && code <= 0x39;
}

function tokenize(text: string): Raw | SpanRefusal {
  const raw: Raw = { numbers: [], literals: [] };
  const addLiteral = (literal: string): boolean => {
    if (
      raw.literals.length >= MAX_LITERALS ||
      raw.literals.length + raw.numbers.length >= MAX_TOKENS
    )
      return false;
    raw.literals.push(literal);
    return true;
  };
  let lastWasNumber = false;
  let pos = 0;
  while (pos < text.length) {
    if (isDigit(text.charCodeAt(pos))) {
      let zeroes = 0;
      while (pos < text.length && text.charCodeAt(pos) === 0x30) {
        zeroes++;
        pos++;
      }
      let num = 0;
      while (pos < text.length && isDigit(text.charCodeAt(pos))) {
        num = num * 10 + (text.charCodeAt(pos) - 0x30);
        pos++;
        if (num > MAX_NUMBER) return 'overflow';
      }
      if (raw.literals.length + raw.numbers.length === 0 && !addLiteral('')) return 'format';
      if (
        raw.numbers.length >= MAX_NUMBERS ||
        raw.literals.length + raw.numbers.length >= MAX_TOKENS
      )
        return 'format';
      raw.numbers.push({ num, zeroes });
      lastWasNumber = true;
    } else {
      const start = pos;
      while (pos < text.length && !isDigit(text.charCodeAt(pos))) pos++;
      if (!addLiteral(text.slice(start, pos))) return 'format';
      lastWasNumber = false;
    }
  }
  if (lastWasNumber && !addLiteral('')) return 'format';
  return raw;
}

/** The fraction's number as ticks: padded to seven digits, or rounded to them away from zero. */
function fractionTicks(fraction: Num): number | SpanRefusal {
  const { num, zeroes } = fraction;
  if (num === 0) return 0;
  if (zeroes === 0 && num > MAX_FRACTION) return 'overflow';
  const total = String(num).length + zeroes;
  if (total === MAX_FRACTION_DIGITS) return num;
  if (total < MAX_FRACTION_DIGITS) return num * POWERS_OF_TEN[MAX_FRACTION_DIGITS - total];
  if (zeroes > MAX_FRACTION_DIGITS) return 0;
  const power = POWERS_OF_TEN[total - MAX_FRACTION_DIGITS];
  // .NET reads a power past its table's end here, and fails with the read.
  if (power === undefined) return 'index';
  return Math.floor((num + Math.floor(power / 2)) / power);
}

/** The ticks of a span's parts, or why they name none: a part past its bound, a fraction .NET cannot
 * round, or a whole past a `TimeSpan`'s range. */
function ticksOf(
  positive: boolean,
  days: Num,
  hours: Num,
  minutes: Num,
  seconds: Num,
  fraction: Num,
): bigint | SpanRefusal {
  if (
    days.num > MAX_DAYS ||
    hours.num > MAX_HOURS ||
    minutes.num > MAX_MINUTES ||
    seconds.num > MAX_SECONDS
  )
    return 'overflow';
  const ticks = fractionTicks(fraction);
  if (typeof ticks === 'string') return ticks;
  const milliseconds =
    (BigInt(days.num) * 86_400n +
      BigInt(hours.num) * 3_600n +
      BigInt(minutes.num) * 60n +
      BigInt(seconds.num)) *
    1_000n;
  if (milliseconds > MAX_MILLISECONDS || milliseconds < MIN_MILLISECONDS) return 'overflow';
  // A long's arithmetic, which wraps: the most negative span is read through it.
  const result = BigInt.asIntN(64, milliseconds * 10_000n + BigInt(ticks));
  if (positive && result < 0n) return 'overflow';
  return result;
}

/** The parts each shape takes from the text's numbers, in order: days, hours, minutes, seconds and
 * the fraction, a part the shape has none of being zero. */
type Shape = (numbers: Num[]) => [Num, Num, Num, Num, Num];

/** A shape: the runs it is written with, read from a pattern, and the parts it takes. */
interface Candidate {
  literals: (pattern: Literals) => string[];
  parts: Shape;
}

const D: Candidate = {
  literals: (p) => [p.start, p.end],
  parts: ([d]) => [d, ZERO, ZERO, ZERO, ZERO],
};
const HM: Candidate = {
  literals: (p) => [p.start, p.hourMinute, p.end],
  parts: ([h, m]) => [ZERO, h, m, ZERO, ZERO],
};
const HMS: Candidate = {
  literals: (p) => [p.start, p.hourMinute, p.minuteSecond, p.end],
  parts: ([h, m, s]) => [ZERO, h, m, s, ZERO],
};
const DHM: Candidate = {
  literals: (p) => [p.start, p.dayHour, p.hourMinute, p.end],
  parts: ([d, h, m]) => [d, h, m, ZERO, ZERO],
};
/** `h:m:.f`, a fraction with no seconds, which .NET reads for compatibility. */
const HM_F: Candidate = {
  literals: (p) => [p.start, p.hourMinute, p.minuteSecond + p.secondFraction, p.end],
  parts: ([h, m, f]) => [ZERO, h, m, ZERO, f],
};
const HMSF: Candidate = {
  literals: (p) => [p.start, p.hourMinute, p.minuteSecond, p.secondFraction, p.end],
  parts: ([h, m, s, f]) => [ZERO, h, m, s, f],
};
const DHMS: Candidate = {
  literals: (p) => [p.start, p.dayHour, p.hourMinute, p.minuteSecond, p.end],
  parts: ([d, h, m, s]) => [d, h, m, s, ZERO],
};
/** `d.h:m:.f`, the same compatibility with days before it. */
const DHM_F: Candidate = {
  literals: (p) => [p.start, p.dayHour, p.hourMinute, p.minuteSecond + p.secondFraction, p.end],
  parts: ([d, h, m, f]) => [d, h, m, ZERO, f],
};
const DHMSF: Candidate = {
  literals: (p) => [p.start, p.dayHour, p.hourMinute, p.minuteSecond, p.secondFraction, p.end],
  parts: ([d, h, m, s, f]) => [d, h, m, s, f],
};

/** The shapes a count of numbers may be, in the order .NET tries them for each pattern. */
const SHAPES: Record<number, Candidate[]> = {
  1: [D],
  2: [HM],
  3: [HMS, DHM, HM_F],
  4: [HMSF, DHMS, DHM_F],
  5: [DHMSF],
};

function sameRuns(literals: string[], expected: string[]): boolean {
  return (
    literals.length === expected.length && literals.every((literal, i) => literal === expected[i])
  );
}

/**
 * A text read as a span, in ticks, as `TimeSpan.Parse` reads it in a culture whose decimal separator
 * is the one given: the invariant pattern first, then the culture's, each positive and then negative.
 * A text two shapes could be is the first one whose numbers fit; one that only fits shapes whose
 * numbers are past their bounds is an overflow.
 */
export function readTimeSpan(text: string, decimalSeparator: string): bigint | SpanRefusal {
  let start = 0;
  let end = text.length;
  while (start < end && isWhiteSpace(text[start])) start++;
  while (end > start && isWhiteSpace(text[end - 1])) end--;
  if (start === end) return 'format';

  const raw = tokenize(text.slice(start, end));
  if (typeof raw === 'string') return raw;
  const shapes = SHAPES[raw.numbers.length];
  if (shapes === undefined) return 'format';

  const patterns: [Literals, boolean][] = [
    [invariant(false), true],
    [invariant(true), false],
    [localized(false, decimalSeparator), true],
    [localized(true, decimalSeparator), false],
  ];
  let overflow = false;
  for (const [pattern, positive] of patterns) {
    for (const shape of shapes) {
      if (!sameRuns(raw.literals, shape.literals(pattern))) continue;
      const ticks = ticksOf(positive, ...shape.parts(raw.numbers));
      if (ticks === 'index') return ticks;
      if (typeof ticks === 'string') {
        overflow = true;
        // A text of one or two numbers, or of five, has one shape: its numbers not fitting is the answer.
        if (shapes.length === 1) return 'overflow';
        continue;
      }
      if (positive) return ticks;
      const negated = BigInt.asIntN(64, -ticks);
      return negated > 0n ? 'overflow' : negated;
    }
  }
  return overflow ? 'overflow' : 'format';
}
