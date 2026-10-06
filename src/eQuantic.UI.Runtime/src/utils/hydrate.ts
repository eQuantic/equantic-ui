import { dec, Decimal } from './decimal';
import { long } from './long';
import { dateTime, timeSpan, dateOnly, timeOnly, dateTimeOffset } from './datetime';
import { adoptMember } from './adopt-member';
import { Dictionary } from './dictionary';
import { SortedMap, SortedSet } from './sorted';
import { comparerOf, type Ordering } from './ordering';
import { LinkedList, Queue, Stack } from './collections';
import { hashSet } from './hash-set';
import type { KeyEquality } from './key-equality';

/**
 * TYPED hydration — the boundary where a value from the server (SSR state, a Server Action result)
 * becomes the runtime type the transpiled code computes with.
 *
 * The wire protocol (`EqJson`) sends what JavaScript cannot represent natively as STRINGS: `long`
 * as "9007199254740993", `decimal` as "0.1", dates as ISO-8601. The compiler KNOWS the C# type of
 * every state field and every Server Action's return, so it emits a small spec — this module's
 * input — and the value is coerced ONCE, here, instead of defensively at every use site.
 *
 * A spec says what a value IS:
 *  - a tag (`'long'`, `'decimal'`, `'dateTime'`, …) — a compat scalar, restored by its factory;
 *    `'single'` is the one whose wire form is already a number: a C# `float` travels as the
 *    shortest text that names IT, which JavaScript reads as the nearest DOUBLE — so it rounds back
 *    to the single here, before any arithmetic sees the difference;
 *  - `[spec]` — a list whose every element hydrates by the inner spec;
 *  - `{ collection, of, order, byValue }` — a collection the browser holds as its own class: the JSON
 *    array becomes the runtime's `HashSet` (its elements found as `byValue` says), `SortedSet` (in its
 *    element type's `order`), `Queue`, `Stack` or `LinkedList`, each element hydrated by `of` (null
 *    when elements arrive as they are);
 *  - `{ dict: spec, key, byValue, sorted, order }` — a dictionary: the JSON object becomes the
 *    runtime's `Dictionary` (a `SortedMap` in its key type's `order` when `sorted`), each property
 *    name turned into the key by `key` and each value hydrated by `dict` (null when values arrive as
 *    they are);
 *  - a class reference — a record/struct twin: the plain JSON object is rebuilt on the class's
 *    prototype (so `instanceof`, `equals`, `with` survive the wire) and each member hydrates by
 *    the class's own static `$hydration` map;
 *  - `{ members, of, typeArguments }` — a type from a referenced assembly, coerced member by member
 *    onto a copy, built on its twin `of` when the runtime ships one; `typeArguments` are the specs
 *    of the type arguments that twin revives (`ServerTopic<T>`), kept in the members its
 *    `$typeArguments` names, as the constructor eqc calls keeps them.
 *
 * Every branch is idempotent: a value that already has its runtime type passes through, so
 * hydrating twice (or hydrating a value that never crossed the wire) is harmless.
 */

/** The scalar compat types whose wire form is a string. */
export type HydrationTag =
  | 'decimal'
  | 'long'
  | 'single'
  | 'dateTime'
  | 'timeSpan'
  | 'dateOnly'
  | 'timeOnly'
  | 'dateTimeOffset';

/**
 * How a JSON property name becomes a dictionary key: a number, a bool, or a compat scalar by its tag.
 * A spec that names none keeps the name itself, which is the key of a string, a char, a `Guid` and an
 * enum (its camelCase member name, the value transpiled code compares).
 */
export type HydrationKey = HydrationTag | 'number' | 'bool';

/** A dictionary: how its values hydrate, how a property name becomes its key, and which class holds it. */
export interface DictionarySpec {
  readonly dict: HydrationSpec | null;
  readonly key?: HydrationKey;
  readonly byValue?: KeyEquality;
  /** A sorted one, and which of .NET's two: a `SortedList` refuses a key already there in its own words. */
  readonly sorted?: 'dictionary' | 'list';
  /** How a sorted one orders its keys, by their type; absent for a type with no order of its own here. */
  readonly order?: Ordering;
}

