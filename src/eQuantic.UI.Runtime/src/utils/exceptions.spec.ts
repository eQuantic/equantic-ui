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
  const notDotNet: Record<string, string> = {
    'exceptions.ts': 'where every .NET exception is made, its type with it',
    'assert-never.ts': 'a case the code never reaches; reaching it is a defect of the runtime',
    'decimal.ts': 'a decimal literal the compiler wrote, which is valid by construction',
  };

  it('throws no untyped error outside the files that say why', () => {
    const untyped = readdirSync('src/utils')
      .filter((file) => file.endsWith('.ts') && !file.endsWith('.spec.ts'))
      .flatMap((file) =>
        readFileSync(`src/utils/${file}`, 'utf8')
          .split('\n')
          .map((line, index) => ({ file, line: index + 1, text: line }))
          .filter(({ text }) => /new (Range|Type)?Error\(/.test(text)),
      );
    const unexplained = untyped.filter(({ file }) => !(file in notDotNet));
    expect(unexplained).toEqual([]);
    // An explained file keeps exactly the throw it explains: one more would hide behind its reason.
    expect(untyped.map(({ file }) => file).sort()).toEqual(Object.keys(notDotNet).sort());
  });
});
