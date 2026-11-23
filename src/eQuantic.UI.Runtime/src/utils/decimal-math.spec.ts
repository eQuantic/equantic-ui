import { describe, it, expect } from 'vitest';
import { Decimal } from './decimal';
import { clamp } from './decimal-math';
import { truncatedFromDecimal } from './number-convert';

const d = (text: string): Decimal => Decimal.from(text);

/**
 * A decimal through Math's members and into an integer, as .NET 10 answers (#1766, #1767): each value
 * below was measured there. The conformance suite runs both sides through the compiler
 * (`DecimalMathConformanceTests`); these pin the runtime's own answers.
 */
describe('Decimal: Math members', () => {
  it('takes its magnitude and its sign, keeping its scale', () => {
    expect(d('-12.50').abs().toString()).toBe('12.50');
    expect(d('12.50').abs().toString()).toBe('12.50');
    expect(d('-12.5').sign()).toBe(-1);
    expect(d('0.00').sign()).toBe(0);
    expect(d('3').sign()).toBe(1);
  });

  it('keeps the first of two equal values for Max and the second for Min, as .NET compares them', () => {
    expect(d('1.0').max(d('1.00')).toString()).toBe('1.0');
    expect(d('1.00').max(d('1.0')).toString()).toBe('1.00');
    expect(d('1.0').min(d('1.00')).toString()).toBe('1.00');
    expect(d('1.00').min(d('1.0')).toString()).toBe('1.0');
    expect(d('-12.50').max(d('0')).toString()).toBe('0');
  });

  it('clamps, keeping the value where it is inside, and refuses a minimum above the maximum', () => {
    expect(clamp(d('5'), d('1'), d('3')).toString()).toBe('3');
    expect(clamp(d('0.5'), d('1.0'), d('3')).toString()).toBe('1.0');
    expect(clamp(d('1.0'), d('1.00'), d('3')).toString()).toBe('1.0');
    expect(() => clamp(d('2'), d('3'), d('1'))).toThrow("'3' cannot be greater than 1.");
  });
});

describe('truncatedFromDecimal', () => {
  it('cuts toward zero from the digits, past what a double holds', () => {
    expect(truncatedFromDecimal(d('9007199254740993'), 'long')).toBe(9007199254740993n);
    expect(truncatedFromDecimal(d('0.99999999999999999'), 'int')).toBe(0);
    expect(truncatedFromDecimal(d('-2147483648.99999999'), 'int')).toBe(-2147483648);
    expect(truncatedFromDecimal(d('18446744073709551615.5'), 'ulong')).toBe(18446744073709551615n);
    expect(truncatedFromDecimal(d('65.9'), 'char')).toBe('A');
  });

  it('refuses what the type cannot hold, in .NET\'s words for it', () => {
    expect(() => truncatedFromDecimal(d('2147483648'), 'int')).toThrow('Value was either too large or too small for an Int32.');
    expect(() => truncatedFromDecimal(d('256'), 'byte')).toThrow('Value was either too large or too small for an unsigned byte.');
    expect(() => truncatedFromDecimal(d('-1'), 'char')).toThrow('Value was either too large or too small for a character.');
  });
});
