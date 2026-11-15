/**
 * What a value held as plain JavaScript is in .NET, where its shape alone cannot say (#742, #954). The browser
 * holds a `List<T>`, a `T[]` and a value tuple as one array, and an anonymous object and a value held as data
 * (a `Color`, a `Curve`) as one plain object, so once C# holds them as `object` the runtime compared them by
 * their shape: two lists with the same items were equal, as two tuples must be, and an anonymous object with a
 * color's members was that color.
 *
 * A mark says which. It lives in a WeakSet, not on the value, so JSON, `Object.keys`, a spread and a member walk
 * never see it, and a value leaves it behind when it is collected. A list and an array are marked where C#
 * converts them to `object` or an interface, which is where their static type stops saying what they are; an
 * anonymous object where it is made, and again where `with` copies it.
 */

const references = new WeakSet<object>();
const anonymous = new WeakSet<object>();

/**
 * A list or an array on its way into `object` or an interface: compared and hashed by its identity from now on.
 * Only an array is marked: a `List<T>` and a `T[]` are arrays here, while what else an `IEnumerable<T>` holds (a
 * record of the app's, a set, a dictionary) answers its equality by its own type, a record by value, and a mark on
 * it compared a record by reference.
 */
export function asReference<T>(value: T): T {
  if (Array.isArray(value)) references.add(value);
  return value;
}

/** Whether `value` is a list or an array that went into `object`, which .NET compares by reference. */
export function isReference(value: unknown): boolean {
  return typeof value === 'object' && value !== null && references.has(value);
}

/** An anonymous object where eqc makes or copies one. */
export function anonymousObject<T extends object>(value: T): T {
  anonymous.add(value);
  return value;
}

/** Whether `value` is an anonymous object, which is never equal to a value held as data of the same members. */
export function isAnonymous(value: unknown): boolean {
  return typeof value === 'object' && value !== null && anonymous.has(value);
}

/**
 * The tag a box carries (`utils/box.ts`), read where a module the box itself imports meets one: an enum's text
 * handed a member that was boxed for a call whose lowering prints it by its type (`StringBuilder.Append(object)`,
 * a `Console.WriteLine(object)`), which is the member's text either way.
 */
export const BOXED = Symbol('eq.boxed');

/** The value a box holds, or the value itself where it is no box. */
export function heldIn(value: unknown): unknown {
  return typeof value === 'object' && value !== null && (value as { [BOXED]?: boolean })[BOXED] === true
    ? (value as { value: unknown }).value
    : value;
}
