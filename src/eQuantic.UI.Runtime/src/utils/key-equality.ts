/**
 * How two values of a type are equal, as .NET's `EqualityComparer<T>.Default` finds them, which the
 * compiler decides from the static type (`ElementEquality`) and writes as ONE argument:
 *
 *  - `false`, or nothing: by IDENTITY, SameValueZero — a number (NaN equal to NaN and -0 to 0, as a
 *    double's `Equals` holds them), a string, a char, a bool, a long, an enum, an array, a delegate;
 *  - `true`: by VALUE, through `$eq.equals` — a record, a struct, a decimal, a date, and a tuple or an
 *    anonymous type whose members all compare so;
 *  - `'own'`: by what the value turns out to be, for a type that does not decide (`object`, an
 *    interface a tuple implements, a type parameter): its twin's own `equals`, a tuple's or an
 *    anonymous type's members, and identity for anything else;
 *  - `'item'`: by identity or the twin's own `equals`, never by members, for a class and an interface
 *    no tuple implements: a `List<int>` is an array here, and .NET finds it by reference where its
 *    elements found another;
 *  - `'gen'`: a type parameter's, which .NET settles at run time: the value's `IEquatable<T>` over its own
 *    type where its twin answers one (`static $equatable`), and what the value turns out to be otherwise,
 *    as `'own'` finds it ({@link sameGeneric});
 *  - a function: a tuple, an anonymous type or a pair with a member that compares otherwise, an array
 *    inside a tuple by reference above all, generated from the member types ({@link tupleEquality}).
 *    `$eq.equals` walks an array element by element, and `ValueTuple.Equals` compares an array member
 *    with `EqualityComparer<int[]>.Default`, by reference: a tuple holding an array was found where
 *    .NET finds nothing (#425).
 *
 * ONE vocabulary for every search .NET decides by that comparer: a dictionary's keys and a set's
 * elements (their slots, `utils/slots.ts`), a list's and an array's `IndexOf`, `LastIndexOf`,
 * `Contains` and `Remove` (`utils/list.ts`), and LINQ's `Contains`. A list compared with `===` where
 * the set beside it compared by value answered two ways about one value.
 */
import { equals } from './equals';
import { valueTwinOf } from './hash';

/** A comparison two values are found equal by. */
export type Equality = (a: unknown, b: unknown) => boolean;

/** How a value of a type is found equal to another: see the module's description. */
export type KeyEquality = boolean | 'own' | 'item' | 'gen' | Equality;

/**
 * `EqualityComparer<T>.Default` for a type compared by reference or by its own `Equals`: identity, a
 * double's where NaN equals NaN, and a twin's own `equals` (a record, a struct, a decimal, a date, a
 * class that overrides it). A class that does not override `Equals` compares by identity.
 */
export function sameItem(item: unknown, value: unknown): boolean {
  if (item === value) return true;
  if (typeof item === 'number' && typeof value === 'number') return item !== item && value !== value;
  if (item == null || value == null) return item == null && value == null;
  const own = (item as { equals?: unknown }).equals;
  return typeof own === 'function' && (own as (other: unknown) => boolean).call(item, value);
}

/**
 * .NET's `EqualityComparer<object>.Default` on the values two keys turned out to be: identity (NaN
 * equal to NaN), a twin's own `equals` (a record, a struct, a decimal, a date, a class overriding
 * `Equals`), and the members of a tuple or an anonymous type, which have no twin to carry one, each
 * member compared so in turn. Two of those are compared only with one of their own kind, as .NET's
 * Equals checks the type first: a tuple never equals an anonymous object, and an anonymous object
 * equals only one of the same members in the same order, never a record with the same members.
 *
 * A tuple is an array here, and so are a `List<T>` and a `T[]`, which .NET compares by reference. Held
 * as `object`, nothing tells them apart, so all three compare by their elements, the tuple's answer: a
 * tuple held as `object` is found in a set, a dictionary and `Distinct` as .NET finds it. Where the
 * compiler still sees the static type (a list, an array, a class that does not override `Equals`), it
 * compares by reference before it gets here (#909).
 */
