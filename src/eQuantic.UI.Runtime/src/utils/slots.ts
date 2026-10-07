/**
 * The storage .NET's `Dictionary<TKey, TValue>` and `HashSet<T>` share, held as theirs is: entries by
 * SLOT, an insertion taking the slot a removal freed last (.NET's free list is last in, first out) or
 * the next one, and enumeration walking the slots in order. So a dictionary and a set enumerate in
 * insertion order while nothing is removed, and after a removal as .NET's do: `{ 3, 1 }` less 3, plus
 * 5, enumerates `5, 1`. A JavaScript `Set` appends instead (#438).
 *
 * A key is found as `EqualityComparer<T>.Default` finds it, which eqc decides from the key type
 * ({@link KeyEquality}): by IDENTITY through a `Map` of key to slot, by a COMPARISON over the slots
 * whose keys hash alike, and, where the type does not decide, by the key's own equality for a key that
 * has one and through the `Map` for any other. ONE table for both collections, so a set and a
 * dictionary of one type find, keep and reuse slots by one rule (#531).
 *
 * The hash is `$eq.hash` (`utils/hash.ts`), which agrees with every equality here: values that compare
 * equal hash equal, so the comparison reads only the keys that may match, as .NET reads one bucket. A
 * walk over every live slot made a set of tuples or of records quadratic to build.
 *
 * It keeps .NET's CAPACITY too, the length of the entries array .NET allocates: a prime of .NET's
 * table, grown to the next when an insertion finds the entries full. Nothing a program sees depends
 * on it but what .NET makes depend on it: whether a copy of a set keeps the set's free slots, and
 * whether `TrimExcess` compacts it.
 */
import { hash } from './hash';
import { hasEquals, hasOwnEquality, sameBy, sameKey, type Equality, type KeyEquality } from './key-equality';

/** An entry: the key it is found by. A dictionary's carries its value beside it. */
export interface Slot<K> {
  key: K;
}

/** .NET's `HashHelpers.Primes`, the sizes a hash table's arrays take. */
const PRIMES = [
  3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
  1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591, 17519,
  21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437, 187751, 225307,
  270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263, 1674319, 2009191,
  2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369,
];

/** .NET's `HashHelpers.MaxPrimeArrayLength`. */
const MAX_PRIME_ARRAY_LENGTH = 0x7fffffc3;

function isPrime(candidate: number): boolean {
  if ((candidate & 1) === 0) return candidate === 2;
  const limit = Math.floor(Math.sqrt(candidate));
  for (let divisor = 3; divisor <= limit; divisor += 2) if (candidate % divisor === 0) return false;
  return true;
}

/** .NET's `HashHelpers.GetPrime`: the size a table asked to hold `min` entries allocates. */
export function getPrime(min: number): number {
  for (const prime of PRIMES) if (prime >= min) return prime;
  for (let candidate = min | 1; candidate < 0x7fffffff; candidate += 2) {
    if (isPrime(candidate) && (candidate - 1) % 101 !== 0) return candidate;
  }
  return min;
}

/** .NET's `HashHelpers.ExpandPrime`: the size a full table of `oldSize` grows to. */
export function expandPrime(oldSize: number): number {
  const newSize = 2 * oldSize;
  if (newSize > MAX_PRIME_ARRAY_LENGTH && MAX_PRIME_ARRAY_LENGTH > oldSize) return MAX_PRIME_ARRAY_LENGTH;
  return getPrime(newSize);
}

export class SlotTable<K, E extends Slot<K>> {
  /** Entries by slot, a freed slot `undefined` until an insertion takes it back. */
  entries: (E | undefined)[] = [];
  /** The slots removals freed, the last freed on top. */
  freed: number[] = [];
  /** How a key is found. */
  readonly equality: KeyEquality;
  /** Each slot of a key found by identity: every key, none when keys are found by a comparison, under
   *  `'own'` every key but one with an equality of its own, and under `'item'` every key but one whose
   *  twin carries an `equals`. */
  private readonly index: Map<K, number> | null;
  /** The slots of the keys found by a comparison, by their hash: the keys a comparison may find. */
  private readonly buckets = new Map<number, number[]>();
  /** The hash each of those slots is filed under, so a release finds its bucket, as .NET's entry
   *  keeps its hash code. */
  private readonly hashes: number[] = [];
  /** The comparison a walk over the slots finds a key by. */
  private readonly same: Equality;
  /** .NET's capacity: the length of the entries array it allocated, 0 before the first. */
  capacity = 0;

