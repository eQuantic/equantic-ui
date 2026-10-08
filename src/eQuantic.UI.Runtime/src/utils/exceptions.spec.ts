import { readdirSync, readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import {
  bases,
  construct,
  create,
  exception,
  Exception,
  filter,
  is,
  raise,
  typeInitialization,
  typesOf,
} from './exceptions';

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

  it("names a nested or a generic type by its own simple name, as .NET's Type.Name does", () => {
    // The names Type.ToString() writes, which the compiler hands.
    expect(create(['App.Outer+Inner', 'System.Exception']).name).toBe('Inner');
    expect(create(['App.Failed`1[App.Item]', 'System.Exception']).name).toBe('Failed`1');
    expect(create(['App.Outer`1+Inner[System.Int32]', 'System.Exception']).name).toBe('Inner');
    expect(create(['App.Pair`2+Gone[App.Box`1[System.Int32],System.String]', 'System.Exception']).name).toBe('Gone');
  });

  it('writes the default message of a nested or a generic type by the name .NET writes', () => {
    expect(create(['App.Outer+Inner', 'System.Exception']).message).toBe("Exception of type 'App.Outer+Inner' was thrown.");
  });

  it('writes a null message as Exception.Message does, a type\'s own text being the compiler\'s to hand', () => {
    expect(create(ARGUMENT_NULL, null).message).toBe("Exception of type 'System.ArgumentNullException' was thrown.");
    expect(create(['App.Oops', 'System.Exception']).message).toBe("Exception of type 'App.Oops' was thrown.");
    expect(create(ARGUMENT_NULL, 'Value cannot be null.', { paramName: 'x' }).message).toBe(
      "Value cannot be null. (Parameter 'x')",
    );
  });

  it("names a type initializer's type, a null one as '', and reads it as TypeName", () => {
    const initializer = ['System.TypeInitializationException', 'System.SystemException', 'System.Exception'];
    const failed = create(initializer, undefined, { typeName: 'App.T', innerException: null }) as Error & { typeName: string };
    expect(failed.message).toBe("The type initializer for 'App.T' threw an exception.");
    expect(failed.typeName).toBe('App.T');
    expect(create(initializer, undefined, { typeName: null }).message).toBe("The type initializer for '' threw an exception.");
  });

  it("gives an aggregate its first inner exception, the runtime's own included", () => {
    const first = new Error('f');
    const aggregate = exception('System.AggregateException', 'One or more errors occurred.', {
      innerExceptions: [first, new Error('g')],
    }) as Error & { innerException: unknown };
    expect(aggregate.message).toBe('One or more errors occurred. (f) (g)');
    expect(aggregate.innerException).toBe(first);
  });

  it('composes the parameter, the actual value and the object name as .NET does', () => {
    const range = ['System.ArgumentOutOfRangeException', 'System.ArgumentException', 'System.SystemException', 'System.Exception'];
    const error = create(range, 'm', { paramName: 'x', actualValue: 5 }) as Error & { paramName: string; actualValue: number };
    expect(error.message).toBe("m (Parameter 'x')\nActual value was 5.");
    expect(error.paramName).toBe('x');
    expect(error.actualValue).toBe(5);
    const disposed = ['System.ObjectDisposedException', 'System.InvalidOperationException', 'System.SystemException', 'System.Exception'];
    expect(create(disposed, 'Cannot access a disposed object.', { objectName: 'thing' }).message).toBe(
      "Cannot access a disposed object.\nObject name: 'thing'.",
    );
  });
});

