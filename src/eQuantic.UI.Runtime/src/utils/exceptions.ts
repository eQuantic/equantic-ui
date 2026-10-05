/**
 * A .NET exception in the browser: a JavaScript `Error` that carries the .NET types it is, the most
 * derived first and `System.Exception` last, which is what a typed `catch`, a type pattern and an
 * `as` read ({@link is}). An `Error` alone says nothing of its type, so every catch took everything:
 * `catch (InvalidOperationException)` caught an `ArgumentException`, and two clauses could not be
 * told apart at all.
 *
 * Two doors make one, and both carry the whole chain:
 * - the translated code's `new T(message)`, where the compiler writes T and the types it derives from,
 *   read off T's symbol ({@link create}), an exception of the app's own included;
 * - the runtime's own throws on .NET's behalf, each of the type .NET throws for the same operation
 *   ({@link exception}), from the table below, which the conformance suite compares with .NET's own
 *   hierarchy.
 *
 * A value thrown without a chain is read as .NET would meet it. A `TypeError` is a
 * `NullReferenceException`: in a program C# has type-checked, the one type error left to the run is
 * a member read or a call through null. Anything else, a `RangeError` of the platform, an error from a
 * browser API or from code the compiler did not write, is an `Exception` and nothing more specific,
 * which only a `catch` with no type, or one of `Exception`, takes.
 */

/** Where an error keeps its chain: not enumerable, so neither `JSON.stringify` nor a spread sees it. */
const TYPES = Symbol.for('eq.exception.types');

type Tagged = Error & { [TYPES]?: readonly string[] };

/**
 * The .NET exceptions the runtime throws on .NET's behalf, each with the type it derives from: the
 * whole of what {@link exception} can throw. Read whole by the conformance suite, which fails where
 * an entry's base is not the one .NET's own type has.
 */
export const bases = {
  'System.Exception': null,
  'System.SystemException': 'System.Exception',
  'System.ArgumentException': 'System.SystemException',
  'System.ArgumentNullException': 'System.ArgumentException',
  'System.ArgumentOutOfRangeException': 'System.ArgumentException',
  'System.ArithmeticException': 'System.SystemException',
  'System.DivideByZeroException': 'System.ArithmeticException',
  'System.OverflowException': 'System.ArithmeticException',
  'System.FormatException': 'System.SystemException',
  'System.InvalidCastException': 'System.SystemException',
  'System.InvalidOperationException': 'System.SystemException',
  'System.NullReferenceException': 'System.SystemException',
  // A string's search by a culture comparison, which .NET makes with ICU's collation and the browser
  // cannot (#528): no .NET exception for the same call, so the one that says the platform lacks it.
  'System.NotSupportedException': 'System.SystemException',
  'System.Collections.Generic.KeyNotFoundException': 'System.SystemException',
  // The cancellation pair's own (utils/cancellation): a token that throws when cancelled, a source
  // used after it was disposed, and the callbacks of one cancellation that threw.
  'System.OperationCanceledException': 'System.SystemException',
  'System.ObjectDisposedException': 'System.InvalidOperationException',
  'System.AggregateException': 'System.Exception',
} as const satisfies Record<string, string | null>;

/** A .NET exception type the runtime throws itself. */
export type RuntimeException = keyof typeof bases;

const chains = new Map<string, readonly string[]>();

/** The type and every type it derives from, most derived first, built once per type. */
function chainOf(type: RuntimeException): readonly string[] {
  let chain = chains.get(type);
  if (chain === undefined) {
    const types: string[] = [];
    for (let at: RuntimeException | null = type; at !== null; at = bases[at]) types.push(at);
    chain = Object.freeze(types);
    chains.set(type, chain);
  }
  return chain;
}

const EXCEPTION = chainOf('System.Exception');
const NULL_REFERENCE = chainOf('System.NullReferenceException');

/**
 * `new T(message)` for an exception type the compiler resolved: `types` is T, the most derived, and
 * every type it derives from, `System.Exception` last. The error's `name` is T's simple name, so the
 * console and a contained component print `InvalidOperationException: …` as the C# side does, where
 * they printed `Error: …`.
 */
export function create(types: readonly string[], message?: string | null, ..._evaluated: unknown[]): Error {
  const error = new Error(message ?? undefined) as Tagged;
  // Defined rather than assigned: an assignment makes `name` an own enumerable property, which an
  // Error's own `name` (its prototype's) is not, and JSON would start writing it.
  Object.defineProperty(error, 'name', {
    value: simpleName(types[0]),
    writable: true,
    configurable: true,
  });
  Object.defineProperty(error, TYPES, { value: types });
  return error;
}

/**
 * A type's own name, its namespace, its containing types and every generic argument list left out:
 * `App.Outer<int>.Inner` is `Inner`, as .NET's `Type.Name` is, where cutting at the first `<` named it
 * `Outer`. The arguments of the constructor past the message (`..._evaluated`) are only evaluated,
 * where C# evaluates them, and carried nowhere.
 */
function simpleName(qualified: string): string {
  let plain = '';
  let depth = 0;
  for (const character of qualified) {
    if (character === '<') depth++;
    else if (character === '>') depth--;
    else if (depth === 0) plain += character;
  }
  return plain.slice(plain.lastIndexOf('.') + 1);
}

/** An exception the runtime throws on .NET's behalf, of the type .NET throws for the same operation. */
export function exception(type: RuntimeException, message: string): Error {
  return create(chainOf(type), message);
}

/**
 * The .NET types a thrown value is, the most derived first, or null for a value that is no exception
 * at all (only foreign code throws one: C# throws nothing but exceptions).
 */
export function typesOf(value: unknown): readonly string[] | null {
  if (!isError(value)) return null;
  const own = (value as Tagged)[TYPES];
  if (own !== undefined) return own;
  return value instanceof TypeError || (value as Error).name === 'TypeError' ? NULL_REFERENCE : EXCEPTION;
}

/**
 * An error of any realm: `instanceof Error` answers false for one thrown in another frame, and for a
 * `DOMException` (an aborted fetch) where the platform does not derive it from Error, so their tag is
 * read instead.
 */
function isError(value: unknown): value is Error {
  if (value instanceof Error) return true;
  const tag = Object.prototype.toString.call(value);
  return tag === '[object Error]' || tag === '[object DOMException]';
}

/**
 * Whether a value is of the .NET exception type named in full (`System.ArgumentException`): what a
 * typed `catch` tests, and a type pattern or an `as` over an exception type.
 */
export function is(value: unknown, type: string): boolean {
  return typesOf(value)?.includes(type) ?? false;
}

/**
 * A `throw` expression (`value ?? throw new X(…)`): the exception arrives as the argument, evaluated
 * where C# evaluates it, in the caller's own function, so an `await` in it is the caller's await.
 */
export function raise(error: unknown): never {
  throw thrown(error);
}

/**
 * What a `throw` throws: the exception, or the NullReferenceException the CLR throws in its place
 * when it is null (`Exception e = null; throw e;`), which a typed catch has to see as one.
 */
export function thrown(error: unknown): unknown {
  return error ?? exception('System.NullReferenceException', 'Object reference not set to an instance of an object.');
}

/**
 * An exception filter, `when (…)`, as .NET runs it: a filter that throws has answered false, and the
 * exception it was asked about goes on to the next clause, where the filter's own exception would
 * otherwise have replaced it. C# allows no `await` in a filter (CS7094), so the filter is a plain
 * function, run once where its clause is reached.
 */
export function filter(test: () => boolean): boolean {
  try {
    return test() === true;
  } catch {
    return false;
  }
}
