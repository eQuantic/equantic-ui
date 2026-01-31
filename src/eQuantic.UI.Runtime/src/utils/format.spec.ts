import { describe, it, expect } from 'vitest';
import { parseEnum, format } from './format';

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
