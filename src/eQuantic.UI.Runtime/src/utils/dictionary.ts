import { sameItem } from './collections';
import { equals } from './equals';

/**
 * How a dictionary finds a key, as .NET's default comparer for the key type does, which eqc says:
 * `false` by IDENTITY, SameValueZero through a `Map`; `true` by VALUE, `$eq.equals` over the live
 * slots; and `'own'` by what the key turns out to be, for a key type that does not decide (`object`,
 * an interface, a type parameter, a class a subclass may override `Equals` in): its twin's own
 * `equals`, a tuple's or an anonymous type's members, and identity for anything else, which keeps
 * that majority in the `Map`.
 */
export type KeyEquality = boolean | 'own';

/**
 * A pair a dictionary enumerates: it destructures as `[key, value]` and reads `.key` and `.value`,
 * the two ways C# takes a `KeyValuePair` apart (`foreach (var (k, v) in d)` and `kv.Key`).
 */
export type Pair<K, V> = [K, V] & { readonly key: K; readonly value: V };

/** A pair of both shapes. */
export function pair<K, V>(key: K, value: V): Pair<K, V> {
  return Object.assign([key, value] as [K, V], { key, value });
}

/**
 * .NET's `Dictionary<TKey, TValue>`, the class every C# `Dictionary`, `IDictionary` and
 * `IReadOnlyDictionary` lowers to. `SortedDictionary` and `SortedList` keep their own (`sorted.ts`).
 *
 * Entries are held by SLOT, as .NET holds them: an insertion takes the slot a removal freed last, or
 * the next one, and enumeration walks the slots in order. So a dictionary enumerates in insertion
 * order while nothing is removed, and after a removal as .NET's does: `{ a, b, c }` less `a`, plus
 * `d`, enumerates `d, b, c`. The plain object this replaced listed integer keys ascending and turned
 * every key into a string, and a `Map` alone would append where .NET reuses the slot.
 *
 * A key is found as .NET's default comparer finds it, a choice eqc makes from the key type
 * ({@link KeyEquality}). By IDENTITY, through a `Map` of key to slot: SameValueZero is .NET's equality
 * for a number (NaN included), a string, a char, a bool, a long, an enum's name and a Guid. By VALUE,
 * through `$eq.equals` over the live slots: a record, a struct, a tuple, a decimal and a date. And by
 * the key's OWN equality where its type does not decide: a class, `object`, an interface, a type
 * parameter, whose value may be a record, a decimal or an instance of a class overriding `Equals`.
 */
export class Dictionary<K, V> implements Iterable<Pair<K, V>> {
  /** Entries by slot, a freed slot `undefined` until an insertion takes it back. */
  private readonly slots: ({ key: K; value: V } | undefined)[] = [];
  /** The slots removals freed, the last freed on top: .NET's free list is last in, first out. */
  private readonly freed: number[] = [];
  /** Each slot of a key found by identity: every key, none when keys are found by value, and under
   *  `'own'` every key but one with an equality of its own. */
  private readonly index: Map<K, number> | null;
  /** How a key is found. */
  private readonly byValue: KeyEquality;

  constructor(entries?: Iterable<readonly [K, V]> | null, byValue: KeyEquality = false) {
    this.byValue = byValue;
    this.index = byValue === true ? null : new Map<K, number>();
    if (entries) for (const [key, value] of entries) this.set(key, value);
  }

  /** `Count`. */
  get size(): number {
    return this.slots.length - this.freed.length;
  }

  /** The key's slot, or -1. A null key is refused here, so every member refuses it as .NET's does. */
  private find(key: K): number {
    requireKey(key);
    if (this.indexes(key)) return this.index!.get(key) ?? -1;
    const same = this.byValue === 'own' ? sameKey : equals;
    for (let slot = 0; slot < this.slots.length; slot++) {
      const entry = this.slots[slot];
      if (entry !== undefined && same(entry.key, key)) return slot;
    }
    return -1;
  }

  /** Whether the index holds this key's slot, rather than a walk over the slots finding it. */
  private indexes(key: K): boolean {
    return this.index !== null && !(this.byValue === 'own' && hasOwnEquality(key));
  }

