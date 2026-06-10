/**
 * .NET-compat `Queue<T>` and `Stack<T>` — FIFO / LIFO collections backed by a JS array, with the
 * .NET API and quirks: `Dequeue`/`Peek`/`Pop` throw on an empty collection, and `Stack.ToArray()`
 * returns items top-first (LIFO order), while `Queue.ToArray()` returns them front-first.
 *
 * The transpiler emits `$eq.collections.queue(...)` / `$eq.collections.stack(...)` for
 * `new Queue<T>(...)` / `new Stack<T>(...)` and maps the instance methods to camelCase.
 */

import { equals } from './equals';

export class Queue<T> {
  private readonly items: T[];

  constructor(initial?: Iterable<T>) {
    this.items = initial ? Array.from(initial) : [];
  }

  get count(): number {
    return this.items.length;
  }

  enqueue(item: T): void {
    this.items.push(item);
  }

  dequeue(): T {
    if (this.items.length === 0) throw new Error('Queue empty.');
    return this.items.shift() as T;
  }

  peek(): T {
    if (this.items.length === 0) throw new Error('Queue empty.');
    return this.items[0];
  }

  contains(item: T): boolean {
    return this.items.includes(item);
  }

  clear(): void {
    this.items.length = 0;
  }

  /** Front-to-back order (FIFO), matching .NET. */
  toArray(): T[] {
    return this.items.slice();
  }
}

export class Stack<T> {
  private readonly items: T[];

  constructor(initial?: Iterable<T>) {
    this.items = initial ? Array.from(initial) : [];
  }

  get count(): number {
    return this.items.length;
  }

  push(item: T): void {
    this.items.push(item);
  }

  pop(): T {
    if (this.items.length === 0) throw new Error('Stack empty.');
    return this.items.pop() as T;
  }

  peek(): T {
    if (this.items.length === 0) throw new Error('Stack empty.');
    return this.items[this.items.length - 1];
  }

  contains(item: T): boolean {
    return this.items.includes(item);
  }

  clear(): void {
    this.items.length = 0;
  }

  /** Top-to-bottom order (LIFO), matching .NET `Stack<T>.ToArray()`. */
  toArray(): T[] {
    return this.items.slice().reverse();
  }
}

export function queue<T>(initial?: Iterable<T>): Queue<T> {
  return new Queue<T>(initial);
}

export function stack<T>(initial?: Iterable<T>): Stack<T> {
  return new Stack<T>(initial);
}

/**
 * .NET-compat `Dictionary<TKey, TValue>` whose KEY compares by VALUE (structural) equality — for keys
 * that are records, `struct`s or value tuples. A plain JS object can't key on those: it coerces the
 * key to a string via `toString`, so two structurally-equal-but-distinct keys collide (or, worse,
 * every record key collapses to `"[object Object]"`). The transpiler detects a value-typed key and
 * emits `$eq.collections.valueMap(...)`, routing the dictionary's operations (indexer, `ContainsKey`,
 * `Add`, `Remove`, `Keys`, `Values`, `Count`, `foreach`) here. String/number/enum keys keep the plain
 * object form — this class is only for the value-typed case.
 *
 * Entries are held in insertion order (matching .NET's enumeration order for a dictionary without
 * removals) and located by a linear scan with `$eq.equals`. Linear lookup is O(n), but key counts in
 * UI state are small and structural equality has no faithful O(1) hash without re-deriving the
 * member-by-member compare — correctness and simplicity win here over micro-optimisation.
 */
export class ValueMap<K, V> implements Iterable<{ key: K; value: V }> {
  private readonly entries: { key: K; value: V }[] = [];

  constructor(initial?: Iterable<readonly [K, V]>) {
    if (initial) {
      for (const [k, v] of initial) this.set(k, v);
    }
  }

  /** Number of entries — backs `.Count`. */
  get size(): number {
    return this.entries.length;
  }

  private indexOf(key: K): number {
    for (let i = 0; i < this.entries.length; i++) {
      if (equals(this.entries[i].key, key)) return i;
    }
    return -1;
  }

  /** `ContainsKey(key)`. */
  has(key: K): boolean {
    return this.indexOf(key) >= 0;
  }

  /** Indexer read — `undefined` when absent (matching the non-throwing plain-object form). */
  get(key: K): V | undefined {
    const i = this.indexOf(key);
    return i >= 0 ? this.entries[i].value : undefined;
  }

  /** Indexer assignment and `Add` — overwrites an existing equal key (as the plain-object form does). */
  set(key: K, value: V): this {
    const i = this.indexOf(key);
    if (i >= 0) this.entries[i].value = value;
    else this.entries.push({ key, value });
    return this;
  }

  /** `Remove(key)` — true when a matching key was present. */
  delete(key: K): boolean {
    const i = this.indexOf(key);
    if (i < 0) return false;
    this.entries.splice(i, 1);
    return true;
  }

  /** `Clear()`. */
  clear(): void {
    this.entries.length = 0;
  }

  /** `Keys` — the key objects, in insertion order. */
  keys(): K[] {
    return this.entries.map((e) => e.key);
  }

  /** `Values` — the values, in insertion order. */
  values(): V[] {
    return this.entries.map((e) => e.value);
  }

  /** `KeyValuePair`-shaped entries (`{ key, value }`) for `foreach (var kvp in dict)`. */
  [Symbol.iterator](): Iterator<{ key: K; value: V }> {
    return this.entries.map((e) => ({ key: e.key, value: e.value }))[Symbol.iterator]();
  }
}

export function valueMap<K, V>(initial?: Iterable<readonly [K, V]>): ValueMap<K, V> {
  return new ValueMap<K, V>(initial);
}
