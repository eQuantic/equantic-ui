import { describe, it, expect } from 'vitest';
import { parseEnum, format, stringFormat } from './format';

describe('parseEnum', () => {
  // String enum (TypeScript style)
  const StringEnum = {
    Active: 'active',
    Pending: 'pending',
    Completed: 'completed',
  } as const;

  // Numeric enum (C# style - bidirectional)
  const NumericEnum = {
    None: 0,
    Low: 1,
    Medium: 2,
    High: 3,
    0: 'None',
    1: 'Low',
    2: 'Medium',
    3: 'High',
  } as const;

  describe('String Enums', () => {
    it('should parse exact match (case-sensitive)', () => {
      expect(parseEnum('Active', StringEnum)).toBe('active');
      expect(parseEnum('Pending', StringEnum)).toBe('pending');
    });

    it('should parse case-insensitive match', () => {
      expect(parseEnum('active', StringEnum)).toBe('active');
      expect(parseEnum('ACTIVE', StringEnum)).toBe('active');
      expect(parseEnum('AcTiVe', StringEnum)).toBe('active');
    });

    it('should parse by value (reverse lookup)', () => {
      expect(parseEnum('active', StringEnum)).toBe('active');
      expect(parseEnum('pending', StringEnum)).toBe('pending');
    });

    it('should return undefined for invalid value', () => {
      expect(parseEnum('Invalid', StringEnum)).toBeUndefined();
      expect(parseEnum('', StringEnum)).toBeUndefined();
    });
  });

  describe('Numeric Enums', () => {
    it('should parse from number', () => {
      expect(parseEnum(0, NumericEnum)).toBe('None');
      expect(parseEnum(1, NumericEnum)).toBe('Low');
      expect(parseEnum(2, NumericEnum)).toBe('Medium');
    });

    it('should parse from string name (case-sensitive)', () => {
      expect(parseEnum('Low', NumericEnum)).toBe(1);
      expect(parseEnum('Medium', NumericEnum)).toBe(2);
    });

    it('should parse from string name (case-insensitive)', () => {
      expect(parseEnum('low', NumericEnum)).toBe(1);
      expect(parseEnum('MEDIUM', NumericEnum)).toBe(2);
    });

    it('should return undefined for out of range number', () => {
      expect(parseEnum(99, NumericEnum)).toBeUndefined();
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty string', () => {
      expect(parseEnum('', StringEnum)).toBeUndefined();
    });

    it('should handle numeric string for numeric enum', () => {
      expect(parseEnum('1', NumericEnum)).toBe('Low');
      expect(parseEnum('2', NumericEnum)).toBe('Medium');
    });

    it('should handle whitespace (no trimming)', () => {
      expect(parseEnum(' Active ', StringEnum)).toBeUndefined();
    });
  });
});

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