  /** `ContainsKey`. */
  has(key: K): boolean {
    return this.find(key) >= 0;
  }

  /**
   * The value for `key`, or undefined when it is absent. The indexer reads through `$eq.mapGet`,
   * which asks this and throws for an absent key, as .NET does.
   */
  get(key: K): V | undefined {
    const slot = this.find(key);
    return slot < 0 ? undefined : this.slots[slot]!.value;
  }

  /** The indexer's write: a key already there keeps its slot, a new one takes the slot freed last. */
  set(key: K, value: V): this {
    const found = this.find(key);
    if (found >= 0) {
      this.slots[found]!.value = value;
      return this;
    }
    const slot = this.freed.length > 0 ? this.freed.pop()! : this.slots.length;
    this.slots[slot] = { key, value };
    if (this.indexes(key)) this.index!.set(key, slot);
    return this;
  }

  /** `TryAdd`: a key that is not there is added and answers true, one that is answers false and keeps its value. */
  tryAdd(key: K, value: V): boolean {
    if (this.find(key) >= 0) return false;
    this.set(key, value);
    return true;
  }

  /**
   * `ContainsValue`: whether a value is held, compared as .NET's default comparer compares the value
   * type ({@link KeyEquality}, which eqc says of the VALUE type here).
   */
  containsValue(value: V, byValue: KeyEquality = false): boolean {
    return containsValue(this.slots, value, byValue);
  }

  /** `Remove`: frees the key's slot for the next insertion, and answers whether the key was there. */
  delete(key: K): boolean {
    const slot = this.find(key);
    if (slot < 0) return false;
    this.slots[slot] = undefined;
    this.freed.push(slot);
    if (this.indexes(key)) this.index!.delete(key);
    return true;
  }

  /** `Clear`: every slot goes, the freed ones too, so the next insertion takes the first. */
  clear(): void {
    this.slots.length = 0;
    this.freed.length = 0;
    this.index?.clear();
  }

  /** `Keys`, in slot order. */
  keys(): K[] {
    const keys: K[] = [];
    for (const entry of this.slots) if (entry !== undefined) keys.push(entry.key);
    return keys;
  }

  /** `Values`, in slot order. */
  values(): V[] {
    const values: V[] = [];
    for (const entry of this.slots) if (entry !== undefined) values.push(entry.value);
    return values;
  }

  /** The pairs, in slot order. */
  *[Symbol.iterator](): Iterator<Pair<K, V>> {
    for (const entry of this.slots) if (entry !== undefined) yield pair(entry.key, entry.value);
  }

  /** A dictionary equals only itself, as .NET's does: `$eq.equals` asks a value's own `equals`. */
  equals(other: unknown): boolean {
    return this === other;
  }

  /**
   * The JSON object System.Text.Json writes and reads for a dictionary: each key by its wire text,
   * in slot order, which a JSON object keeps for every key but an integer-like one (#437). Each
   * entry is DEFINED, since assigning "__proto__" would reach the prototype's setter.
   */
  toJSON(): Record<string, V> {
    return wireObject(this.slots);
  }
}

/**
 * The JSON object of a dictionary's entries, each keyed by its wire text and DEFINED, since assigning
 * "__proto__" would reach the prototype's setter. A freed slot is skipped.
 */
export function wireObject<K, V>(entries: Iterable<{ key: K; value: V } | undefined>): Record<string, V> {
  const json: Record<string, V> = {};
  for (const entry of entries) {
    if (entry === undefined) continue;
    Object.defineProperty(json, wireKey(entry.key), {
      value: entry.value,
      writable: true,
      enumerable: true,
      configurable: true,
    });
  }
  return json;
}

/**
 * A dictionary refuses a null key in every member, `ContainsKey`, `TryGetValue` and `Remove`
 * included, with .NET's words: a `Map` would have answered a miss, or filed an entry under null.
 */
export function requireKey(key: unknown): void {
  if (key == null) throw new Error("Value cannot be null. (Parameter 'key')");
}

