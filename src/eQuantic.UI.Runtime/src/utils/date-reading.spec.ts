import { describe, it, expect } from 'vitest';
import { readMoment } from './date-reading';
import { INVARIANT_FORMAT, type DateTimeFormatData } from './culture';

/**
 * The reader's shapes, culture by culture. Each culture below is .NET 10's data for it, as the server
 * writes it, and each answer was measured with `DateTime.Parse(text, culture)` there; the conformance
 * suite (`DateTextInTheCultureConformanceTests`) runs both sides through the twins, and these pin
 * what the reader itself answers, where a failure names the shape rather than a page.
 */
function culture(data: Partial<DateTimeFormatData>): DateTimeFormatData {
  return { ...INVARIANT_FORMAT.dateTimeFormat, ...data };
}

const RLM = '\u200f';

const CULTURES = {
  invariant: INVARIANT_FORMAT.dateTimeFormat,
  'en-US': culture({ shortDatePattern: 'M/d/yyyy' }),
  'pt-BR': culture({ shortDatePattern: 'dd/MM/yyyy' }),
  'de-DE': culture({ shortDatePattern: 'dd.MM.yyyy', dateSeparator: '.' }),
  'sv-SE': culture({
    shortDatePattern: 'yyyy-MM-dd',
    dateSeparator: '-',
    amDesignator: 'fm',
    pmDesignator: 'em',
  }),
  ar: culture({
    shortDatePattern: `d${RLM}/M${RLM}/yyyy`,
    dateSeparator: `${RLM}/`,
    amDesignator: 'ص',
    pmDesignator: 'م',
  }),
  'fi-FI': culture({
    shortDatePattern: 'd.M.yyyy',
    dateSeparator: '.',
    timeSeparator: '.',
    amDesignator: 'ap.',
    pmDesignator: 'ip.',
  }),
  'id-ID': culture({ shortDatePattern: 'dd/MM/yyyy', timeSeparator: '.' }),
  'ja-JP': culture({ shortDatePattern: 'yyyy/MM/dd', amDesignator: '午前', pmDesignator: '午後' }),
  'ko-KR': culture({
    shortDatePattern: 'yyyy. M. d.',
    dateSeparator: '. ',
    amDesignator: '오전',
    pmDesignator: '오후',
  }),
} satisfies Record<string, DateTimeFormatData>;

type Culture = keyof typeof CULTURES;

/** What a text names, as `yyyy-MM-dd HH:mm:ss` and its zone, or the refusal. */
function read(text: string, name: Culture): string {
  const moment = readMoment(text, CULTURES[name]);
  if ('refused' in moment)
    return moment.refused === 'word' ? `word at ${moment.index}` : moment.refused;
  const pad = (value: number, width = 2) => String(value).padStart(width, '0');
  const date =
    moment.date && `${pad(moment.date.year, 4)}-${pad(moment.date.month)}-${pad(moment.date.day)}`;
  const clock =
    moment.clock &&
    `${pad(moment.clock.hour)}:${pad(moment.clock.minute)}:${pad(moment.clock.second)}` +
      (moment.clock.fraction ? `+${moment.clock.fraction}` : '');
  const zone =
    moment.zone === null ? null : moment.zone === 'utc' ? 'Z' : `${moment.zone / 600_000_000n}min`;
  return [date, clock, zone].filter((part) => part !== null).join(' ');
}

describe('readMoment: the order of a date is its culture’s', () => {
  it.each<[Culture, string, string]>([
    ['invariant', '01/02/2024', '2024-01-02'],
    ['en-US', '1/2/2024', '2024-01-02'],
    ['pt-BR', '01/02/2024', '2024-02-01'],
    ['de-DE', '01.02.2024', '2024-02-01'],
    ['sv-SE', '2024-02-01', '2024-02-01'],
    ['ja-JP', '2024/02/01', '2024-02-01'],
    // Three digits or more are a year wherever they stand; two pivot at 2049.
    ['pt-BR', '2024-02-01', '2024-02-01'],
    ['pt-BR', '01/02/49', '2049-02-01'],
    ['pt-BR', '01/02/50', '1950-02-01'],
  ])('%s reads %s as %s', (name, text, expected) => {
    expect(read(text, name)).toBe(expected);
  });

  it('refuses three numbers that make no date in the order, and no clock', () => {
    expect(read('13/01/2024', 'en-US')).toBe('format');
    expect(read('31/02/2024', 'pt-BR')).toBe('format');
  });
});

