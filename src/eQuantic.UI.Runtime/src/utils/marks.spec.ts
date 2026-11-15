import { describe, expect, it } from 'vitest';
import { equals } from './equals';
import { hash } from './hash';
import { sameKey } from './key-equality';
import { anonymousObject, asReference, isAnonymous, isReference } from './marks';

describe('a value held as plain JavaScript, marked as what it is in .NET (#742, #954)', () => {
  it('compares and hashes a list that went into object by reference, and a tuple by its elements', () => {
    const a = asReference([1]);
    const b = asReference([1]);
    expect(isReference(a)).toBe(true);
    expect(equals(a, b)).toBe(false);
    expect(equals(a, a)).toBe(true);
    expect(sameKey(a, b)).toBe(false);
    expect(equals([1, 2], [1, 2])).toBe(true);
    expect(sameKey([1, 2], [1, 2])).toBe(true);
    const before = hash(a);
    a.push(2);
    expect(hash(a)).toBe(before);
  });

  it('leaves the mark out of the value: JSON, its keys and a copy never see it', () => {
    const list = asReference([1, 2]);
    expect(JSON.stringify(list)).toBe('[1,2]');
    expect(isReference([...list])).toBe(false);
    const anon = anonymousObject({ r: 1 });
    expect(Object.keys(anon)).toEqual(['r']);
    expect(JSON.stringify(anon)).toBe('{"r":1}');
    expect(isAnonymous({ ...anon })).toBe(false);
  });

  it('never finds an anonymous object equal to a value held as data of the same members', () => {
    const color = { r: 255, g: 255, b: 255, a: 255 };
    const anon = anonymousObject({ r: 255, g: 255, b: 255, a: 255 });
    expect(equals(color, anon)).toBe(false);
    expect(equals(anon, color)).toBe(false);
    expect(sameKey(color, anon)).toBe(false);
    expect(equals(anon, anonymousObject({ r: 255, g: 255, b: 255, a: 255 }))).toBe(true);
    expect(equals(color, { ...color })).toBe(true);
  });

  it('leaves a primitive alone', () => {
    expect(asReference('text')).toBe('text');
    expect(isReference('text')).toBe(false);
  });
});
