/**
 * A `TimeSpan`'s text in a format, as .NET 10's `TimeSpanFormat` writes it (#837). The formatter
 * handed a TimeSpan to the twin's own `toString`, which takes no format, so every format wrote the
 * constant text: `ToString(@"hh\:mm")` of an hour and two minutes was `01:02:00`, and a format .NET
 * refuses wrote a value.
 *
 * A format of one character is a STANDARD one: `c`, `t` and `T` are the constant form in every
 * culture, `g` and `G` the general ones, with the culture's decimal separator, and any other letter
 * is .NET's FormatException. A longer one is CUSTOM, drawn token by token from the duration's parts
 * without its sign, as .NET draws it: a custom format writes no minus, so `\-` is how one is asked for.
 */
import { exception } from './exceptions';

const TICKS_PER_SECOND = 10_000_000n;
const TICKS_PER_MINUTE = 600_000_000n;
const TICKS_PER_HOUR = 36_000_000_000n;
const TICKS_PER_DAY = 864_000_000_000n;

/** .NET's FormatException for a format a TimeSpan does not take. */
const BAD_FORMAT = 'Input string was not in a correct format.';

/** The widest each repeated letter may be: past it, the format is refused. */
const WIDEST: Readonly<Record<string, number>> = { d: 8, h: 2, m: 2, s: 2, f: 7, F: 7 };

/** The duration's parts, its sign aside, as both kinds of format read them. */
interface Parts {
  readonly negative: boolean;
  readonly days: number;
  readonly hours: number;
  readonly minutes: number;
  readonly seconds: number;
  /** The fraction of a second, in ticks: seven digits of it. */
  readonly fraction: number;
}

function partsOf(ticks: bigint): Parts {
  const magnitude = ticks < 0n ? -ticks : ticks;
  const time = magnitude % TICKS_PER_DAY;
  return {
    negative: ticks < 0n,
    days: Number(magnitude / TICKS_PER_DAY),
    hours: Number(time / TICKS_PER_HOUR),
    minutes: Number((time / TICKS_PER_MINUTE) % 60n),
    seconds: Number((time / TICKS_PER_SECOND) % 60n),
    fraction: Number(time % TICKS_PER_SECOND),
  };
}

function digits(value: number, width: number): string {
  return String(value).padStart(width, '0');
}

/** The fraction's seven digits with the zeros that end them dropped, which may leave none. */
function trimmedFraction(fraction: number): string {
  return digits(fraction, 7).replace(/0+$/, '');
}

function formatError(message: string = BAD_FORMAT): Error {
  return exception('System.FormatException', message);
}

/**
 * The text of a duration of `ticks` in `format`, its general forms written with `decimal`, the
 * culture's decimal separator. A null or an empty format is the caller's to settle: it is the
 * constant form, which `c` writes.
 */
export function timeSpanText(ticks: bigint, format: string, decimal: string): string {
  const parts = partsOf(ticks);
  if (format.length === 1) return standard(parts, format, decimal);
  return custom(parts, format);
}

/** `c`, `t` and `T`: `[-][d.]hh:mm:ss[.fffffff]`. `g`: `[-][d:]h:mm:ss[.FFFFFFF]`. `G`:
 * `[-]d:hh:mm:ss.fffffff`. */
function standard(parts: Parts, letter: string, decimal: string): string {
  const sign = parts.negative ? '-' : '';
  const clock = `${digits(parts.minutes, 2)}:${digits(parts.seconds, 2)}`;
  switch (letter) {
    case 'c':
    case 't':
    case 'T': {
      const days = parts.days > 0 ? `${parts.days}.` : '';
      const fraction = parts.fraction > 0 ? `.${digits(parts.fraction, 7)}` : '';
      return `${sign}${days}${digits(parts.hours, 2)}:${clock}${fraction}`;
    }
    case 'g': {
      const days = parts.days > 0 ? `${parts.days}:` : '';
      const fraction = parts.fraction > 0 ? `${decimal}${trimmedFraction(parts.fraction)}` : '';
      return `${sign}${days}${parts.hours}:${clock}${fraction}`;
    }
    case 'G':
      return `${sign}${parts.days}:${digits(parts.hours, 2)}:${clock}${decimal}${digits(parts.fraction, 7)}`;
    default:
      throw formatError();
  }
}

/** How many times the character at `at` repeats from there. */
function run(format: string, at: number): number {
  let end = at + 1;
  while (end < format.length && format[end] === format[at]) end++;
  return end - at;
}

/** A custom format, token by token, as `TimeSpanFormat.FormatCustomized` draws it. */
function custom(parts: Parts, format: string): string {
  let text = '';
  let at = 0;
  while (at < format.length) {
    const ch = format[at];
    switch (ch) {
      case 'd':
      case 'h':
      case 'm':
      case 's':
      case 'f':
      case 'F': {
        const width = run(format, at);
        if (width > WIDEST[ch]) throw formatError();
        text += part(parts, ch, width);
        at += width;
        break;
      }
      case "'":
      case '"': {
        const [quoted, length] = quote(format, at);
        text += quoted;
        at += length;
        break;
      }
      case '%': {
        // One letter on its own, read as a format of that letter: `%h` is the hour unpadded. A
        // second `%`, or nothing, is refused.
        const next = format[at + 1];
        if (next === undefined || next === '%') throw formatError();
        text += custom(parts, next);
        at += 2;
        break;
      }
      case '\\': {
        const next = format[at + 1];
        if (next === undefined) throw formatError();
        text += next;
        at += 2;
        break;
      }
      default:
        throw formatError();
    }
  }
  return text;
}

/** One part, `width` letters wide. */
function part(parts: Parts, letter: string, width: number): string {
  switch (letter) {
    case 'd':
      return digits(parts.days, width);
    case 'h':
      return digits(parts.hours, width);
    case 'm':
      return digits(parts.minutes, width);
    case 's':
      return digits(parts.seconds, width);
    case 'f':
      return digits(Math.trunc(parts.fraction / 10 ** (7 - width)), width);
    default: {
      // `F`: the same digits, the zeros that end them dropped, and nothing where none is left.
      let value = Math.trunc(parts.fraction / 10 ** (7 - width));
      let kept = width;
      while (kept > 0 && value % 10 === 0) {
        value /= 10;
        kept--;
      }
      return kept > 0 ? digits(value, kept) : '';
    }
  }
}

/**
 * A quoted literal, as `DateTimeFormat.ParseQuoteString` reads one: up to the same quote again, a
 * backslash writing the character after it, and an open quote refused in .NET's words. Answers the
 * text and how many characters of the format it took.
 */
function quote(format: string, at: number): [string, number] {
  const mark = format[at];
  let text = '';
  let i = at + 1;
  while (i < format.length) {
    const ch = format[i++];
    if (ch === mark) return [text, i - at];
    if (ch === '\\') {
      if (i >= format.length) throw formatError();
      text += format[i++];
    } else {
      text += ch;
    }
  }
  throw formatError(`Cannot find a matching quote character for the character '${mark}'.`);
}
