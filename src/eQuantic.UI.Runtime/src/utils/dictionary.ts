import { adoptMember } from './adopt-member';
import { sameBy, type KeyEquality } from './key-equality';
import { SlotTable } from './slots';

/**
 * How a dictionary finds a key, as .NET's default comparer for the key type does, which eqc says
 * (`utils/key-equality.ts`): by identity, by value, by the key's own equality, or by a comparison
 * eqc generated for a tuple. A set's elements are found by the same rule.
 */
export type { KeyEquality } from './key-equality';

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
 * The slots and the finding are the {@link SlotTable} a `HashSet` keeps its elements in too.
 */
export class Dictionary<K, V> implements Iterable<Pair<K, V>> {
  /** The entries, by slot, and how a key is found among them. */
  private readonly table: SlotTable<K, { key: K; value: V }>;
  /** Bumped when a NEW key goes in, the one change .NET's enumerator refuses: an overwrite, a
   *  removal and `Clear` leave a walk over the pairs running (measured). */
  private version = 0;

  constructor(entries?: Iterable<readonly [K, V]> | null, byValue: KeyEquality = false) {
    this.table = new SlotTable(byValue);
    // A constructor adds what it copies and what a collection initializer lists, as .NET's `Add`
    // does: a key already there is refused, where the indexer's write would replace it (#440).
    if (entries) for (const [key, value] of entries) this.add(key, value);
  }

  /**
   * `Add`: a new key, or .NET's refusal of one already there. The indexer's write replaces instead. It
   * answers the dictionary, so a collection initializer chains one call per entry and an entry that
   * throws stops the ones after it.
   */
  add(key: K, value: V): this {
    if (this.find(key) >= 0) throw new Error(`An item with the same key has already been added. Key: ${keyText(key)}`);
    return this.set(key, value);
  }

  /** `Count`. */
  get size(): number {
    return this.table.size;
  }

  /** The key's slot, or -1. A null key is refused here, so every member refuses it as .NET's does. */
  private find(key: K): number {
    requireKey(key);
    return this.table.find(key);
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
    return slot < 0 ? undefined : this.table.entries[slot]!.value;
  }

  /** The indexer's write: a key already there keeps its slot, a new one takes the slot freed last. */
  set(key: K, value: V): this {
    const found = this.find(key);
    if (found >= 0) {
      this.table.entries[found]!.value = value;
      return this;
    }
    this.table.insert({ key, value });
    this.version++;
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
    return containsValue(this.table.entries, value, byValue);
  }

  /** `Remove`: frees the key's slot for the next insertion, and answers whether the key was there. */
  delete(key: K): boolean {
    const slot = this.find(key);
    if (slot < 0) return false;
    this.table.release(slot);
    return true;
  }

  /** `Clear`: every slot goes, the freed ones too, so the next insertion takes the first. */
  clear(): void {
    this.table.clear();
  }

  /** `Keys`, in slot order. */
  keys(): K[] {
    const keys: K[] = [];
    for (const entry of this.table.entries) if (entry !== undefined) keys.push(entry.key);
    return keys;
  }

  /** `Values`, in slot order. */
  values(): V[] {
    const values: V[] = [];
    for (const entry of this.table.entries) if (entry !== undefined) values.push(entry.value);
    return values;
  }

  /**
   * The pairs, in slot order. A key added while they are walked ends the walk as .NET's enumerator
   * ends it, with its InvalidOperationException: walking the live slots visited each new key in
   * turn, so a loop that adds as it goes never ended.
   */
  *[Symbol.iterator](): Iterator<Pair<K, V>> {
    const version = this.version;
    for (const entry of this.table.entries) {
      if (entry === undefined) continue;
      yield pair(entry.key, entry.value);
      if (this.version !== version) throw collectionModified();
    }
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
    return wireObject(this.table.entries);
  }
}

/**
 * The JSON object of a dictionary's entries, each keyed by its wire text and DEFINED, since assigning
 * "__proto__" would reach the prototype's setter. A freed slot is skipped.
 */
export function wireObject<K, V>(entries: Iterable<{ key: K; value: V } | undefined>): Record<string, V> {
  const json: Record<string, V> = {};
  for (const entry of entries) if (entry !== undefined) adoptMember(json, wireKey(entry.key), entry.value);
  return json;
}

/** .NET's InvalidOperationException for a collection changed under a walk over it. */
export function collectionModified(): Error {
  return new Error('Collection was modified; enumeration operation may not execute.');
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
  const same = sameBy(byValue);
  for (const entry of entries) {
    if (entry !== undefined && same(entry.value, value)) return true;
  }
  return false;
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
