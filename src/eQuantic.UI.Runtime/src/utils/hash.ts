/**
 * `GetHashCode` as .NET's contract has it: values `Equals` finds equal hash equal. .NET's own numbers
 * are not stable across processes (a string's hash is randomized in every one), so the browser cannot
 * agree with the server's number, and nothing may depend on the number itself. Each call threw
 * instead, `getHashCode is not a function`, for a string, a number and a record alike (#519).
 *
 * The hash follows `$eq.equals` (utils/equals.ts) case by case, so `equals(a, b)` implies
 * `hash(a) === hash(b)`:
 *  - a value with its own `getHashCode` answers it: a class or a record that overrides GetHashCode, a
 *    record's or a struct's twin, and the runtime's own types (a decimal, the dates, a dictionary);
 *  - a number as a double hashes, NaN with NaN and -0 with 0, as Equals holds them, and an int is its
 *    own hash, as in .NET; a long by its two halves; a string and a char by their UTF-16 code units;
 *    a bool as 1 or 0; null as 0;
 *  - a value tuple, an array here, element by element, in order, and an array that holds itself, at
 *    any depth, by its identity where it recurs, so the walk ends. A C# array the compiler can see is
 *    one is hashed by its identity before it gets here, as .NET hashes it;
 *  - a record's and a struct's twin by the `getHashCode` eqc writes from the members its `equals` reads,
 *    a vocabulary value type's hand-written twin (`Point`, `TypeStyle`, registered with
 *    {@link hashesByValue}) and an anonymous value, plain data here, member by member, in any order;
 *  - an instance of any other class by its identity, as `object.GetHashCode` is.
 */

import { exception } from './exceptions';
export function hash(value: unknown): number {
  if (value == null) return 0;
  switch (typeof value) {
    case 'number':
      return numberHash(value);
    case 'string':
      return stringHash(value);
    case 'boolean':
      return value ? 1 : 0;
    case 'bigint':
      return Number(BigInt.asIntN(32, value)) ^ Number(BigInt.asIntN(32, value >> 32n));
    case 'object':
      return objectHash(value);
    default:
      return 0;
  }
}

/** `HashCode.Combine`: the values' hashes, combined in the order they are given. */
export function hashCombine(...values: unknown[]): number {
  return ordered(values);
}

/** The hashes of a sequence's items, combined in order: a walk, not a spread, so a large array is no
 * call's argument list. */
function ordered(values: Iterable<unknown>): number {
  let combined = 17;
  for (const value of values) combined = (Math.imul(combined, 31) + hash(value)) | 0;
  return combined;
}

/** The arrays whose items are being walked, so one that holds itself is not walked again. */
const walking = new Set<unknown[]>();

/** A value tuple's hash, its items' in order. An array met again inside its own walk (`object[] a`
 * holding `a`) answers its identity's there, where the walk would otherwise never end. */
function arrayHash(items: unknown[]): number {
  if (walking.has(items)) return identityHash(items);
  walking.add(items);
  try {
    return ordered(items);
  } finally {
    walking.delete(items);
  }
}

/** .NET's refusal of an instance call on null, the message its `NullReferenceException` carries. */
const NULL_RECEIVER = 'Object reference not set to an instance of an object.';

/**
 * An instance `GetHashCode()` call on a receiver that may be null: its hash, and .NET's refusal
 * where it is null. `hash` itself answers 0 for null, as `HashCode.Combine` hashes a null argument
 * and an empty `Nullable<T>` answers, and the compiler sends those two there.
 */
export function instanceHash(value: unknown): number {
  if (value == null) throw exception('System.NullReferenceException', NULL_RECEIVER);
  return hash(value);
}

/**
 * A value's members, hashed in any order, without asking its own `getHashCode`: plain data's hash, and
 * `ValueType.GetHashCode`, which a struct's override reaches through `base` and which must not call
 * the override back.
 */
export function hashFields(value: object): number {
  let result = 0;
  for (const [key, member] of Object.entries(value)) result = (result + (Math.imul(stringHash(key), 31) ^ hash(member))) | 0;
  return result;
}

const identities = new WeakMap<object, number>();
let lastIdentity = 0;

/**
 * `object.GetHashCode` for a class that does not override it: the identity's, the same for as long as
 * the object lives, and spread so that two objects seldom share one.
 */
