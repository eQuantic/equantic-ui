import { describe, it, expect, afterEach } from 'vitest';
import { dateOnly } from './datetime';
import { installCulture } from './culture';

/**
 * What the DatePicker's typed row accepts, pinned against .NET rather than against an opinion.
 *
 * Every expectation in the table below was PROBED — `DateOnly.TryParse(text, new CultureInfo(c))`
 * on .NET 10 — and copied here verbatim, including the ones that look wrong at first read:
 * a month-first culture reads "2026/7/17" in ISO order, a year-first culture reads "17/7/26" as
 * 2017, and "1/2/003" is year THREE because three digits are literal where two pivot.
 */
const PATTERNS: Record<string, string> = {
  'en-US': 'M/d/yyyy',
  'pt-BR': 'dd/MM/yyyy',
  'ja-JP': 'yyyy/MM/dd',
  'sv-SE': 'yyyy-MM-dd',
  'de-DE': 'dd.MM.yyyy',
};

/** culture → input → what .NET answers, `null` where .NET refuses. */
const PROBED: Record<string, Record<string, string | null>> = {
  'en-US': {
    '7/17/26': '2026-07-17',
    '17/7/26': null,
    '2026/7/17': '2026-07-17',
    '7/17/2026': '2026-07-17',
    '17/7/2026': null,
    '26/7/17': null,
    '7-17-2026': '2026-07-17',
    '1/2/03': '2003-01-02',
    '1/2/3': '2003-01-02',
    '1/2/00': '2000-01-02',
    '1/2/49': '2049-01-02',
    '1/2/50': '1950-01-02',
    '1/2/003': '0003-01-02',
    '0026/7/17': '0026-07-17',
  },
  'pt-BR': {
    '7/17/26': null,
    '17/7/26': '2026-07-17',
    '2026/7/17': '2026-07-17',
    '7/17/2026': null,
    '17/7/2026': '2026-07-17',
    '26/7/17': '2017-07-26',
    '17.7.2026': '2026-07-17',
    '1/2/03': '2003-02-01',
  },
  'ja-JP': {
    '17/7/26': '2017-07-26',
    '26/7/17': '2026-07-17',
    '99/1/2': '1999-01-02',
    '7/17/2026': '2026-07-17',
    '2026/7/17': '2026-07-17',
    '1/2/03': '2001-02-03',
    '1/2/3': '2001-02-03',
    '1/2/00': null,
    '1/2/003': '0003-01-02',
    '7/17/26': null,
  },
  'sv-SE': {
    '17/7/26': '2017-07-26',
    '26/7/17': '2026-07-17',
    '99/1/2': '1999-01-02',
    '1/2/03': '2001-02-03',
  },
  'de-DE': {
    '17/7/26': '2026-07-17',
    '7/17/26': null,
    '17.7.2026': '2026-07-17',
    '1/2/03': '2003-02-01',
  },
};

afterEach(() => installCulture('', '', {}));

describe('DateOnly.TryParse — the slot a number lands in is the culture pattern says', () => {
  for (const [culture, cases] of Object.entries(PROBED)) {
    for (const [text, expected] of Object.entries(cases)) {
      it(`${culture}: "${text}" → ${expected ?? 'refused'}`, () => {
        installCulture(culture, culture, { $dateShort: PATTERNS[culture] });
        const parsed = dateOnly.tryParse(text);
        expect(parsed === null ? null : parsed.toString('yyyy-MM-dd')).toBe(expected);
      });
    }
  }

  it('ISO is culture-free — a day-first culture still reads yyyy-MM-dd in order', () => {
    installCulture('pt-BR', 'pt-BR', { $dateShort: 'dd/MM/yyyy' });
    expect(dateOnly.tryParse('2026-07-17')?.toString('yyyy-MM-dd')).toBe('2026-07-17');
  });

  it('with no catalog behind the page the invariant order applies', () => {
    expect(dateOnly.tryParse('7/17/26')?.toString('yyyy-MM-dd')).toBe('2026-07-17');
    expect(dateOnly.tryParse('17/7/26')).toBeNull();
  });
});
