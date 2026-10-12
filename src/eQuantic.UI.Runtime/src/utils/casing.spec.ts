import { afterEach, describe, expect, it } from 'vitest';
import { toLower, toUpper } from './casing';
import { installCulture } from './culture';

/**
 * The casing twins' rules, one each. Every answer was measured with .NET 10's `ToUpper` and `ToLower`
 * in the culture named; the conformance suite (`CasingConformanceTests`) runs both sides over every
 * code unit of the BMP and the first supplementary plane, and these name the rule a failure breaks.
 */
function inCulture(name: string): void {
  installCulture(name, name, {});
}

afterEach(() => installCulture('', '', {}));

describe('casing: one code unit to one', () => {
  it('keeps a letter whose full upper case lengthens', () => {
    expect(toUpper('straße')).toBe('STRAßE');
    expect(toUpper('ﬁ')).toBe('ﬁ');
    expect(toUpper('ŉǰ')).toBe('ŉǰ');
  });

  it('upper-cases a letter with an iota subscript to its capital with a prosgegrammeni', () => {
    expect(toUpper('ᾳᾀῃῳ')).toBe('ᾼᾈῌῼ');
  });

  it('lower-cases a capital sigma to the medial one wherever it stands', () => {
    expect(toLower('ΟΔΟΣ ΟΔΟΣ')).toBe('οδοσ οδοσ');
  });

  it('cases a surrogate pair as its code point and leaves a lone surrogate', () => {
    expect(toUpper('𐐨x\ud801')).toBe('𐐀X\ud801');
    expect(toLower('\udc28𐐀')).toBe('\udc28𐐨');
  });

  it('leaves a letter .NET 10 does not case, whatever the engine’s Unicode says', () => {
    expect(toUpper('ƛɤ')).toBe('ƛɤ');
    expect(toLower('ꟜꟋ')).toBe('ꟜꟋ');
  });

  it('cases the rest as the engine does', () => {
    expect(toUpper('ação ſ ǅ')).toBe('AÇÃO S Ǆ');
    expect(toLower('AÇÃO Ǆ')).toBe('ação ǆ');
  });
});

describe('casing: the two i’s are the culture’s', () => {
  it.each<[string, string, string]>([
    ['', 'IıIİ', 'iıiİ'],
    ['en-US', 'IIIİ', 'iıii'],
    ['pt-BR', 'IIIİ', 'iıii'],
    ['tr-TR', 'İIIİ', 'iııi'],
    ['az-Latn-AZ', 'İIIİ', 'iııi'],
    ['tr-CY', 'İIIİ', 'iııi'],
  ])('%s upper-cases iıIİ to %s and lower-cases it to %s', (culture, upper, lower) => {
    inCulture(culture);
    expect(toUpper('iıIİ')).toBe(upper);
    expect(toLower('iıIİ')).toBe(lower);
  });

  it('reads the invariant culture where the call names it, in any page', () => {
    inCulture('tr-TR');
    expect(toUpper('iı', true)).toBe('Iı');
    expect(toLower('Iİ', true)).toBe('iİ');
  });
});

describe('casing: a char’s culture', () => {
  it('refuses a null one, as .NET refuses it', () => {
    expect(() => toUpper('a', null)).toThrow("Value cannot be null. (Parameter 'culture')");
    expect(() => toLower('a', null)).toThrow("Value cannot be null. (Parameter 'culture')");
  });
});
