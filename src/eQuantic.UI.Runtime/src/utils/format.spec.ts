import { describe, it, expect, afterEach, vi } from 'vitest';
import { format, stringFormat, stringFormatInvariant, asNumber, recordText } from './format';
import { INVARIANT_FORMAT, installCulture, type CultureFormat } from './culture';

/** pt-BR as .NET 10 writes it on ICU, as far as these specs read it: its separators, digits and
 * patterns, its currency, its symbols and its short date and long time patterns. */
const PT_BR: CultureFormat = {
  numberFormat: {
    ...INVARIANT_FORMAT.numberFormat,
    numberDecimalSeparator: ',',
    numberGroupSeparator: '.',
    numberDecimalDigits: 3,
    currencySymbol: 'R$',
    currencyDecimalSeparator: ',',
    currencyGroupSeparator: '.',
    currencyPositivePattern: 2,
    currencyNegativePattern: 9,
    percentDecimalSeparator: ',',
    percentGroupSeparator: '.',
    percentDecimalDigits: 3,
    percentPositivePattern: 1,
    percentNegativePattern: 1,
    positiveInfinitySymbol: '∞',
    negativeInfinitySymbol: '-∞',
  },
  dateTimeFormat: {
    ...INVARIANT_FORMAT.dateTimeFormat,
    shortDatePattern: 'dd/MM/yyyy',
    longTimePattern: 'HH:mm:ss',
    eraName: 'd.C.',
  },
};
import { dateTime } from './datetime';

describe('format (existing tests)', () => {
  it('should format number with C2 (currency)', () => {
    const result = format(1234.56, 'C2');
    expect(result).toContain('1,234.56');
  });

  it('should format number with N0 (no decimals)', () => {
    const result = format(1234.56, 'N0');
    expect(result).toBe('1,235');
  });

  it('should format number with P (percentage)', () => {
    const result = format(0.1234, 'P2');
    expect(result).toContain('12.34');
  });

  it('should format number with D4 (decimal pad)', () => {
    const result = format(42, 'D4');
    expect(result).toBe('0042');
  });

  it('should format date with yyyy-MM-dd', () => {
    const date = new Date(2024, 0, 15); // Jan 15, 2024
    const result = format(date, 'yyyy-MM-dd');
    expect(result).toBe('2024-01-15');
  });

  it('should handle null/undefined', () => {
    expect(format(null, 'N2')).toBe('');
    expect(format(undefined, 'N2')).toBe('');
  });
});

describe('stringFormat (string.Format)', () => {
  it('substitutes positional placeholders', () => {
    expect(stringFormat('{0} + {1} = {2}', 1, 2, 3)).toBe('1 + 2 = 3');
  });

  it('reuses the same placeholder index', () => {
    expect(stringFormat('{0}-{0}', 'a')).toBe('a-a');
  });

  it('applies format specifiers via the value formatter', () => {
    expect(stringFormat('{0:F2}', 3.14159)).toBe('3.14');
    expect(stringFormat('{0:D3}', 7)).toBe('007');
  });

  it('unescapes doubled braces', () => {
    expect(stringFormat('{{literal}} {0}', 'x')).toBe('{literal} x');
  });

  it('renders null/undefined args as empty string', () => {
    expect(stringFormat('[{0}]', null)).toBe('[]');
    expect(stringFormat('[{0}]', undefined)).toBe('[]');
  });
});

describe('a number in .NET notation, a float with its own digits (#378)', () => {
  it('writes G and R as the shortest text that reads back, in .NET notation', () => {
    expect(format(1e21, 'G', undefined, true)).toBe('1E+21');
    expect(format(0.1 + 0.2, 'R', undefined, true)).toBe('0.30000000000000004');
  });

  it("writes a float's own digits when it says it is one", () => {
    const tenth = Math.fround(0.1);
    expect(format(tenth, 'G', undefined, true, 'single')).toBe('0.1');
    expect(format(tenth, null, 6, undefined, 'single')).toBe('   0.1');
    expect(format(Math.fround(1e9), null, undefined, undefined, 'single')).toBe('1E+09');
    // Without the kind the double underneath shows, which is what the kind exists to prevent.
    expect(format(tenth, 'G', undefined, true)).toBe('0.10000000149011612');
  });

  it('aligns a null as the empty text it writes', () => {
    expect(format(null, null, 4)).toBe('    ');
    expect(format(undefined, null, -3)).toBe('   ');
  });
});