export function identityHash(value: object | null | undefined): number {
  if (value == null) throw exception('System.NullReferenceException', NULL_RECEIVER);
  let identity = identities.get(value);
  if (identity === undefined) {
    lastIdentity = (lastIdentity + 0x9e3779b9) | 0;
    identity = lastIdentity;
    identities.set(value, identity);
  }
  return identity;
}

const bits = new DataView(new ArrayBuffer(8));

function numberHash(value: number): number {
  // An int is its own hash, and `| 0` folds -0 into 0.
  if (Number.isInteger(value) && value >= -2147483648 && value <= 2147483647) return value | 0;
  if (value !== value) return 0x7ff80000;
  bits.setFloat64(0, value);
  return bits.getInt32(0) ^ bits.getInt32(4);
}

function stringHash(text: string): number {
  let result = 0;
  for (let at = 0; at < text.length; at++) result = (Math.imul(result, 31) + text.charCodeAt(at)) | 0;
  return result;
}

function objectHash(value: object): number {
  const own = (value as { getHashCode?: unknown }).getHashCode;
  if (typeof own === 'function') return Number((own as () => unknown).call(value)) | 0;
  if (Array.isArray(value)) return arrayHash(value);
  // Plain data (an anonymous value) and a vocabulary value type's hand-written twin by their members,
  // in any order, as `equals` matches them by name. A record's and a struct's twin answer above, from
  // the members their `equals` reads; any other class, the runtime's included, is its identity unless
  // it says otherwise.
  const prototype = Object.getPrototypeOf(value);
  return prototype === Object.prototype || prototype === null || byValue.has(prototype)
    ? hashFields(value)
    : identityHash(value);
}

/**
 * The method group `value.GetHashCode`, `Func<int> f = n.GetHashCode`: a delegate over the receiver as
 * it is when the delegate is made, refused there when the receiver is null, as .NET refuses it, an
 * empty `Nullable<T>` included, which boxes to null. `byIdentity` hashes an array as .NET does.
 */
export function hashGroup(value: unknown, byIdentity = false): () => number {
  if (value == null) throw exception('System.NullReferenceException', NULL_RECEIVER);
  return byIdentity ? () => identityHash(value as object) : () => hash(value);
}

/** A runtime class that twins a value type of the vocabulary by hand. */
export interface ValueTwin {
  readonly prototype: object;
  readonly name: string;
}

/** The prototypes of the classes registered with {@link hashesByValue}. */
const byValue = new WeakSet<object>();

/**
 * The same classes by name: what a value of one crosses a document reload by
 * ({@link valueTwinNamed}).
 */
const twinsByName = new Map<string, ValueTwin>();

/**
 * Registers the runtime classes that twin a .NET value type by hand: a record or a struct of the
 * vocabulary (`Point`, `TypeStyle`). `equals` compares an instance member by member, so its hash is
 * its members' too, where an unregistered class hashes by its identity, as a .NET class does. A twin
 * eqc emits carries its own `getHashCode` and needs none of this. Each mirror file registers its own,
 * and `ValueTwinHashTests` fails on a value type's twin left out and on a class registered that is
 * not one.
 */
export function hashesByValue(...types: ValueTwin[]): void {
  for (const type of types) {
    byValue.add(type.prototype);
    twinsByName.set(type.name, type);
  }
}

/**
 * The vocabulary value type a value is an instance of, as {@link hashesByValue} registered it, or
 * undefined: an instance of the registered class itself, never of a class derived from it.
 */
export function valueTwinOf(value: object): ValueTwin | undefined {
  const prototype = Object.getPrototypeOf(value) as { constructor?: ValueTwin } | null;
  const twin = prototype === null || !byValue.has(prototype) ? undefined : prototype.constructor;
  return twin !== undefined && twinsByName.get(twin.name) === twin ? twin : undefined;
}

/**
 * The vocabulary value type registered under `name`. A hot reload names the class of a value it
 * carries, since no class crosses a document reload, and the runtime that registered it is the same
 * runtime on both sides of the reload, so the name finds the same class (`dev/hot-reload-state.ts`).
 */
export function valueTwinNamed(name: string): ValueTwin | undefined {
  return twinsByName.get(name);
}
