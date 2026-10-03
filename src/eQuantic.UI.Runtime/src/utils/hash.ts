/**
 * `GetHashCode` as .NET's contract has it: values `Equals` finds equal hash equal. .NET's own numbers
 * are not stable across processes (a string's hash is randomized in every one), so the browser cannot
 * agree with the server's number, and nothing may depend on the number itself. Each call threw
 * instead, `getHashCode is not a function`, for a string, a number and a record alike (#519).
 *
 * The hash follows `$eq.equals` (utils/equals.ts) case by case, so `equals(a, b)` implies
 * `hash(a) === hash(b)`:
 *  - a value with its own `getHashCode` answers it: a class or a record that overrides GetHashCode,
 *    and the runtime's own types (a decimal, the dates, a dictionary);
 *  - a number as a double hashes, NaN with NaN and -0 with 0, as Equals holds them, and an int is its
 *    own hash, as in .NET; a long by its two halves; a string and a char by their UTF-16 code units;
 *    a bool as 1 or 0; null as 0;
 *  - a value tuple, an array here, element by element, in order;
 *  - a record, a struct or an anonymous type, a value that compares by `equals` or plain data, member
 *    by member, in any order, as `equals` reads them by name;
 *  - an instance of any other class by its identity, as `object.GetHashCode` is.
 */
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
  let combined = 17;
  for (const value of values) combined = (Math.imul(combined, 31) + hash(value)) | 0;
  return combined;
}

const identities = new WeakMap<object, number>();
let lastIdentity = 0;

/**
 * `object.GetHashCode` for a class that does not override it: the identity's, the same for as long as
 * the object lives, and spread so that two objects seldom share one.
 */
export function identityHash(value: object): number {
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
  if (Array.isArray(value)) return hashCombine(...value);
  const prototype = Object.getPrototypeOf(value);
  const data = prototype === Object.prototype || prototype === null;
  if (!data && typeof (value as { equals?: unknown }).equals !== 'function') return identityHash(value);
  // In any order: `equals` matches the members by name, whatever order each value was built in.
  let result = 0;
  for (const [key, member] of Object.entries(value)) result = (result + (Math.imul(stringHash(key), 31) ^ hash(member))) | 0;
  return result;
}