describe('stringFormat, as .NET writes its placeholders', () => {
  it('writes a number with no specifier in .NET notation, and a bool as True/False', () => {
    expect(stringFormatInvariant('{0}|{1}|{2}', 1e21, true, false)).toBe('1E+21|True|False');
  });

  it('writes a float boxed for the call with its own digits, specifier or not', () => {
    expect(
      stringFormatInvariant(
        '{0}|{1:G}',
        asNumber(Math.fround(0.1), 'single'),
        asNumber(Math.fround(0.1), 'single'),
      ),
    ).toBe('0.1|0.1');
    expect(asNumber(null, 'single')).toBeNull();
  });

  it('aligns a placeholder by its width, right for a positive one and left for a negative one', () => {
    expect(stringFormatInvariant('[{0,5}][{0,-5}][{1,8:F2}]', 42, 3.14159)).toBe(
      '[   42][42   ][    3.14]',
    );
  });
});

describe('custom numeric formats (digit pictures)', () => {
  // `$"{x:0.0}"` is ordinary C#, and it used to fall through to value.toString(): a size printed
  // as "0.72265625 KB" in a panel meant to read like a terminal. eqc emitted the specifier
  // faithfully all along — it was dropped here.
  it('honours a fixed decimal picture', () => {
    expect(format(0.72265625, '0.0')).toBe('0.7');
    expect(format(0.72265625, '0.00')).toBe('0.72');
    expect(format(3, '0.0')).toBe('3.0');
  });

  it('treats # as an optional digit and 0 as a required one', () => {
    expect(format(1.5, '0.##')).toBe('1.5');
    expect(format(1.0, '0.##')).toBe('1');
    expect(format(1.0, '0.00')).toBe('1.00');
  });

  it('pads the integer side to the zeros asked for', () => {
    expect(format(7, '000')).toBe('007');
    expect(format(1234, '000')).toBe('1234');
  });

  it('groups only when the picture asks', () => {
    expect(format(1234567, '#,##0')).toBe('1,234,567');
    expect(format(1234567, '0')).toBe('1234567');
  });

  it('leaves the standard specifiers alone', () => {
    expect(format(3.14159, 'F2')).toBe('3.14');
    expect(format(7, 'D3')).toBe('007');
    expect(format(1234.5, 'N2')).toBe('1,234.50');
  });
});

describe('an invariant conversion ignores the culture reading it', () => {
  afterEach(() => installCulture('', '', {}));

  const reading = () => installCulture('pt-BR', 'pt-BR', {}, PT_BR);

  it('writes the invariant date patterns', () => {
    reading();
    const date = new Date(2026, 8, 24, 10, 30, 15);
    expect(stringFormat('{0:d}', date)).toBe('24/09/2026');
    expect(stringFormatInvariant('{0:G}', date)).toBe('09/24/2026 10:30:15');
    expect(format(date, 'd', undefined, true)).toBe('09/24/2026');
  });

  it('writes the generic currency sign', () => {
    reading();
    expect(stringFormatInvariant('{0:C}', 1.5)).toBe('¤1.50');
    expect(format(1.5, 'C', undefined, true)).toBe('¤1.50');
  });
});

// Past the 100 digits Intl writes after the point (#445), the culture's patterns still hold around
// every digit. The expected strings are what .NET 10 writes for the same value in pt-BR, byte for
// byte: the space in its currency pattern is a plain one (#634).
describe('a precision past 100 digits, in a culture', () => {
  afterEach(() => installCulture('', '', {}));

  it('keeps the culture’s currency and percent patterns around every digit', () => {
    installCulture('pt-BR', 'pt-BR', {}, PT_BR);
    const zeros = (count: number): string => '0'.repeat(count);
    expect(format(0.125, 'P101')).toBe(`12,5${zeros(100)}%`);
    expect(format(-1234.5, 'C101')).toBe(`-R$ 1.234,5${zeros(100)}`);
    expect(format(-1234.5, 'N101')).toBe(`-1.234,5${zeros(100)}`);
  });
});

