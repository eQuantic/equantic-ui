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

/**
 * A node of {@link LinkedList} — mirrors .NET `LinkedListNode<T>`: a value plus links to the adjacent
 * nodes (`next`/`previous` are `null` at the ends). The transpiler maps `.Value`/`.Next`/`.Previous`
 * to these camelCase members, so `list.First.Value` and node traversal work as in .NET.
 */
export class LinkedListNode<T> {
  value: T;
  next: LinkedListNode<T> | null = null;
  previous: LinkedListNode<T> | null = null;

  constructor(value: T) {
    this.value = value;
  }
}

/**
 * .NET-compat `LinkedList<T>` — a doubly-linked list with the .NET API: `AddFirst`/`AddLast` (return the
 * new node), `RemoveFirst`/`RemoveLast`, `Remove(value)`, `Contains`, `Clear`, `Count`, and the `First`/
 * `Last` node accessors. Real nodes (not an array) so `First.Next` traversal is faithful. Value lookup
 * (`Contains`/`Remove`) uses `$eq.equals`, matching `EqualityComparer<T>.Default` for primitives and
 * value types. The transpiler emits `$eq.collections.linkedList(...)` and maps members to camelCase.
 */
export class LinkedList<T> implements Iterable<T> {
  private head: LinkedListNode<T> | null = null;
  private tail: LinkedListNode<T> | null = null;
  private _count = 0;

  constructor(initial?: Iterable<T>) {
    if (initial) {
      for (const v of initial) this.addLast(v);
    }
  }

  get count(): number {
    return this._count;
  }

  /** First node, or `null` when empty (`.NET` `First`). */
  get first(): LinkedListNode<T> | null {
    return this.head;
  }

  /** Last node, or `null` when empty (`.NET` `Last`). */
  get last(): LinkedListNode<T> | null {
    return this.tail;
  }

  addFirst(value: T): LinkedListNode<T> {
    const node = new LinkedListNode(value);
    if (this.head === null) {
      this.head = this.tail = node;
    } else {
      node.next = this.head;
      this.head.previous = node;
      this.head = node;
    }
    this._count++;
    return node;
  }

  addLast(value: T): LinkedListNode<T> {
    const node = new LinkedListNode(value);
    if (this.tail === null) {
      this.head = this.tail = node;
    } else {
      node.previous = this.tail;
      this.tail.next = node;
      this.tail = node;
    }
    this._count++;
    return node;
  }

  removeFirst(): void {
    if (this.head === null) throw new Error('LinkedList empty.');
    this.unlink(this.head);
  }

  removeLast(): void {
    if (this.tail === null) throw new Error('LinkedList empty.');
    this.unlink(this.tail);
  }

  /** Removes the first node whose value equals `value`; true when one was found. */
  remove(value: T): boolean {
    for (let n = this.head; n !== null; n = n.next) {
      if (equals(n.value, value)) {
        this.unlink(n);
        return true;
      }
    }
    return false;
  }

  contains(value: T): boolean {
    for (let n = this.head; n !== null; n = n.next) {
      if (equals(n.value, value)) return true;
    }
    return false;
  }

  clear(): void {
    this.head = this.tail = null;
    this._count = 0;
  }

  private unlink(node: LinkedListNode<T>): void {
    if (node.previous !== null) node.previous.next = node.next;
    else this.head = node.next;
    if (node.next !== null) node.next.previous = node.previous;
    else this.tail = node.previous;
    this._count--;
  }

  *[Symbol.iterator](): Iterator<T> {
    for (let n = this.head; n !== null; n = n.next) yield n.value;
  }

  /** Front-to-back order, matching .NET enumeration. */
  toArray(): T[] {
    return [...this];
  }
}

export function linkedList<T>(initial?: Iterable<T>): LinkedList<T> {
  return new LinkedList<T>(initial);
}

/**
 * Membership, for a collection whose RUNTIME shape the transpiler could not know. A C# API that
 * takes `IReadOnlyCollection<T>` is handed a `HashSet` as readily as a `List`, and those become a
 * `Set` and an array here — one answers `has`, the other `includes`, and asking the wrong one
 * returns `undefined` rather than failing. That is a selection that never highlights and a filter
 * that never matches, with nothing in the console to say so.
 */
export function contains(collection: unknown, value: unknown): boolean {
  if (collection == null) return false;
  if (Array.isArray(collection)) return collection.includes(value);
  if (collection instanceof Set || collection instanceof Map) return collection.has(value as never);
  if (typeof collection === 'string') return collection.includes(value as string);
  // Anything else iterable — a generated sequence, a Map's keys view.
  if (typeof (collection as Iterable<unknown>)[Symbol.iterator] === 'function') {
    for (const item of collection as Iterable<unknown>) if (item === value) return true;
  }
  return false;
}

/**
 * `HashSet<T>.Add` — which answers whether the value was NEW, and is the whole reason
 * `if (!set.Add(x)) set.Remove(x)` toggles. A JS `Set.add` returns the set itself, always truthy,
 * so that idiom silently became "add, and never remove".
 */
export function setAdd<T>(set: Set<T>, value: T): boolean {
  if (set.has(value)) return false;
  set.add(value);
  return true;
}

/**
 * How many a collection holds — `length`, `size`, or a walk. Same reason as {@link contains}: a C#
 * receiver typed as a collection may be an array or a Set here, and each keeps its count under a
 * different name. Null counts as none, so a guarded `xs?.Count` needs no guard at all.
 */
export function count(collection: unknown): number {
  if (collection == null) return 0;
  if (Array.isArray(collection) || typeof collection === 'string') return collection.length;
  const sized = collection as { size?: unknown; length?: unknown };
  if (typeof sized.size === 'number') return sized.size;
  if (typeof sized.length === 'number') return sized.length;
  let total = 0;
  if (typeof (collection as Iterable<unknown>)[Symbol.iterator] === 'function')
    for (const _ of collection as Iterable<unknown>) total++;
  return total;
}

/**
 * A transpiled C# `Dictionary` is a plain object — not iterable. This is what a `foreach` over
 * one (and a `new List<KeyValuePair<,>>(dict)`) compiles to: each pair DESTRUCTURES as
 * `[key, value]` and also answers `.key`/`.value` (both C# consumption shapes), and numeric keys
 * come back as numbers — Object.entries strings them, and a stringified key turns the next
 * `key + 1` into concatenation.
 */
export function entries<V>(
  dict: Record<string, V>,
  numericKeys: true,
): Array<[number, V] & { key: number; value: V }>;
export function entries<V>(
  dict: Record<string, V>,
  numericKeys?: false,
): Array<[string, V] & { key: string; value: V }>;
export function entries<V>(
  dict: Record<string, V>,
  numericKeys = false,
): Array<[string | number, V] & { key: string | number; value: V }> {
  return Object.entries(dict).map(([rawKey, value]) => {
    const key = numericKeys ? Number(rawKey) : rawKey;
    return Object.assign([key, value] as [string | number, V], { key, value });
  });
}
