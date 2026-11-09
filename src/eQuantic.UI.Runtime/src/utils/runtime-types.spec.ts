import { describe, expect, it } from 'vitest';
import { closing } from './closures';
import { exception, is } from './exceptions';
import { named, of, same } from './runtime-types';

class Node {}
class A extends Node {}
class B extends Node {}
class Box<T> {
  constructor(readonly value?: T) {}
}

describe('a runtime type compares as .NET compares it', () => {
  it('is the class a value was built as, never its base', () => {
    expect(same(of(new A()), of(new A()))).toBe(true);
    expect(same(of(new A()), of(new B()))).toBe(false);
    expect(same(of(new A()), named(Node))).toBe(false);
    expect(same(named(A), of(new A()))).toBe(true);
  });

  it('tells two closed types of one generic class apart, and takes an unmarked one for either', () => {
    const ints = closing(new Box(1), 'int');
    const strings = closing(new Box('a'), 'string');
    expect(same(of(ints), of(strings))).toBe(false);
    expect(same(of(ints), of(closing(new Box(2), 'int')))).toBe(true);
    expect(same(of(ints), of(new Box(3)))).toBe(true);
    expect(same(of(ints), named(Box, 'string'))).toBe(false);
  });

  it('is an exception by the .NET type it carries', () => {
    const argument = exception('System.ArgumentException', 'bad');
    expect(same(of(argument), 'System.ArgumentException')).toBe(true);
    expect(same(of(argument), 'System.Exception')).toBe(false);
    expect(same(of(argument), of(new A()))).toBe(false);
  });

  it('throws the NullReferenceException for a null receiver', () => {
    let thrown: unknown;
    try {
      of(null);
    } catch (error) {
      thrown = error;
    }
    expect(is(thrown, 'System.NullReferenceException')).toBe(true);
  });
});