// With no culture in force a date's patterns are the invariant culture's, and so are its names
// (#388): `Intl` was asked in the host's default locale, which wrote a Portuguese machine's day and
// month names into the invariant layout. The names are the invariant culture's own data now (#471),
// so the proof is that `Intl` is not asked at all.
describe('a date with no culture in force', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    installCulture('', '', {});
  });

  it('writes the invariant culture’s names, asking nothing of the host', () => {
    installCulture('', '', {});
    const asked: (string | string[] | undefined)[] = [];
    const DateTimeFormat = Intl.DateTimeFormat;
    // A function, not an arrow: the formatter calls it with `new`.
    vi.spyOn(Intl, 'DateTimeFormat').mockImplementation(function (
      locale?: string | string[],
      options?: Intl.DateTimeFormatOptions,
    ) {
      asked.push(locale);
      return new DateTimeFormat(locale, options);
    } as unknown as typeof Intl.DateTimeFormat);
    expect(format(new Date(2026, 8, 24, 22, 5), 'D')).toBe('Thursday, 24 September 2026');
    expect(format(new Date(2026, 8, 24, 22, 5), 'ddd, MMM d h:mm tt g')).toBe(
      'Thu, Sep 24 10:05 PM A.D.',
    );
    expect(asked).toEqual([]);
  });
});

// A DateTime keeps no time zone (found in review, #472): its parts print as they are, where a local
// Date built from them was normalised by the host's zone, and in New York's spring-forward gap 02:30
// became 03:30. Only U reads the value as local time, as .NET does. The expected strings are .NET 10's
// with TZ=America/New_York.
describe('a date in a time zone that skips or repeats an hour', () => {
  const zone = process.env.TZ;
  afterEach(() => {
    if (zone === undefined) delete process.env.TZ;
    else process.env.TZ = zone;
    installCulture('', '', {});
  });

  it('prints its own parts, and U moves it to UTC as .NET does', () => {
    process.env.TZ = 'America/New_York';
    installCulture('', '', {});
    const gap = dateTime(2026, 3, 8, 2, 30, 0);
    expect(format(gap, 'HH:mm')).toBe('02:30');
    expect(format(gap, 's')).toBe('2026-03-08T02:30:00');
    expect(format(gap, 'o')).toBe('2026-03-08T02:30:00.0000000');
    expect(format(gap, 'F')).toBe('Sunday, 08 March 2026 02:30:00');
    expect(format(gap, 'U')).toBe('Sunday, 08 March 2026 07:30:00');
    expect(format(dateTime(2026, 7, 1, 12, 0, 0), 'U')).toBe('Wednesday, 01 July 2026 16:00:00');
  });

  // A repeated hour is standard time to .NET's ToUniversalTime, where the Date constructor took the
  // daylight instant (found in Copilot's second round, #472). .NET 10's strings, TZ=America/New_York.
  it('reads the hour a fall-back repeats as standard time for U, as .NET does', () => {
    process.env.TZ = 'America/New_York';
    installCulture('', '', {});
    expect(format(dateTime(2026, 11, 1, 1, 30, 0), 'U')).toBe('Sunday, 01 November 2026 06:30:00');
    expect(format(dateTime(2026, 11, 1, 0, 59, 59), 'U')).toBe('Sunday, 01 November 2026 04:59:59');
    expect(format(dateTime(2026, 11, 1, 2, 0, 0), 'U')).toBe('Sunday, 01 November 2026 07:00:00');
    expect(format(dateTime(2026, 11, 1, 1, 30, 0), 'HH:mm')).toBe('01:30');
  });
});