describe('readMoment: what a culture writes reads back', () => {
  it.each<[Culture, string, string]>([
    ['en-US', '1/15/2024 9:30:05 PM', '2024-01-15 21:30:05'],
    ['pt-BR', '15/01/2024 21:30:05', '2024-01-15 21:30:05'],
    ['de-DE', '15.01.2024 21:30', '2024-01-15 21:30:00'],
    ['sv-SE', '2024-01-15 21:30:05', '2024-01-15 21:30:05'],
    ['ar', `15${RLM}/1${RLM}/2024 9:30:05 م`, '2024-01-15 21:30:05'],
    ['fi-FI', '15.1.2024 21.30.05', '2024-01-15 21:30:05'],
    ['ja-JP', '2024/01/15 21:30:05', '2024-01-15 21:30:05'],
    ['ko-KR', '2024. 1. 15. 오후 9:30:05', '2024-01-15 21:30:05'],
  ])('%s reads %s', (name, text, expected) => {
    expect(read(text, name)).toBe(expected);
  });

  it('reads a clock whose culture writes a point between its parts, with no date', () => {
    expect(read('21.30.05', 'fi-FI')).toBe('21:30:05');
    expect(read('9.30 ip.', 'fi-FI')).toBe('21:30:00');
  });

  it('reads past the marks of direction ar writes after a number, and no other', () => {
    expect(read(`15${RLM}/1${RLM}/2024`, 'ar')).toBe('2024-01-15');
    expect(read(`${RLM}15/1/2024`, 'ar')).toBe('format');
    expect(read('15\u200e/1/2024', 'ar')).toBe('format');
  });
});

describe('readMoment: a clock', () => {
  it.each<[string, string]>([
    ['9:30', '09:30:00'],
    ['9 PM', '21:00:00'],
    ['pm 9:30', '21:30:00'],
    ['12:15 AM', '00:15:00'],
    ['13:00 PM', '13:00:00'],
    ['09:30:15.5', '09:30:15+5000000'],
    ['09:30:15,25', '09:30:15+2500000'],
    ['09:30:15.', '09:30:15'],
  ])('reads %s as %s', (text, expected) => {
    expect(read(text, 'invariant')).toBe(expected);
  });

  it('reads the culture’s designators beside AM and PM', () => {
    expect(read('9:30 em', 'sv-SE')).toBe('21:30:00');
    expect(read('9:30 PM', 'sv-SE')).toBe('21:30:00');
    expect(read('오전 12:30', 'ko-KR')).toBe('00:30:00');
  });

  it.each<[string, string]>([
    ['9', 'format'],
    ['13:00 AM', 'format'],
    ['9:30:15:20', 'format'],
    ['123:30', 'format'],
    ['24:00', 'clock'],
    ['23:60', 'clock'],
    ['PM', 'format'],
  ])('refuses %s (%s)', (text, refusal) => {
    expect(read(text, 'invariant')).toBe(refusal);
  });
});

describe('readMoment: a zone', () => {
  it.each<[string, string]>([
    ['2024-01-15T09:30:00Z', '2024-01-15 09:30:00 Z'],
    ['2024-01-15 09:30 -3', '2024-01-15 09:30:00 -180min'],
    ['2024-01-15 09:30 +0530', '2024-01-15 09:30:00 330min'],
    ['2024-01-15T09:30:00-03:00', '2024-01-15 09:30:00 -180min'],
    ['2024-01-15 +14:00', '2024-01-15 840min'],
    ['2024-01-15 09:30 +123', '2024-01-15 09:30:00 83min'],
  ])('reads %s', (text, expected) => {
    expect(read(text, 'invariant')).toBe(expected);
  });

  it.each<[string, string]>([
    ['2024-01-15 09:30 +14:01', 'zone'],
    ['2024-01-15 09:30 +03:60', 'format'],
    ['2024-01-15 09:30 +12345', 'format'],
  ])('refuses %s (%s)', (text, refusal) => {
    expect(read(text, 'invariant')).toBe(refusal);
  });
});