export function sameKey(a: unknown, b: unknown): boolean {
  if (sameItem(a, b)) return true;
  // A vocabulary value type's hand-written twin (`Point`, registered with `hashesByValue`) carries no
  // `equals` and is no plain data: its record struct's Equals compares it member by member, and only with a
  // value of its own type, as `$eq.equals` and its hash already do. By reference, two tuples held as object
  // holding two equal points were unequal (Copilot's second review of #947).
  const twin = valueTwinIn(a);
  if (twin !== undefined) return valueTwinIn(b) === twin && equals(a, b);
  if (!isPlainValue(a) || !isPlainValue(b)) return false;
  if (Array.isArray(a) || Array.isArray(b)) {
    if (!Array.isArray(a) || !Array.isArray(b) || a.length !== b.length) return false;
    for (let at = 0; at < a.length; at++) if (!sameKey(a[at], b[at])) return false;
    return true;
  }
  const ao = a as Record<string, unknown>;
  const bo = b as Record<string, unknown>;
  const ak = Object.keys(ao);
  const bk = Object.keys(bo);
  if (ak.length !== bk.length) return false;
  for (let i = 0; i < ak.length; i++) {
    const k = ak[i];
    if (k !== bk[i] || !sameKey(ao[k], bo[k])) return false;
  }
  return true;
}

/** The contract `IEquatable<T>` over the value's own type, which `EqualityComparer<T>.Default` asks. */
const SELF = 'System.IEquatable<T>|@';

/** What a value's twin maps in `static $equatable`: each contract it answers to the method answering it. */
function contractsOf(value: unknown): Record<string, string> | undefined {
  if (value === null || typeof value !== 'object') return undefined;
  const map = (value as { constructor?: { $equatable?: unknown } }).constructor?.$equatable;
  return map !== null && typeof map === 'object' ? (map as Record<string, string>) : undefined;
}

/**
 * The method a value's twin answers an interface's typed `Equals` with, found by the CONTRACT a call binds
 * (`System.IEquatable<T>|int`, `App.IProbe|App.IProbe`): its class's `static $equatable` maps each
 * such contract to the method, which holds a name of its own on the twin, since the twin's `equals` is
 * `Equals(object)`'s. A contract over a type parameter (`|*`, generic code, whose type argument is erased)
 * takes the class's own `IEquatable<T>` (`|@`), or the one contract of that interface it answers; a class
 * that answers it over several other types and not over itself is answered by `Equals(object)`. Undefined
 * for a value that answers none: a number, a string, a record's synthesized one, any value of .NET's own types.
 */
function contractMethod(value: unknown, key: string): ((other: unknown) => unknown) | undefined {
  const named = contractsOf(value);
  if (named === undefined) return undefined;
  let name = named[key];
  if (name === undefined && key.endsWith('|*')) {
    const face = key.slice(0, -1);
    name = named[`${face}@`];
    if (name === undefined) {
      const answered = Object.keys(named).filter((contract) => contract.startsWith(face));
      if (answered.length === 1) name = named[answered[0]];
    }
  }
  const method = name === undefined ? undefined : (value as Record<string, unknown>)[name];
  return typeof method === 'function' ? (method as (other: unknown) => unknown) : undefined;
}

/** Whether a value's twin answers `IEquatable<T>` over its own type, which `'gen'` asks before `Equals(object)`. */
export function answersEquatable(value: unknown): boolean {
  return contractsOf(value)?.[SELF] !== undefined;
}

/** The interface's typed `Equals` of `key` on `a` with `b`, and what `a` turns out to be where it answers none. */
function dispatch(key: string, a: unknown, b: unknown): boolean {
  const method = contractMethod(a, key);
  return method !== undefined ? Boolean(method.call(a, b)) : sameKey(a, b);
}

/**
 * An interface's typed `Equals` called, or made a delegate of, through the contract the call binds:
 * `IEquatable<int> c = new C(); c.Equals(1)` asks C's `Equals(int)`, whatever else C answers. The method
 * decides what a null argument means and runs on it, side effects included, as the call does in .NET
 * (Copilot's third review of #947); the receiver's null is the caller's (`$eq.equalsInstance`).
 */
export function equatableCall(key: string): Equality {
  return generated(`c:${key}`, () => (a, b) => dispatch(key, a, b));
}

