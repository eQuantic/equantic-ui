import { describe, expect, it } from 'vitest';
import { box, Boxed, isBoxOf, isDefined, name, SINGLE, unbox, unboxed, unboxEnum, values } from './box';
import { equals } from './equals';
import { format } from './format';
import { hash } from './hash';
import { sameKey } from './key-equality';

/** The shapes the compiler writes for `enum Tone { Warm, Cool }`, `enum Other { Small }` and a flags enum. */
const tone = { names: ['Warm', 'Cool'], keys: ['warm', 'cool'], values: [0, 1], flags: false, digits: 8 };
const other = { names: ['Small'], keys: ['small'], values: [0], flags: false, digits: 8 };
const wide = { names: ['Low'], keys: ['low'], values: [-5n], flags: false, digits: 16 }; // a 64-bit enum's values are BigInts (#1248)
const perm = { names: ['None', 'Read', 'Write'], values: [0, 1, 2], flags: true, digits: 8 };

describe('a value boxed into object (#552, #768, #954)', () => {
  it('boxes a member and a float with their type, and leaves null and a box as they are', () => {
    const cool = box('cool', 'Tone', tone);
    expect(cool).toBeInstanceOf(Boxed);
    expect(box(cool, 'Tone', tone)).toBe(cool);
    expect(box(null, 'Tone', tone)).toBeNull();
    expect(box(undefined, SINGLE)).toBeUndefined();
  });

  it('writes the value it holds to JSON, so the wire does not move', () => {
    expect(JSON.stringify([box('cool', 'Tone', tone), box(0.5, SINGLE), box(3, 'Perm', perm)])).toBe('["cool",0.5,3]');
  });

  it('equals a box of its type holding its value, a float NaN its NaN, and nothing else', () => {
    expect(equals(box('warm', 'Tone', tone), box('warm', 'Tone', tone))).toBe(true);
    expect(equals(box('warm', 'Tone', tone), 'warm')).toBe(false);
    expect(equals(box(0, 'Other', other), box(0, 'Tone', tone))).toBe(false);
    expect(equals(box(Number.NaN, SINGLE), box(Number.NaN, SINGLE))).toBe(true);
    expect(sameKey(box('warm', 'Tone', tone), box('warm', 'Tone', tone))).toBe(true);
    expect(sameKey(box('warm', 'Tone', tone), 'warm')).toBe(false);
  });

  it('hashes as its value unboxed, an enum by its underlying value', () => {
    expect(hash(box('cool', 'Tone', tone))).toBe(hash(1));
    expect(hash(box('low', 'Wide', wide))).toBe(hash(-5n));
    expect(hash(box(0.5, SINGLE))).toBe(hash(0.5));
  });

  it('writes a member by its name and a float by its own digits, a format included', () => {
    expect(format(box('cool', 'Tone', tone), null)).toBe('Cool');
    expect(format(box('cool', 'Tone', tone), 'D')).toBe('1');
    expect(format(box(3, 'Perm', perm), null)).toBe('Read, Write');
    expect(format(box(Math.fround(0.1), SINGLE), null)).toBe('0.1');
    expect(String(box(Math.fround(0.1), SINGLE))).toBe('0.1');
  });

  it('unboxes to its own type and an enum to its underlying type, and refuses any other', () => {
    expect(unbox(box(0.5, SINGLE), SINGLE)).toBe(0.5);
    expect(() => unbox(box(0.5, SINGLE), 'System.Double')).toThrow("Unable to cast object of type 'System.Single' to type 'System.Double'.");
    expect(unbox(box('cool', 'Tone', tone), 'System.Int32')).toBe(1);
    expect(unbox(box('low', 'Wide', wide), 'System.Int64')).toBe(-5n);
    expect(() => unbox(box('cool', 'Tone', tone), 'System.Int64')).toThrow("Unable to cast object of type 'Tone' to type 'System.Int64'.");
    expect(unbox(7, 'System.Int32')).toBe(7);
    expect(unbox(null, 'System.Int32', true)).toBeNull();
    expect(() => unbox(null, 'System.Int32')).toThrow('Object reference not set to an instance of an object.');
    expect(unboxed(box('cool', 'Tone', tone))).toBe(1);
    expect(unboxed(box(0.5, SINGLE))).toBe(0.5);
  });

  it('unboxes to an enum its own box, a box of another enum over the same type by number, and no string', () => {
    expect(unboxEnum(box('cool', 'Tone', tone), tone, 'Tone')).toBe('cool');
    expect(unboxEnum(box('small', 'Other', other), tone, 'Tone')).toBe('warm');
    expect(unboxEnum(1, tone, 'Tone')).toBe('cool');
    expect(() => unboxEnum('cool', tone, 'Tone')).toThrow("Unable to cast object of type 'System.String' to type 'Tone'.");
    expect(() => unboxEnum(box(0.5, SINGLE), tone, 'Tone')).toThrow("Unable to cast object of type 'System.Single' to type 'Tone'.");
  });

  it('answers the enum statics that take an object for its own box, and refuses another', () => {
    expect(isDefined(box('cool', 'Tone', tone), tone, 'object', 'Tone')).toBe(true);
    expect(isDefined('cool', tone, 'object', 'Tone')).toBe(false);
    expect(isDefined('Cool', tone, 'object', 'Tone')).toBe(true);
    expect(() => isDefined(box('small', 'Other', other), tone, 'object', 'Tone')).toThrow(
      "Object must be the same type as the enum. The type passed in was 'Other'; the enum type was 'Tone'.",
    );
    expect(name(box('cool', 'Tone', tone), tone, 'object', 'Tone')).toBe('Cool');
    expect(values(tone, 'Tone').every((value) => isBoxOf(value, 'Tone'))).toBe(true);
    expect(values(tone)).toEqual(['warm', 'cool']);
  });

  it('tests a value held as object for its type', () => {
    expect(isBoxOf(box('cool', 'Tone', tone), 'Tone')).toBe(true);
    expect(isBoxOf(box('small', 'Other', other), 'Tone')).toBe(false);
    expect(isBoxOf('cool', 'Tone')).toBe(false);
  });
});
