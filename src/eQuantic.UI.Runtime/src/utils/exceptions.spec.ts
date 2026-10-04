import { readdirSync, readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { bases, create, exception, filter, is, raise, typesOf } from './exceptions';

const ARGUMENT_NULL = [
  'System.ArgumentNullException',
  'System.ArgumentException',
  'System.SystemException',
  'System.Exception',
];

describe('a .NET exception carries the types it is', () => {
  it('is an Error named by its simple name, with its message', () => {
    const error = create(ARGUMENT_NULL, 'p');
    expect(error).toBeInstanceOf(Error);
    expect(error.name).toBe('ArgumentNullException');
    expect(error.message).toBe('p');
    expect(typesOf(error)).toEqual(ARGUMENT_NULL);
  });

  it('is every type of its chain and no other', () => {
    const error = create(ARGUMENT_NULL, 'p');
    for (const type of ARGUMENT_NULL) expect(is(error, type)).toBe(true);
    expect(is(error, 'System.ArgumentOutOfRangeException')).toBe(false);
    expect(is(error, 'System.InvalidOperationException')).toBe(false);
  });

  it('keeps its chain out of JSON and out of a spread', () => {
    const error = create(ARGUMENT_NULL, 'p');
    expect(JSON.stringify(error)).toBe('{}');
    expect(Object.keys({ ...error })).toEqual([]);
  });

  it('names a nested or a generic type by its own simple name', () => {
    expect(create(['App.Outer.Inner', 'System.Exception']).name).toBe('Inner');
    expect(create(['App.Failed<App.Item>', 'System.Exception']).name).toBe('Failed');
    expect(create(['App.Outer<int>.Inner', 'System.Exception']).name).toBe('Inner');
    expect(create(['App.Pair<App.Box<int>, string>.Gone', 'System.Exception']).name).toBe('Gone');
  });

  it('takes a null message as no message', () => {
    expect(create(ARGUMENT_NULL, null).message).toBe('');
  });
});

describe('the runtime throws the type .NET throws', () => {
  it('builds each chain from the table, the most derived first', () => {
    expect(typesOf(exception('System.OverflowException', 'x'))).toEqual([
      'System.OverflowException',
      'System.ArithmeticException',
      'System.SystemException',
      'System.Exception',
    ]);
    expect(typesOf(exception('System.Collections.Generic.KeyNotFoundException', 'x'))).toEqual([
      'System.Collections.Generic.KeyNotFoundException',
      'System.SystemException',
      'System.Exception',
    ]);
  });

  it('roots every entry of the table at System.Exception', () => {
    for (const type of Object.keys(bases) as (keyof typeof bases)[]) {
      const chain = typesOf(exception(type, '')) ?? [];
      expect(chain[chain.length - 1]).toBe('System.Exception');
    }
  });
});

describe('a value thrown without a chain is read as .NET would meet it', () => {
  it('reads a TypeError as a NullReferenceException', () => {
    const lengthOf = (text: string | null): number => (text as string).length;
    let caught: unknown;
    try {
      lengthOf(null);
    } catch (error) {
      caught = error;
    }
    expect(is(caught, 'System.NullReferenceException')).toBe(true);
    expect(is(caught, 'System.SystemException')).toBe(true);
    expect(is(caught, 'System.Exception')).toBe(true);
    expect(is(caught, 'System.ArgumentException')).toBe(false);
  });

  it('reads any other error as an Exception and nothing more specific', () => {
    expect(typesOf(new RangeError('x'))).toEqual(['System.Exception']);
    expect(typesOf(new Error('x'))).toEqual(['System.Exception']);
    expect(is(new RangeError('x'), 'System.SystemException')).toBe(false);
  });

  it('reads an error of another realm, or a DOMException, as an Exception', () => {
    const foreign = { name: 'Error', message: 'x', [Symbol.toStringTag]: 'Error' };
    expect(typesOf(foreign)).toEqual(['System.Exception']);
    const crossTypeError = { name: 'TypeError', message: 'x', [Symbol.toStringTag]: 'Error' };
    expect(typesOf(crossTypeError)).toEqual(['System.NullReferenceException', 'System.SystemException', 'System.Exception']);
    expect(typesOf(new DOMException('aborted', 'AbortError'))).toEqual(['System.Exception']);
  });

  it('throws a null exception as the NullReferenceException the CLR throws', () => {
    expect(() => raise(null)).toThrow('Object reference not set to an instance of an object.');
    let caught: unknown;
    try {
      raise(undefined);
    } catch (error) {
      caught = error;
    }
    expect(is(caught, 'System.NullReferenceException')).toBe(true);
  });

  it('reads a value that is no error as no exception at all', () => {
    expect(typesOf('thrown text')).toBeNull();
    expect(is({ message: 'x' }, 'System.Exception')).toBe(false);
    expect(is(null, 'System.Exception')).toBe(false);
  });
});

describe('a throw expression and an exception filter', () => {
  it('raises the exception it is handed', () => {
    const error = create(ARGUMENT_NULL, 'p');
    expect(() => raise(error)).toThrow(error);
  });

  it('answers what the filter answers', () => {
    expect(filter(() => true)).toBe(true);
    expect(filter(() => false)).toBe(false);
  });

  it('answers false where the filter throws, as .NET does', () => {
    expect(
      filter(() => {
        throw new Error('inside the filter');
      }),
    ).toBe(false);
  });
});

/**
 * The instrument that keeps the table honest from the other side: a `throw new Error(…)` written in
 * the runtime's .NET twins carries no type, and once a `catch` tests its type, the clause it was
 * written for lets it through. Every throw there goes through `exception`, or is named here with the
 * reason it is no .NET exception.
 */
describe('every throw of the .NET twins carries its .NET type', () => {
  /** The untyped errors there are, each by its file and its words, and why it is no .NET exception. */
  const notDotNet: { file: string; words: string; why: string }[] = [
    { file: 'exceptions.ts', words: 'new Error(message', why: 'where every .NET exception is made, its type with it' },
    { file: 'assert-never.ts', words: 'Unhandled', why: 'a case the code never reaches; reaching it is a defect of the runtime' },
    { file: 'decimal.ts', words: 'Invalid decimal literal', why: 'a literal the compiler wrote, valid by construction' },
    { file: 'linq.ts', words: 'A sequence was expected', why: 'a value that crossed as a plain object where C# holds a sequence' },
  ];

  it('throws no untyped error but the ones that say why', () => {
    const untyped = readdirSync('src/utils')
      .filter((file) => file.endsWith('.ts') && !file.endsWith('.spec.ts'))
      .flatMap((file) =>
        readFileSync(`src/utils/${file}`, 'utf8')
          .split('\n')
          .map((line, index) => ({ file, line: index + 1, text: line }))
          .filter(({ text }) => /new (Range|Type)?Error\(/.test(text)),
      );
    const explained = (found: { file: string; text: string }) =>
      notDotNet.some(({ file, words }) => file === found.file && found.text.includes(words));
    expect(untyped.filter((found) => !explained(found))).toEqual([]);
    // Each reason still explains a throw that is there: one left standing after its throw went would
    // explain the next one written in its place.
    for (const reason of notDotNet) {
      expect(untyped.some(({ file, text }) => file === reason.file && text.includes(reason.words)), reason.file).toBe(true);
    }
  });
});