/**
 * `EqualityComparer<T>.Default` for a type that implements `IEquatable<T>` with a typed `Equals` of the
 * app's own: a null equals only a null, as .NET's comparer answers before it asks the method, and two
 * values otherwise by that method, found by the contract.
 */
export function equatable(key: string): Equality {
  return generated(`q:${key}`, () => (a, b) => (a == null || b == null ? a == null && b == null : dispatch(key, a, b)));
}

/**
 * `EqualityComparer<T>.Default` for a type parameter, which .NET settles at run time: `IEquatable<T>` over
 * the value's own type where its twin answers it, and what it turns out to be otherwise ({@link sameKey}),
 * a null equal only to a null. Never a contract over another type: a `C : IEquatable<int>` is compared by
 * `Equals(object)`, as .NET's comparer for C compares it. Through `'own'`, a generic `List<T>.Contains`
 * never asked the typed `Equals` an IEquatable class answers with (Copilot's third review of #947).
 */
export function sameGeneric(a: unknown, b: unknown): boolean {
  if (a == null || b == null) return a == null && b == null;
  return dispatch(SELF, a, b);
}

/** A tuple (an array) or an anonymous type (a plain object): a value compared by its members. */
export function isPlainValue(value: unknown): boolean {
  if (Array.isArray(value)) return true;
  if (value === null || typeof value !== 'object') return false;
  const prototype = Object.getPrototypeOf(value);
  return prototype === Object.prototype || prototype === null;
}

/** The vocabulary value type a value is an instance of, as `hashesByValue` registered it, or undefined. */
function valueTwinIn(value: unknown): ReturnType<typeof valueTwinOf> {
  return value !== null && typeof value === 'object' ? valueTwinOf(value) : undefined;
}

/** Whether a value has an equality of its own, which under `'own'` is asked rather than identity. */
export function hasOwnEquality(value: unknown): boolean {
  return hasEquals(value) || isPlainValue(value) || valueTwinIn(value) !== undefined;
}

/** Whether a value's twin carries an `equals`, which under `'item'` is asked rather than identity. */
export function hasEquals(value: unknown): boolean {
  return value !== null && typeof value === 'object' && typeof (value as { equals?: unknown }).equals === 'function';
}

/** SameValueZero, a `Map`'s equality and a number's `Equals`: identity, NaN equal to NaN and -0 to 0. */
export function sameValueZero(a: unknown, b: unknown): boolean {
  return a === b || (a !== a && b !== b);
}

/** The comparison an equality stands for. */
export function sameBy(equality: KeyEquality | null | undefined): Equality {
  if (typeof equality === 'function') return equality;
  if (equality === true) return equals;
  if (equality === 'gen') return sameGeneric;
  return equality === 'own' ? sameKey : equality === 'item' ? sameItem : sameValueZero;
}

/**
 * Each comparison's SHAPE, which names it whatever closure holds it: two tuples of one type compare by
 * one function, so two sets of them hold one comparer, as two .NET sets of one element type do. A set
 * asks that before it copies another's slots or takes its fast path ({@link sameEquality}).
 */
const shapes = new WeakMap<Equality, string>([
  [equals, 'v'],
  [sameKey, 'o'],
  [sameItem, 'r'],
  [sameValueZero, 'i'],
  [sameGeneric, 'g'],
]);
const byShape = new Map<string, Equality>();
let foreign = 0;

function shapeOf(equality: KeyEquality | null | undefined): string {
  if (typeof equality !== 'function') {
    return equality === true ? 'v' : equality === 'own' ? 'o' : equality === 'item' ? 'r' : equality === 'gen' ? 'g' : 'i';
  }
  let shape = shapes.get(equality);
  if (shape === undefined) {
    shape = `f${++foreign}`;
    shapes.set(equality, shape);
  }
  return shape;
}

/** The one comparison of a shape, made the first time it is asked for. */
function generated(shape: string, make: () => Equality): Equality {
  let found = byShape.get(shape);
  if (found === undefined) {
    found = make();
    shapes.set(found, shape);
    byShape.set(shape, found);
  }
  return found;
}