/**
 * Whether live entries hold `value`, compared as {@link KeyEquality} says: by `$eq.equals`, by the
 * value's own equality, or by SameValueZero (NaN is NaN, as a double's Equals holds).
 */
export function containsValue<V>(
  entries: Iterable<{ value: V } | undefined>,
  value: V,
  byValue: KeyEquality,
): boolean {
  const same = byValue === true ? equals : byValue === 'own' ? sameKey : sameValueZero;
  for (const entry of entries) {
    if (entry !== undefined && same(entry.value, value)) return true;
  }
  return false;
}

/**
 * .NET's `EqualityComparer<object>.Default` on the values two keys turned out to be: identity (NaN
 * equal to NaN), a twin's own `equals` (a record, a struct, a decimal, a date, a class overriding
 * `Equals`), and the members of a tuple or an anonymous type, which have no twin to carry one.
 */
export function sameKey(a: unknown, b: unknown): boolean {
  return sameItem(a, b) || (isPlainValue(a) && equals(a, b));
}

/** SameValueZero, a `Map`'s equality: identity, and NaN equal to NaN. */
function sameValueZero(a: unknown, b: unknown): boolean {
  return a === b || (a !== a && b !== b);
}

/** A tuple (an array) or an anonymous type (a plain object): a value compared by its members. */
function isPlainValue(value: unknown): boolean {
  if (Array.isArray(value)) return true;
  if (value === null || typeof value !== 'object') return false;
  const prototype = Object.getPrototypeOf(value);
  return prototype === Object.prototype || prototype === null;
}

/** Whether a key has an equality of its own, which under `'own'` a walk over the slots asks. */
function hasOwnEquality(key: unknown): boolean {
  return (
    key !== null &&
    typeof key === 'object' &&
    (typeof (key as { equals?: unknown }).equals === 'function' || isPlainValue(key))
  );
}

/** A key as .NET's messages write it, by its `ToString`: a bool as True or False. */
export function keyText(key: unknown): string {
  return typeof key === 'boolean' ? (key ? 'True' : 'False') : String(key);
}

/**
 * A key's text on the wire, as System.Text.Json writes a dictionary key: a bool as True or False, and
 * a long, a decimal or a date by its own `toJSON`.
 */
export function wireKey(key: unknown): string {
  if (typeof key === 'boolean') return key ? 'True' : 'False';
  const own = (key as { toJSON?: () => unknown } | null | undefined)?.toJSON;
  return typeof own === 'function' ? String(own.call(key)) : String(key);
}

/**
 * A new dictionary, from `[key, value]` pairs or another dictionary, whose keys are found as
 * `byValue` says ({@link KeyEquality}). A copy enumerates compacted, in the order of what it copies,
 * as .NET's does.
 */
export function dictionary<K, V>(
  entries?: Iterable<readonly [K, V]> | null,
  byValue: KeyEquality = false,
): Dictionary<K, V> {
  return new Dictionary<K, V>(entries, byValue);
}

/** A bag of the DOM escape hatch: what a transpiled dictionary or the runtime's own code hands over. */
export type Bag<V> = Dictionary<string, V> | Readonly<Record<string, V>>;

/**
 * A bag's entries, in either form. `HtmlElement`'s attributes, events and styles are dictionaries
 * when transpiled C# builds them and plain objects when the runtime does, and every reader walks both.
 */
export function bagEntries<V>(bag: Bag<V>): Iterable<readonly [string, V]> {
  return bag instanceof Dictionary ? bag : Object.entries(bag);
}

/** How many entries a bag holds, in either form. */
export function bagSize(bag: Bag<unknown>): number {
  return bag instanceof Dictionary ? bag.size : Object.keys(bag).length;
}

/**
 * A bag as a plain object, for a reader that walks it and indexes it by name: a dictionary's entries
 * copied into a new object, a plain object handed back as it is, and nothing as a new empty object.
 */
export function plainBag<V>(bag: Bag<V> | null | undefined): Record<string, V> {
  if (bag == null) return {};
  return bag instanceof Dictionary ? Object.fromEntries(bag) : (bag as Record<string, V>);
}
