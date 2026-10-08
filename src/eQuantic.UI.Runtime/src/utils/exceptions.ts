/**
 * A .NET exception in the browser: a JavaScript `Error` that carries the .NET types it is, the most
 * derived first and `System.Exception` last, which is what a typed `catch`, a type pattern and an
 * `as` read ({@link is}). An `Error` alone says nothing of its type, so every catch took everything:
 * `catch (InvalidOperationException)` caught an `ArgumentException`, and two clauses could not be
 * told apart at all.
 *
 * Three doors make one, and each carries the whole chain:
 * - the translated code's `new T(message)` for a type of .NET's, where the compiler writes T and the
 *   types it derives from, read off T's symbol ({@link create});
 * - an exception class of the app's own, which is a class like any other: its twin extends
 *   {@link Exception}, and says its chain in `static $types` (#611);
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
  // A type initializer that threw: what every access to the type throws from then on (typeInitialization).
  'System.TypeInitializationException': 'System.SystemException',
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
 * What a framework exception's constructor was handed besides its message, by the member that reads
 * it (#558): `ArgumentException.ParamName` and `ArgumentOutOfRangeException.ActualValue`, which its
 * message ends with, an `InnerException`, and `ObjectDisposedException.ObjectName`.
 */
export interface ExceptionParts {
  readonly paramName?: string | null;
  readonly actualValue?: unknown;
  readonly innerException?: unknown;
  readonly objectName?: string | null;
  /** `TypeInitializationException.TypeName`, which its message names, a null one as ''. */
  readonly typeName?: string | null;
  /** `AggregateException`'s inner exceptions, whose messages its own ends with. */
  readonly innerExceptions?: Iterable<unknown> | null;
}

/** A value as `string.Format("{0}", value)` writes it in an exception's message. */
function valueText(value: unknown): string {
  if (typeof value === 'boolean') return value ? 'True' : 'False';
  return String(value);
}

/**
 * The message .NET composes: the one given, then the parameter's name, ` (Parameter 'x')`, the actual
 * value on a line of its own, and a disposed object's name. Each was dropped:
 * `new ArgumentNullException(nameof(x)).Message` was "x", the parameter's name taken for the message,
 * and an `InvalidOperationException()` had none at all (#558). Where no message is given, the text a
 * type's constructor writes then is the compiler's to hand, read from .NET itself, and what is left
 * is `Exception.Message`'s own, which names the type.
 */
function composed(types: readonly string[], message: string | null | undefined, parts: ExceptionParts | undefined): string {
  if (parts !== undefined && 'typeName' in parts) {
    return `The type initializer for '${parts.typeName ?? ''}' threw an exception.`;
  }
  let text = message ?? `Exception of type '${types[0]}' was thrown.`;
  const inners = parts?.innerExceptions == null ? [] : [...parts.innerExceptions];
  if (inners.length > 0) text += ' ' + inners.map((inner) => `(${messageOf(inner)})`).join(' ');
  if (parts?.objectName) text += `\nObject name: '${parts.objectName}'.`;
  if (parts?.paramName) text += ` (Parameter '${parts.paramName}')`;
  if (parts?.actualValue != null) text += `\nActual value was ${valueText(parts.actualValue)}.`;
  return text;
}

/**
 * `new T(message)` for an exception type the compiler resolved: `types` is T, the most derived, and
 * every type it derives from, `System.Exception` last. The error's `name` is T's simple name, so the
 * console and a contained component print `InvalidOperationException: …` as the C# side does, where
 * they printed `Error: …`. `parts` carries what a framework type's constructor took besides the
 * message, which the message and its members read.
 */
export function create(
  types: readonly string[],
  message?: string | null,
  parts?: ExceptionParts,
  ..._evaluated: unknown[]
): Error {
  const error = new Error(composed(types, message, parts));
  tag(error, types);
  if (parts !== undefined) hold(error, parts);
  return error;
}

/**
 * Gives an error its .NET types and the `name` the first one's simple name is. Defined rather than
 * assigned: an assignment makes `name` an own enumerable property, which an Error's own `name` (its
 * prototype's) is not, and JSON would start writing it.
 */
function tag(error: Error, types: readonly string[]): void {
  Object.defineProperty(error, 'name', {
    value: simpleName(types[0]),
    writable: true,
    configurable: true,
  });
  Object.defineProperty(error, TYPES, { value: types, configurable: true });
}