describe('readMoment: what is left over', () => {
  it('names a word where it starts, as .NET does', () => {
    expect(read('2024-01-15 junk', 'invariant')).toBe('word at 11');
  });

  it('names none after an ISO clock, where .NET gives its plain message', () => {
    expect(read('2024-01-15T09:30:00 junk', 'invariant')).toBe('format');
  });

  it('refuses a text with nothing to read', () => {
    expect(read('', 'invariant')).toBe('format');
    expect(read('   ', 'invariant')).toBe('format');
  });
});

describe('readMoment: a clock written with points', () => {
  it.each<[Culture, string, string]>([
    // Three short numbers joined by points are a clock where the culture writes its clock so.
    ['fi-FI', '10.11.12', '10:11:12'],
    ['fi-FI', '9.05.07', '09:05:07'],
    ['fi-FI', '10.11.12.5', '10:11:12+5000000'],
    ['id-ID', '10.11.12', '10:11:12'],
    ['de-DE', '10.11.12', '2012-11-10'],
    // A year among them makes them a date where the culture writes its date with points too.
    ['fi-FI', '10.11.2012', '2012-11-10'],
    ['fi-FI', '1.2.2024 10.11.12', '2024-02-01 10:11:12'],
    ['fi-FI', '10.11.12 1.2.2024', '2024-02-01 10:11:12'],
    ['id-ID', '10.11.2012', 'format'],
    ['id-ID', '10/11/2012', '2012-11-10'],
  ])('%s reads %s as %s', (name, text, expected) => {
    expect(read(text, name)).toBe(expected);
  });
});

describe('readMoment: the ISO form', () => {
  it.each<[string, string]>([
    ['2024-01-15T09:30:00.5', '2024-01-15 09:30:00+5000000'],
    ['2024-1-5T09:30', '2024-01-05 09:30:00'],
    ['2024/01/15T09:30', '2024-01-15 09:30:00'],
    ['2024-01-15 T 09:30', '2024-01-15 09:30:00'],
    ['2024-01-15t09:30z', '2024-01-15 09:30:00 Z'],
    ['2024-01-15T09:30:00+05:3', '2024-01-15 09:30:00 303min'],
    // Two digits a part, no designator, and a year first.
    ['2024-01-15T9:30', 'format'],
    ['2024-01-15T09:30:5', 'format'],
    ['2024-01-15T09:30:00 PM', 'format'],
    ['2024-01-15T09', 'format'],
    ['2024-01-15T09:30:00.', 'format'],
    ['24-01-15T09:30', 'format'],
  ])('reads %s as %s', (text, expected) => {
    expect(read(text, 'invariant')).toBe(expected);
  });
});

describe('readMoment: the order of the parts', () => {
  it.each<[string, string]>([
    ['21:30 2024/01/15', '2024-01-15 21:30:00'],
    ['9:30 PM 2024/01/15', '2024-01-15 21:30:00'],
    ['21:30 -03:00 2024/01/15', '2024-01-15 21:30:00 -180min'],
    ['2024/01/15 21:30 -03:00 PM', '2024-01-15 21:30:00 -180min'],
    ['2024/01/15 9:30 -03:00 PM', '2024-01-15 21:30:00 -180min'],
  ])('reads %s as %s', (text, expected) => {
    expect(read(text, 'invariant')).toBe(expected);
  });

  it('names a word before it refuses a clock out of range, as .NET’s lexer meets it first', () => {
    expect(read('2024/01/15 24:00 junk', 'invariant')).toBe('word at 17');
    expect(read('2024/01/15 13:30 AM junk', 'invariant')).toBe('word at 20');
    expect(read('2024/01/15 24:00', 'invariant')).toBe('clock');
  });

  it('reads a number of eight digits and refuses one of nine', () => {
    expect(read('1/1/00002024', 'invariant')).toBe('2024-01-01');
    expect(read('1/1/000002024', 'invariant')).toBe('format');
  });
});