describe('recordText', () => {
  it("writes a plain value as .NET writes the record, member by member in the order it is given", () => {
    expect(recordText({ r: 1, g: 2, b: 3, a: 4 }, 'Color', ['R', 'G', 'B', 'A'])).toBe(
      'Color { R = 1, G = 2, B = 3, A = 4 }',
    );
  });

  it('writes each member as an interpolation hole does', () => {
    expect(recordText({ on: true, ratio: 0.5, label: null }, 'Probe', ['On', 'Ratio', 'Label'])).toBe(
      'Probe { On = True, Ratio = 0.5, Label =  }',
    );
  });

  it('writes a member with what its type says: a number\u2019s kind, or a function of the value', () => {
    // A float in its own digits, an integer with no sign on the zero JavaScript made of `-1 / 2`, and
    // an enum by the function the compiler writes for its member names.
    expect(
      recordText({ ratio: Math.fround(0.1), half: -0, kind: 1 }, 'Reading', [
        ['Ratio', 'single'],
        ['Half', 'int32'],
        ['Kind', (value: number) => (value === 1 ? 'Large' : 'Small')],
      ]),
    ).toBe('Reading { Ratio = 0.1, Half = 0, Kind = Large }');
  });

  it('writes a record with no members, and nothing for a null value', () => {
    expect(recordText({}, 'Empty', [])).toBe('Empty { }');
    expect(recordText(null, 'Color', ['R'])).toBe('');
    expect(recordText(undefined, 'Color', ['R'])).toBe('');
  });
});

// A value's text with no format is its culture's, in the data the server wrote for the culture
// (#454, #471): the general digits with its separator and signs, a negative integer's minus sign.
describe('a value’s own text, in the culture in force', () => {
  afterEach(() => installCulture('', '', {}));

  const SV_SE: CultureFormat = {
    ...INVARIANT_FORMAT,
    numberFormat: {
      ...INVARIANT_FORMAT.numberFormat,
      numberDecimalSeparator: ',',
      numberGroupSeparator: ' ',
      negativeSign: '−',
    },
  };

  it('writes a number with no format in the culture’s symbols, whatever its type', () => {
    installCulture('sv-SE', 'sv-SE', {}, SV_SE);
    expect(format(-1.5, null)).toBe('−1,5');
    expect(format(-5, null, undefined, undefined, 'int32')).toBe('−5');
    expect(format(-5n, null)).toBe('−5');
    expect(format(1e-5, null, 8)).toBe('   1E−05');
    expect(stringFormat('{0}|{1}', -1.5, -2)).toBe('−1,5|−2');
  });

  it('writes the invariant text in an invariant conversion, whoever is reading', () => {
    installCulture('sv-SE', 'sv-SE', {}, SV_SE);
    expect(format(-1.5, null, undefined, true)).toBe('-1.5');
    expect(stringFormatInvariant('{0}', -1.5)).toBe('-1.5');
  });
});

// The formatter knows a number's type where the compiler passes it (#455).
describe('a number’s kind', () => {
  it('refuses D, X and B for a double whatever it holds, and takes them for a whole value of no kind', () => {
    expect(() => format(2, 'D', undefined, undefined, 'double')).toThrow('Format specifier was invalid.');
    expect(() => format(2, 'X', undefined, undefined, 'single')).toThrow('Format specifier was invalid.');
    expect(format(2, 'D3')).toBe('002');
    expect(() => format(2.5, 'D')).toThrow('Format specifier was invalid.');
  });

  it('writes a nint and a nuint at the platform’s width, rounding a half as an integer does', () => {
    expect(format(-1, 'X', undefined, undefined, 'nint')).toBe('FFFFFFFFFFFFFFFF');
    expect(format(255, 'X', undefined, undefined, 'nuint')).toBe('FF');
    expect(format(125, 'E1', undefined, undefined, 'nint')).toBe('1.3E+002');
    expect(format(125, 'E1', undefined, undefined, 'double')).toBe('1.2E+002');
  });
});

// A culture whose data did not travel — a switch with no server to ask — still writes `/`, `:` and
// `g` in its own symbols, as `Intl` writes them (#470).
describe('a date picture in a culture whose data did not travel', () => {
  afterEach(() => installCulture('', '', {}));

  it('takes the separators and the era from Intl', () => {
    const moment = dateTime(2026, 9, 24, 10, 30, 15);
    installCulture('de-DE', 'de-DE', {});
    expect(format(moment, 'dd/MM/yyyy HH:mm g')).toBe('24.09.2026 10:30 n. Chr.');
    installCulture('fi-FI', 'fi-FI', {});
    expect(format(moment, 'HH:mm')).toBe('10.30');
  });
});