/** What a framework constructor took besides the message, each held as the member that reads it. */
function hold(error: Error, parts: ExceptionParts): void {
  for (const [member, value] of Object.entries(parts)) {
    const held = member === 'innerExceptions' && value != null ? [...(value as Iterable<unknown>)] : value;
    Object.defineProperty(error, member, { value: held, writable: true, configurable: true });
  }
  // An AggregateException's InnerException is its first inner one, as .NET's is.
  const first = (error as { innerExceptions?: unknown[] }).innerExceptions?.[0];
  if (parts.innerException === undefined && first !== undefined) {
    Object.defineProperty(error, 'innerException', { value: first, writable: true, configurable: true });
  }
}

/** Where an app exception keeps the message its constructor was handed: null for none. */
const MESSAGE = Symbol('eq.exception.message');

/** An app exception's twin, as the base reads it: the chain its class says. */
type Twin = { readonly $types?: readonly string[] };

/**
 * The base of the twin of an exception class of the app's own (#611): the browser's `Error`, carrying
 * the .NET types the class is, which its twin says in `static $types` (itself first, `System.Exception`
 * last), so a typed `catch` reads it as it reads one {@link create} built, and an exception of a
 * derived class carries the derived class's. The class's members are its twin's: it was an `Error`
 * built by its symbol, with no members at all, so its fields, its constructor's body and its methods
 * were gone.
 *
 * The constructor is `System.Exception`'s, a message and an inner exception, which `Message` and
 * `InnerException` read. A message that is null or missing is .NET's default, composed when it is read
 * from the type the exception is. `Message` is virtual in .NET, so it is an accessor on the prototype,
 * which the twin of a class that overrides it replaces: a message the `Error` constructor wrote would be
 * the instance's own, and would hide every override.
 */
export class Exception extends Error {
  constructor(message?: string | null, innerException?: unknown) {
    super();
    tag(this, (new.target as unknown as Twin).$types ?? EXCEPTION);
    Object.defineProperty(this, MESSAGE, { value: message ?? null, writable: true });
    Object.defineProperty(this, 'innerException', {
      value: innerException ?? null,
      writable: true,
      configurable: true,
    });
  }
}

Object.defineProperty(Exception.prototype, 'message', {
  get(this: Exception & { [MESSAGE]: string | null }): string {
    return composed((this as Tagged)[TYPES] ?? EXCEPTION, this[MESSAGE], undefined);
  },
  configurable: true,
});

/**
 * An exception of a generic class of the app's, built as one of its constructions (`new Failed<int>()`),
 * tagged with that construction's chain: its twin's `$types` can only say the class
 * (`Failed<T>`), and a typed `catch` tells `Failed<int>` from `Failed<string>`.
 */
export function typed<E extends Error>(error: E, types: readonly string[]): E {
  tag(error, types);
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

/** What a thrown value says: an error's message, and anything else's text. */
function messageOf(value: unknown): string {
  return isError(value) ? value.message : String(value);
}

/**
 * An exception the runtime throws on .NET's behalf, of the type .NET throws for the same operation,
 * with what .NET's constructor would have been handed besides the message.
 */
export function exception(type: RuntimeException, message: string, parts?: ExceptionParts): Error {
  const error = create(chainOf(type), message, parts);
  // An argument exception names its parameter in its message, which `ParamName` reads too, as .NET's
  // does: the runtime's own throws wrote the name into the text alone (#558).
  const parameter = /\(Parameter '([^']*)'\)/.exec(message);
  if (parameter !== null && chainOf(type).includes('System.ArgumentException')) {
    Object.defineProperty(error, 'paramName', { value: parameter[1], writable: true, configurable: true });
  }
  return error;
}

/**
 * The `TypeInitializationException` .NET throws when a type's static initializers or its static
 * constructor threw: on that first use of the type and on every one after it, the type never being
 * initialized again. `typeName` is the type's full name, as .NET's message writes it, and `inner` the
 * exception the initializer threw, which `InnerException` reads.
 */
export function typeInitialization(typeName: string, inner: unknown): Error {
  return exception('System.TypeInitializationException', `The type initializer for '${typeName}' threw an exception.`, {
    typeName,
    innerException: inner,
  });
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
