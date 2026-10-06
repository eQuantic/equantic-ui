/**
 * How two values of a type are equal, as .NET's `EqualityComparer<T>.Default` finds them, which the
 * compiler decides from the static type (`ElementEquality`) and writes as ONE argument:
 *
 *  - `false`, or nothing: by IDENTITY, SameValueZero — a number (NaN equal to NaN and -0 to 0, as a
 *    double's `Equals` holds them), a string, a char, a bool, a long, an enum, an array, a delegate;
 *  - `true`: by VALUE, through `$eq.equals` — a record, a struct, a decimal, a date, and a tuple or an
 *    anonymous type whose members all compare so;
 *  - `'own'`: by what the value turns out to be, for a type that does not decide (`object`, an
 *    interface, a type parameter, a class a subclass may override `Equals` in): its twin's own
 *    `equals`, a tuple's or an anonymous type's members, and identity for anything else;
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

/** A comparison two values are found equal by. */
export type Equality = (a: unknown, b: unknown) => boolean;

/** How a value of a type is found equal to another: see the module's description. */
export type KeyEquality = boolean | 'own' | Equality;

/**
 * `EqualityComparer<T>.Default` for a type compared by reference or by its own `Equals`: identity, a
 * double's where NaN equals NaN, and a twin's own `equals` (a record, a struct, a decimal, a date, a
 * class that overrides it). A class that does not override `Equals` compares by identity.
 */
export function sameItem(item: unknown, value: unknown): boolean {
  if (item === value) return true;
  if (typeof item === 'number' && typeof value === 'number') return item !== item && value !== value;
  if (item == null || value == null) return false;
  const own = (item as { equals?: unknown }).equals;
  return typeof own === 'function' && (own as (other: unknown) => boolean).call(item, value);
}

/**
 * .NET's `EqualityComparer<object>.Default` on the values two keys turned out to be: identity (NaN
 * equal to NaN), a twin's own `equals` (a record, a struct, a decimal, a date, a class overriding
 * `Equals`), and the members of a tuple or an anonymous type, which have no twin to carry one. Two
 * of those are compared only with one of their own kind, as .NET's Equals checks the type first: an
 * anonymous type never equals a record with the same members.
 */
export function sameKey(a: unknown, b: unknown): boolean {
  if (sameItem(a, b)) return true;
  return isPlainValue(a) && isPlainValue(b) && Array.isArray(a) === Array.isArray(b) && equals(a, b);
}

/** A tuple (an array) or an anonymous type (a plain object): a value compared by its members. */
export function isPlainValue(value: unknown): boolean {
  if (Array.isArray(value)) return true;
  if (value === null || typeof value !== 'object') return false;
  const prototype = Object.getPrototypeOf(value);
  return prototype === Object.prototype || prototype === null;
}

/** Whether a value has an equality of its own, which under `'own'` is asked rather than identity. */
export function hasOwnEquality(value: unknown): boolean {
  return (
    value !== null &&
    typeof value === 'object' &&
    (typeof (value as { equals?: unknown }).equals === 'function' || isPlainValue(value))
  );
}

/** SameValueZero, a `Map`'s equality and a number's `Equals`: identity, NaN equal to NaN and -0 to 0. */
export function sameValueZero(a: unknown, b: unknown): boolean {
  return a === b || (a !== a && b !== b);
}

/** The comparison an equality stands for. */
export function sameBy(equality: KeyEquality | null | undefined): Equality {
  if (typeof equality === 'function') return equality;
  return equality === true ? equals : equality === 'own' ? sameKey : sameValueZero;
}

/**
 * Each comparison's SHAPE, which names it whatever closure holds it: two tuples of one type compare by
 * one function, so two sets of them hold one comparer, as two .NET sets of one element type do. A set
 * asks that before it copies another's slots or takes its fast path ({@link sameEquality}).
 */
const shapes = new WeakMap<Equality, string>([
  [equals, 'v'],
  [sameKey, 'o'],
  [sameValueZero, 'i'],
]);
const byShape = new Map<string, Equality>();
let foreign = 0;

function shapeOf(equality: KeyEquality | null | undefined): string {
  if (typeof equality !== 'function') return equality === true ? 'v' : equality === 'own' ? 'o' : 'i';
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
 * type compares each with `EqualityComparer<T>.Default`.
 */
export function memberEquality(members: Readonly<Record<string, KeyEquality>>): Equality {
  const names = Object.keys(members);
  return generated(`{${names.map((name) => `${name}:${shapeOf(members[name])}`).join(',')}}`, () => {
    const same = names.map((name) => sameBy(members[name]));
    return (a, b) => {
      if (a == null || b == null) return a == null && b == null;
      const left = a as Readonly<Record<string, unknown>>;
      const right = b as Readonly<Record<string, unknown>>;
      for (let at = 0; at < names.length; at++) {
        if (!same[at](left[names[at]], right[names[at]])) return false;
      }
      return true;
    };
  });
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
      const left = a as { key: K; value: V };
      const right = b as { key: K; value: V };
      return sameKeys(left.key, right.key) && sameValues(left.value, right.value);
    };
  });
}