/** A collection the browser holds as one of its own classes, and how each element hydrates. */
export interface CollectionSpec {
  readonly collection: 'set' | 'sortedSet' | 'queue' | 'stack' | 'linkedList';
  readonly of: HydrationSpec | null;
  /** How a sorted set orders its elements, by their type; absent for a type with no order of its own here. */
  readonly order?: Ordering;
  /** How a set finds its elements, by their type ({@link KeyEquality}); absent for identity. */
  readonly byValue?: KeyEquality;
}

/** A record/struct twin: a prototype to rebuild on, and its own member specs. */
export interface HydratableConstructor {
  readonly prototype: object;
  readonly $hydration?: Readonly<Record<string, HydrationSpec>>;
  /** The members that hold the specs of the type arguments it revives, in their parameters' order. */
  readonly $typeArguments?: readonly string[];
}

export type HydrationSpec =
  | HydrationTag
  | readonly [HydrationSpec]
  | DictionarySpec
  | CollectionSpec
  | { readonly tuple: readonly (HydrationSpec | null)[] }
  | {
      readonly members: Readonly<Record<string, HydrationSpec>>;
      readonly of?: HydratableConstructor;
      readonly typeArguments?: readonly (HydrationSpec | null)[];
    }
  | HydratableConstructor;

/** The value coerced to what the spec says it is. Null and undefined pass through untouched. */
export function hydrate(incoming: unknown, spec: HydrationSpec): unknown {
  if (incoming == null) return incoming;
  if (typeof spec === 'string') return scalar(incoming, spec);
  if (typeof spec === 'function') return instance(incoming, spec as HydratableConstructor);
  if (Array.isArray(spec)) {
    const inner = (spec as readonly [HydrationSpec])[0];
    return Array.isArray(incoming) ? incoming.map((element) => hydrate(element, inner)) : incoming;
  }
  // A TUPLE is positional: element i hydrates by spec i, and a null position passes through.
  if ('tuple' in (spec as { tuple?: readonly (HydrationSpec | null)[] })) {
    const parts = (spec as { tuple: readonly (HydrationSpec | null)[] }).tuple;
    if (!Array.isArray(incoming)) return incoming;
    return incoming.map((element, i) => (parts[i] == null ? element : hydrate(element, parts[i]!)));
  }
  // A type from a referenced assembly coerces STRUCTURALLY: a shallow COPY of the plain object
  // with the named members hydrated, everything else verbatim. A copy, not a mutation: the payload
  // object may still be read by whoever else holds it. When the type is one the RUNTIME ships (a
  // vocabulary value type such as Rect), `of` names its twin and the copy is built on that
  // prototype; without it a Rect in a payload arrived as a plain object, and its getters and
  // methods were gone.
  if ('members' in (spec as { members?: Readonly<Record<string, HydrationSpec>> })) {
    const { members, of, typeArguments } = spec as {
      members: Readonly<Record<string, HydrationSpec>>;
      of?: HydratableConstructor;
      typeArguments?: readonly (HydrationSpec | null)[];
    };
    if (typeof incoming !== 'object' || Array.isArray(incoming)) return incoming;
    if (of && incoming instanceof (of as unknown as new (...args: never[]) => object))
      return incoming;
    const source = incoming as Record<string, unknown>;
    const result = (of ? Object.create(of.prototype) : {}) as object;
    for (const key of Object.keys(source))
      adoptMember(
        result,
        key,
        ownSpec(members, key) !== undefined ? hydrate(source[key], members[key]) : source[key],
      );
    // The specs of the type arguments the twin revives, which the wire never carries: the C# type
    // knows them and JSON does not. A topic that crossed without them revived no payload.
    if (of?.$typeArguments !== undefined && typeArguments !== undefined)
      of.$typeArguments.forEach((member, i) => adoptMember(result, member, typeArguments[i] ?? null));
    return result;
  }
  if ('dict' in (spec as DictionarySpec)) return dictionary(incoming, spec as DictionarySpec);
  if ('collection' in (spec as CollectionSpec)) return collection(incoming, spec as CollectionSpec);
  return incoming;
}

/**
 * A collection from the array System.Text.Json wrote for it, in the order the collection enumerates
 * on the server. A stack enumerates from its top, which the runtime's `Stack` keeps at the END of its
 * items, so it is built from the reversed array and its top still comes off first. A value that is
 * already its class is not an array, and passes through.
 */
