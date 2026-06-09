import { describe, it, expect } from 'vitest';
import { StringBuilder, stringBuilder } from './string-builder';

describe('StringBuilder — .NET-compat', () => {
  it('appends strings fluently', () => {
    expect(stringBuilder().append('Hello').append(' ').append('World').toString()).toBe('Hello World');
  });

  it('seeds from an initial string', () => {
    expect(stringBuilder('a').append('b').append('c').toString()).toBe('abc');
  });

  it('ignores a numeric capacity argument', () => {
    expect(stringBuilder(64).append('x').toString()).toBe('x');
  });

  it('appends numbers via their string form', () => {
    expect(stringBuilder().append(1).append(2).append(3).toString()).toBe('123');
  });

  it('appends booleans as .NET does (True/False)', () => {
    expect(stringBuilder().append(true).append(false).toString()).toBe('TrueFalse');
  });

  it('appendLine uses \\n', () => {
    expect(stringBuilder().appendLine('line1').append('line2').toString()).toBe('line1\nline2');
    expect(stringBuilder().appendLine().toString()).toBe('\n');
  });

  it('inserts at an index', () => {
    expect(stringBuilder('hello').insert(0, '>>').toString()).toBe('>>hello');
    expect(stringBuilder('hello').insert(2, 'XX').toString()).toBe('heXXllo');
  });

  it('removes a range', () => {
    expect(stringBuilder('hello').remove(0, 2).toString()).toBe('llo');
  });

  it('replaces every occurrence', () => {
    expect(stringBuilder('a-b-c').replace('-', '+').toString()).toBe('a+b+c');
  });

  it('clears', () => {
    expect(stringBuilder('hello').clear().append('x').toString()).toBe('x');
  });

  it('reports Length', () => {
    expect(stringBuilder('hello').length).toBe(5);
    expect(stringBuilder().append('ab').append('cd').length).toBe(4);
  });

  it('is an instance of StringBuilder', () => {
    expect(stringBuilder()).toBeInstanceOf(StringBuilder);
  });
});