describe("an exception class of the app's own is a class over the browser's Error (#611)", () => {
  // The shape of the twin eqc writes for `class Failure : Exception`: its chain in `static $types`.
  class Failure extends Exception {
    static $types = ['App.Failure', 'System.Exception'];
    code = 7;
  }
  class Retry extends Failure {
    static $types = ['App.Retry', 'App.Failure', 'System.Exception'];
  }
  class Custom extends Exception {
    static $types = ['App.Custom', 'System.Exception'];
    get message(): string {
      return 'custom:' + super.message;
    }
  }

  it('is an Error carrying the types its class says, and a derived class its own', () => {
    const retry = new Retry('r');
    expect(retry).toBeInstanceOf(Error);
    expect(retry.name).toBe('Retry');
    expect(typesOf(retry)).toEqual(['App.Retry', 'App.Failure', 'System.Exception']);
    expect(is(new Failure(), 'App.Retry')).toBe(false);
    expect(retry.code).toBe(7);
  });

  it('keeps the message and the inner exception it was handed, and null for none', () => {
    const inner = exception('System.InvalidOperationException', 'inner');
    const outer = new Failure('outer', { innerException: inner }) as Failure & { innerException: unknown };
    expect(outer.message).toBe('outer');
    const alone = new Failure('alone') as Failure & { innerException: unknown };
    expect(outer.innerException).toBe(inner);
    expect(alone.innerException).toBe(null);
  });

  it("composes the message from what its .NET base's constructor took, as a create does (#558)", () => {
    // The twin of `class Bad : ArgumentException { public Bad(string name) : base("bad", name) { } }`.
    class Bad extends Exception {
      static $types = ['App.Bad', 'System.ArgumentException', 'System.SystemException', 'System.Exception'];
    }
    const bad = new Bad('bad', { paramName: 'x' }) as Bad & { paramName: unknown };
    expect(bad.message).toBe("bad (Parameter 'x')");
    expect(bad.paramName).toBe('x');
    expect(JSON.stringify(bad)).toBe('{}');
  });

  it("reads a missing message as .NET's default for the type it is", () => {
    expect(new Failure().message).toBe("Exception of type 'App.Failure' was thrown.");
    expect(new Retry(null).message).toBe("Exception of type 'App.Retry' was thrown.");
  });

  it('lets a class override Message, which the Error constructor would have hidden', () => {
    expect(new Custom('text').message).toBe('custom:text');
  });

  it("lets a member the class declares answer under the name of one of the base's", () => {
    // The twins of `class Lookup : Exception { public string Name => key; }` and of
    // `class Renamed : ArgumentException { public override string ParamName => "q"; }`: an own
    // property of the instance, as `create` writes one, hid both.
    class Lookup extends Exception {
      static $types = ['App.Lookup', 'System.Exception'];
      get name(): string {
        return 'key';
      }
    }
    class Renamed extends Exception {
      static $types = ['App.Renamed', 'System.ArgumentException', 'System.SystemException', 'System.Exception'];
      get paramName(): string {
        return 'q';
      }
    }
    expect(new Lookup('m').name).toBe('key');
    const renamed = new Renamed('m', { paramName: 'p' });
    expect(renamed.paramName).toBe('q');
    expect(renamed.message).toBe("m (Parameter 'p')");
  });

  it("reads an aggregate's first inner exception as its InnerException, and none as null", () => {
    const first = exception('System.FormatException', 'f');
    const many = new Failure('m', { innerExceptions: [first, exception('System.FormatException', 'g')] }) as Failure & {
      innerException: unknown;
    };
    expect(many.innerException).toBe(first);
    expect((new Failure('m') as Failure & { innerException: unknown }).innerException).toBe(null);
  });

  it('keeps its members and its chain out of the way of JSON', () => {
    expect(JSON.stringify(new Failure('m'))).toBe('{"code":7}');
  });

  it("hands a construction of a generic class its construction's types before its constructor's body runs", () => {
    const INT = ['App.Failed`1[System.Int32]', 'System.Exception'];
    // The twin of `class Failed<T> : Exception`, whose constructor reads Message and throws itself.
    class Failed extends Exception {
      static $types = ['App.Failed`1[T]', 'System.Exception'];
      seen: string;
      constructor(message: string | null, rethrow: boolean) {
        super(message);
        this.seen = this.message;
        if (rethrow) throw this;
      }
    }
    const failed = construct(Failed, INT, 'x', false);
    expect(is(failed, 'App.Failed`1[System.Int32]')).toBe(true);
    expect(is(failed, 'App.Failed`1[System.String]')).toBe(false);
    expect(failed.name).toBe('Failed`1');
    expect(failed.message).toBe('x');
    expect(construct(Failed, INT, null, false).seen).toBe("Exception of type 'App.Failed`1[System.Int32]' was thrown.");
    let thrown: unknown;
    try {
      construct(Failed, INT, null, true);
    } catch (error) {
      thrown = error;
    }
    expect(is(thrown, 'App.Failed`1[System.Int32]')).toBe(true);
  });

  it('leaves the types a construction hands to the class it constructs', () => {
    class Inner extends Exception {
      static $types = ['App.Inner', 'System.Exception'];
    }
    // A field initializer runs before the base's constructor, and builds another exception there.
    class Outer extends Exception {
      static $types = ['App.Outer`1[T]', 'System.Exception'];
      constructor(readonly made = new Inner()) {
        super();
      }
    }
    const outer = construct(Outer, ['App.Outer`1[System.Int32]', 'System.Exception']);
    expect(typesOf(outer)).toEqual(['App.Outer`1[System.Int32]', 'System.Exception']);
    expect(typesOf(outer.made)).toEqual(['App.Inner', 'System.Exception']);
    expect(typesOf(new Outer())).toEqual(['App.Outer`1[T]', 'System.Exception']);
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

  it('wraps what a type initializer threw as .NET does, the original as its inner exception', () => {
    const original = exception('System.InvalidOperationException', 'x');
    const error = typeInitialization('App.Boom', original) as Error & { innerException?: unknown };
    expect(typesOf(error)).toEqual(['System.TypeInitializationException', 'System.SystemException', 'System.Exception']);
    expect(error.name).toBe('TypeInitializationException');
    expect(error.message).toBe("The type initializer for 'App.Boom' threw an exception.");
    expect(error.innerException).toBe(original);
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
    { file: 'exceptions.ts', words: 'new Error(composed(', why: 'where every .NET exception is made, its type with it' },
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