function collection(incoming: unknown, spec: CollectionSpec): unknown {
  if (!Array.isArray(incoming)) return incoming;
  const items = spec.of == null ? incoming : incoming.map((element) => hydrate(element, spec.of!));
  switch (spec.collection) {
    case 'set':
      return hashSet(spec.byValue ?? false, items);
    case 'sortedSet':
      return new SortedSet(items, spec.order === undefined ? undefined : comparerOf(spec.order));
    case 'queue':
      return new Queue(items);
    case 'stack':
      return new Stack(items.slice().reverse());
    case 'linkedList':
      return new LinkedList(items);
  }
}

/**
 * A dictionary from the JSON object System.Text.Json wrote for it, its entries in the order the parsed
 * object holds them: every name but an integer-like one keeps the order it was written in (#437).
 */
function dictionary(incoming: unknown, spec: DictionarySpec): unknown {
  if (incoming instanceof Dictionary || incoming instanceof SortedMap) return incoming;
  if (typeof incoming !== 'object' || Array.isArray(incoming)) return incoming;
  const source = incoming as Record<string, unknown>;
  const entries = Object.keys(source).map(
    (name) =>
      [
        spec.key === undefined ? name : dictionaryKey(name, spec.key),
        spec.dict == null ? source[name] : hydrate(source[name], spec.dict),
      ] as const,
  );
  return spec.sorted
    ? new SortedMap(entries, spec.order === undefined ? undefined : comparerOf(spec.order), spec.sorted)
    : new Dictionary(entries, spec.byValue ?? false);
}

/** A dictionary key from the property name System.Text.Json wrote for it. */
function dictionaryKey(name: string, key: HydrationKey): unknown {
  switch (key) {
    case 'number':
      return Number(name);
    case 'single':
      return Math.fround(Number(name));
    // Written True or False, and read in any case, as System.Text.Json reads a bool key.
    case 'bool':
      return name.trim().toLowerCase() === 'true';
    default:
      return scalar(name, key);
  }
}

/** A compat scalar from its wire form — pass-through when it already has the runtime type. */
function scalar(incoming: unknown, tag: HydrationTag): unknown {
  switch (tag) {
    case 'decimal':
      return incoming instanceof Decimal
        ? incoming
        : typeof incoming === 'string' || typeof incoming === 'number'
          ? dec(incoming)
          : incoming;
    case 'long':
      return typeof incoming === 'bigint'
        ? incoming
        : typeof incoming === 'string' || typeof incoming === 'number'
          ? long(incoming)
          : incoming;
    case 'single':
      return typeof incoming === 'number' ? Math.fround(incoming) : incoming;
    case 'dateTime':
      return typeof incoming === 'string' ? dateTime.parse(incoming) : incoming;
    case 'timeSpan':
      return typeof incoming === 'string' ? timeSpan.parse(incoming) : incoming;
    case 'dateOnly':
      return typeof incoming === 'string' ? dateOnly.parse(incoming) : incoming;
    case 'timeOnly':
      return typeof incoming === 'string' ? timeOnly.parse(incoming) : incoming;
    case 'dateTimeOffset':
      return typeof incoming === 'string' ? dateTimeOffset.parse(incoming) : incoming;
  }
}

/**
 * A record/struct twin rebuilt from its plain JSON form: the prototype restored (methods,
 * `instanceof`), every member the class's `$hydration` names coerced, everything else copied
 * verbatim. A value that is already an instance passes through.
 */
function instance(incoming: unknown, ctor: HydratableConstructor): unknown {
  if (incoming instanceof (ctor as unknown as new (...args: never[]) => object)) return incoming;
  if (typeof incoming !== 'object' || Array.isArray(incoming)) return incoming;
  const source = incoming as Record<string, unknown>;
  const rebuilt = Object.create(ctor.prototype) as object;
  for (const key of Object.keys(source)) {
    const spec = ownSpec(ctor.$hydration, key);
    adoptMember(rebuilt, key, spec !== undefined ? hydrate(source[key], spec) : source[key]);
  }
  return rebuilt;
}

/**
 * The spec a map gives `key` ITSELF. The key comes from the payload, and a plain lookup answers for
 * `constructor` or `__proto__` with what every object inherits: a function, which reads as a twin.
 */
function ownSpec(
  specs: Readonly<Record<string, HydrationSpec>> | undefined,
  key: string,
): HydrationSpec | undefined {
  return specs !== undefined && Object.prototype.hasOwnProperty.call(specs, key)
    ? specs[key]
    : undefined;
}