/** Whether two equalities are one comparison: what .NET's `EqualityComparersAreEqual` asks of two sets. */
export function sameEquality(a: KeyEquality | null | undefined, b: KeyEquality | null | undefined): boolean {
  return shapeOf(a) === shapeOf(b);
}

/**
 * A value tuple's `Equals`, generated by eqc from its element types: each element by its own type's
 * equality, a null tuple (a `Nullable` one) equal only to another. `ValueTuple.Equals` compares each
 * element with that element type's `EqualityComparer<T>.Default`, so an array element by reference
 * and a nested tuple by its own elements.
 */
export function tupleEquality(...elements: KeyEquality[]): Equality {
  return generated(`(${elements.map(shapeOf).join(',')})`, () => {
    const same = elements.map(sameBy);
    return (a, b) => {
      if (a == null || b == null) return a == null && b == null;
      // `Equals(object)` with a tuple of another arity, which is another type to .NET: the elements
      // past the receiver's were never read, so `(a, 1)` equalled `(a, 1, 2)`.
      if (!Array.isArray(a) || !Array.isArray(b) || a.length !== same.length || b.length !== same.length) return false;
      const left = a as readonly unknown[];
      const right = b as readonly unknown[];
      for (let at = 0; at < same.length; at++) if (!same[at](left[at], right[at])) return false;
      return true;
    };
  });
}

/**
 * An anonymous type's `Equals`, generated by eqc from its members' types: each member, by the name it
 * crosses as, compared by its own type's equality, as the compiler-written `Equals` of an anonymous
 * type compares each with `EqualityComparer<T>.Default`. It is `Equals(object)`, which .NET answers
 * false for a value of another type before it reads a member: an anonymous object of other members,
 * or of the same members in another order, is another type, and so are a record and a tuple, so the
 * other value has to be plain data of exactly these members, in this order (#743). The order is the
 * one the compiler writes the object literal in, the C# declaration's.
 */
export function memberEquality(members: Readonly<Record<string, KeyEquality>>): Equality {
  const names = Object.keys(members);
  return generated(`{${names.map((name) => `${name}:${shapeOf(members[name])}`).join(',')}}`, () => {
    const same = names.map((name) => sameBy(members[name]));
    return (a, b) => {
      if (a == null || b == null) return a == null && b == null;
      if (!holdsExactly(a, names) || !holdsExactly(b, names)) return false;
      const left = a as Readonly<Record<string, unknown>>;
      const right = b as Readonly<Record<string, unknown>>;
      for (let at = 0; at < names.length; at++) {
        if (!same[at](left[names[at]], right[names[at]])) return false;
      }
      return true;
    };
  });
}

/** Whether a value is an anonymous object of exactly these members, in this order: plain data that
 * is not a tuple (an array), whose own keys are the names. */
function holdsExactly(value: unknown, names: readonly string[]): boolean {
  if (Array.isArray(value) || !isPlainValue(value)) return false;
  const keys = Object.keys(value as object);
  return keys.length === names.length && keys.every((key, at) => key === names[at]);
}

/**
 * `EqualityComparer<KeyValuePair<K, V>>.Default`, which compares the pair's two halves as each one's
 * own comparer does (`ValueType.Equals` over its fields), and `Dictionary`'s
 * `ICollection<KeyValuePair<K, V>>.Remove`, which compares the value by `V`'s. The compiler picks
 * each half's equality from its static type (found in review, #421): a tuple value is an array here,
 * and only the type says it compares by value. It reads `.key` and `.value`, which both shapes of a
 * pair have: a dictionary's entry (an array that carries them) and a plain pair.
 */
export function pairComparer<K, V>(
  key: KeyEquality,
  value: KeyEquality,
): (a: { key: K; value: V }, b: { key: K; value: V }) => boolean {
  return generated(`[${shapeOf(key)},${shapeOf(value)}]`, () => {
    const sameKeys = sameBy(key);
    const sameValues = sameBy(value);
    return (a, b) => {
      // A null pair, a `Nullable` one, is equal only to another, before either is read.
      if (a == null || b == null) return a == null && b == null;
      const left = a as { key: K; value: V };
      const right = b as { key: K; value: V };
      return sameKeys(left.key, right.key) && sameValues(left.value, right.value);
    };
  });
}