  constructor(equality: KeyEquality = false) {
    this.equality = equality;
    this.index = equality === true || typeof equality === 'function' ? null : new Map<K, number>();
    this.same = equality === 'own' ? sameKey : sameBy(equality);
  }

  /** How many entries are live. */
  get size(): number {
    return this.entries.length - this.freed.length;
  }

  /** The key's slot, or -1. */
  find(key: K): number {
    if (this.indexes(key)) return this.index!.get(key) ?? -1;
    const slots = this.buckets.get(hash(key));
    if (slots === undefined) return -1;
    for (const slot of slots) {
      const entry = this.entries[slot];
      if (entry !== undefined && this.same(entry.key, key)) return slot;
    }
    return -1;
  }

  /** Whether the index holds this key's slot, rather than a walk over the slots finding it. */
  private indexes(key: K): boolean {
    if (this.index === null) return false;
    if (this.equality === 'own') return !hasOwnEquality(key);
    return this.equality !== 'item' || !hasEquals(key);
  }

  /**
   * Stores an entry whose key is not there, in the slot freed last or the next one, and answers the
   * slot. The capacity grows as .NET grows its arrays: to the first prime on the first insertion, and
   * to `ExpandPrime(count)` when the entries are full.
   */
  insert(entry: E): number {
    let slot: number;
    if (this.freed.length > 0) {
      slot = this.freed.pop()!;
    } else {
      slot = this.entries.length;
      if (this.capacity === 0) this.capacity = getPrime(0);
      if (slot === this.capacity) this.capacity = expandPrime(slot);
    }
    this.entries[slot] = entry;
    this.file(entry.key, slot);
    return slot;
  }

  /** Files a live slot where its key is found: the index for a key found by identity, its hash's
   *  bucket for one found by a comparison. */
  private file(key: K, slot: number): void {
    if (this.indexes(key)) {
      this.index!.set(key, slot);
      return;
    }
    const code = hash(key);
    this.hashes[slot] = code;
    const slots = this.buckets.get(code);
    if (slots === undefined) this.buckets.set(code, [slot]);
    else slots.push(slot);
  }

  /** Frees a live slot for the next insertion. */
  release(slot: number): void {
    const entry = this.entries[slot]!;
    this.entries[slot] = undefined;
    this.freed.push(slot);
    if (this.indexes(entry.key)) {
      this.index!.delete(entry.key);
      return;
    }
    const code = this.hashes[slot];
    const slots = this.buckets.get(code)!;
    slots.splice(slots.indexOf(slot), 1);
    if (slots.length === 0) this.buckets.delete(code);
  }

  /** Every slot goes, the freed ones too, so the next insertion takes the first. The capacity stays,
   *  as .NET's `Clear` keeps its arrays. */
  clear(): void {
    this.entries.length = 0;
    this.freed.length = 0;
    this.index?.clear();
    this.buckets.clear();
    this.hashes.length = 0;
  }

  /** .NET's `Initialize`: empty, with the arrays a table of `capacity` entries allocates. Answers the size. */
  initialize(capacity: number): number {
    this.clear();
    this.capacity = getPrime(capacity);
    return this.capacity;
  }

  /** The live entries packed into the first slots, in their order, with no slot free: .NET's
   *  `TrimExcess` and a copy that does not keep the slots. */
  compact(capacity: number): void {
    const live = this.entries.filter((entry): entry is E => entry !== undefined);
    this.initialize(capacity);
    for (const entry of live) this.insert(entry);
  }

  /** Another table's slots, free ones and capacity included, as .NET clones a set's arrays. */
  copyFrom(source: SlotTable<K, E>, copy: (entry: E) => E): void {
    this.clear();
    this.entries = source.entries.map((entry) => (entry === undefined ? undefined : copy(entry)));
    this.freed = source.freed.slice();
    this.capacity = source.capacity;
    this.entries.forEach((entry, slot) => {
      if (entry !== undefined) this.file(entry.key, slot);
    });
  }
}
