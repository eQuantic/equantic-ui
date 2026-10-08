/**
 * .NET-compat `Queue<T>` and `Stack<T>` — FIFO / LIFO collections backed by a JS array, with the
 * .NET API and quirks: `Dequeue`/`Peek`/`Pop` throw on an empty collection, and `Stack.ToArray()`
 * returns items top-first (LIFO order), while `Queue.ToArray()` returns them front-first.
 *
 * The transpiler emits `$eq.collections.queue(...)` / `$eq.collections.stack(...)` for
 * `new Queue<T>(...)` / `new Stack<T>(...)` and maps the instance methods to camelCase.
 */

import { Dictionary as RuntimeDictionary } from './dictionary';
import { equals } from './equals';
import { exception } from './exceptions';
import { HashSet } from './hash-set';
import { sameBy, sameItem, type KeyEquality } from './key-equality';
import { SortedMap } from './sorted';

export { pairComparer, sameItem } from './key-equality';

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
    if (this.items.length === 0) throw exception('System.InvalidOperationException', 'Queue empty.');
    return this.items.shift() as T;
  }

  peek(): T {
    if (this.items.length === 0) throw exception('System.InvalidOperationException', 'Queue empty.');
    return this.items[0];
  }

  /** By `EqualityComparer<T>.Default`, as .NET's does: a decimal, a date or a record by its value. */
  contains(item: T): boolean {
    return this.items.some((held) => equals(held, item));
  }

  clear(): void {
    this.items.length = 0;
  }

  /** Front-to-back order (FIFO), matching .NET. */
  toArray(): T[] {
    return this.items.slice();
  }

  /** Enumerates front to back, as .NET's does, for a `foreach` and LINQ alike. */
  [Symbol.iterator](): Iterator<T> {
    return this.items.slice()[Symbol.iterator]();
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
    if (this.items.length === 0) throw exception('System.InvalidOperationException', 'Stack empty.');
    return this.items.pop() as T;
  }

  peek(): T {
    if (this.items.length === 0) throw exception('System.InvalidOperationException', 'Stack empty.');
    return this.items[this.items.length - 1];
  }

  /** By `EqualityComparer<T>.Default`, as .NET's does: a decimal, a date or a record by its value. */
  contains(item: T): boolean {
    return this.items.some((held) => equals(held, item));
  }

  clear(): void {
    this.items.length = 0;
  }

  /** Top-to-bottom order (LIFO), matching .NET `Stack<T>.ToArray()`. */
  toArray(): T[] {
    return this.items.slice().reverse();
  }

  /** Enumerates from the top, as .NET's does, for a `foreach` and LINQ alike. */
  [Symbol.iterator](): Iterator<T> {
    return this.items.slice().reverse()[Symbol.iterator]();
  }
}

export function queue<T>(initial?: Iterable<T>): Queue<T> {
  return new Queue<T>(initial);
}

export function stack<T>(initial?: Iterable<T>): Stack<T> {
  return new Stack<T>(initial);
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
    if (this.head === null) throw exception('System.InvalidOperationException', 'LinkedList empty.');
    this.unlink(this.head);
  }

  removeLast(): void {
    if (this.tail === null) throw exception('System.InvalidOperationException', 'LinkedList empty.');
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
 * Membership, as `ICollection<T>.Contains` and LINQ's `Contains` answer it, for a collection whose
 * RUNTIME shape the transpiler could not know. A C# API that takes `IReadOnlyCollection<T>` is handed
 * a `HashSet` as readily as a `List`, and those are a set and an array here — one answers `has`, the
 * other `includes`, and asking the wrong one returns `undefined` rather than failing. That is a
 * selection that never highlights and a filter that never matches, with nothing in the console to say
 * so. A set answers by its own equality, as .NET's `ICollection<T>.Contains` does; anything else is
 * walked by the element type's ({@link KeyEquality}, which eqc says), so a NaN, a record and a tuple
 * are found as `EqualityComparer<T>.Default` finds them (#425).
 */
export function contains(collection: unknown, value: unknown, equality: KeyEquality = false): boolean {
  if (collection == null) return false;
  if (typeof collection === 'string') return collection.includes(value as string);
  if (collection instanceof HashSet || collection instanceof Set || collection instanceof Map) {
    return collection.has(value as never);
  }
  if (Array.isArray(collection) && equality === false) return collection.includes(value);
  // Anything else iterable — a list compared by its element type, a generated sequence, a
  // dictionary's keys view, the runtime's sorted set, queue, stack and linked list.
  if (typeof (collection as Iterable<unknown>)[Symbol.iterator] === 'function') {
    const same = sameBy(equality);
    for (const item of collection as Iterable<unknown>) if (same(item, value)) return true;
  }
  return false;
}

/** What a dictionary is here: the runtime's `Dictionary` or sorted map, or a `Map`, keyed by the pair's key. */
interface Dictionary<K, V> {
  has(key: K): boolean;
  get(key: K): V | undefined;
  delete(key: K): boolean;
}

function isDictionary(collection: unknown): collection is Dictionary<unknown, unknown> {
  if (collection instanceof Map) return true;
  const shape = collection as Partial<Dictionary<unknown, unknown>> | null;
  return (
    shape != null &&
    typeof shape.has === 'function' &&
    typeof shape.get === 'function' &&
    typeof shape.delete === 'function' &&
    !(collection instanceof Set) &&
    !(collection instanceof HashSet)
  );
}

/**
 * `ICollection<KeyValuePair<K, V>>.Remove`: the pair leaves only when its key is there with an equal
 * value, and the answer says whether it did. The equality is the pair's, which the compiler picks from
 * its halves' types: a comparison it generated (`pairComparer`, found in review, #421) is handed the
 * stored pair and the one to remove; `true`, a pair whose halves both compare by value, compares the
 * value by `$eq.equals`; and without one, the value is compared as `sameItem` compares it.
 */
function removePair(dictionary: Dictionary<unknown, unknown>, pair: unknown, equality: KeyEquality): boolean {
  if (pair == null || typeof pair !== 'object' || !('key' in pair)) return false;
  const { key, value } = pair as { key: unknown; value: unknown };
  if (!dictionary.has(key)) return false;
  const stored = { key, value: dictionary.get(key) };
  const equal =
    typeof equality === 'function'
      ? equality(stored, pair)
      : equality === true
        ? equals(stored.value, value)
        : sameItem(stored.value, value);
  return equal && dictionary.delete(key);
}

/**
 * `List<T>.Remove`: takes out the FIRST item equal to the value and answers whether there was one
 * (#400). It was lowered to `((_idx = list.indexOf(x)) >= 0 && list.splice(_idx, 1))`, which assigned
 * a name nothing declared, so every call threw `ReferenceError: _idx is not defined` in a module, and
 * would have answered the spliced array where C# answers a bool. `equality` is the element type's,
 * which the compiler says ({@link KeyEquality}), the one `Contains` and `IndexOf` search by (#425).
 *
 * The static type may be `ICollection<T>`, which can hold any collection that implements it when the
 * call runs (found in review, #421), and each removes as it does when called directly, as `contains`
 * asks the value what it is: a set (`HashSet<T>`) through `delete`, the way `set.Remove(x)` lowers; a
 * dictionary (`ICollection<KeyValuePair<K, V>>`) the pair whose key it holds with an equal value, as
 * .NET's does, the runtime's `Dictionary` and a sorted map alike; and a twin
 * with a `remove` of its own (`LinkedList<T>`, `SortedSet<T>`) through it. An
 * array stands for a `List<T>` and for a `T[]` alike, and .NET throws for the second, which this side
 * cannot tell apart.
 */
export function remove<T>(
  list: T[] | Set<T> | Dictionary<unknown, unknown> | { remove(value: T): boolean },
  value: T,
  equality: KeyEquality = false,
): boolean {
  if (list instanceof HashSet || list instanceof Set) return list.delete(value);
  if (isDictionary(list)) return removePair(list, value, equality);
  if (!Array.isArray(list)) return (list as { remove(value: T): boolean }).remove(value);
  const same = sameBy(equality);
  for (let index = 0; index < list.length; index++) {
    if (same(list[index], value)) {
      list.splice(index, 1);
      return true;
    }
  }
  return false;
}

/**
 * `ICollection<T>.Add`, for the collection the interface holds when the call runs, each adding as its
 * own `ICollection<T>.Add` does in .NET (#593): an array (a `List<T>`) appends; a set adds a value it
 * does not hold and ignores one it does, a `HashSet` and a `SortedSet` alike; a linked list adds LAST;
 * a dictionary (`ICollection<KeyValuePair<K, V>>`) adds the pair's key and value and refuses a key
 * already there in its own words, the runtime's `Dictionary` and a sorted map alike; and a twin calls
 * its own `add`. It was an array's `push`, which none of the others has. An array stands for a `T[]`
 * as well as a `List<T>`, so a fixed-size array behind the face grows here where .NET refuses the call
 * (NotSupportedException): the value carries nothing that tells the two apart.
 */
export function add<T>(collection: unknown, item: T): void {
  if (Array.isArray(collection)) collection.push(item);
  else if (collection instanceof LinkedList) collection.addLast(item);
  else if (collection instanceof RuntimeDictionary || collection instanceof SortedMap) {
    const { key, value } = item as { key: unknown; value: unknown };
    collection.add(key, value);
  } else (collection as { add(item: T): unknown }).add(item);
}

/**
 * `ICollection<T>.Clear`, for the collection the interface holds when the call runs: an array (a
 * `List<T>`) is emptied in place, and every other collection, the runtime's and a twin, by its own
 * `clear` (#593). It was an array's `splice`, which a set, a linked list and a dictionary lack.
 */
export function clear(collection: unknown): void {
  if (Array.isArray(collection)) collection.length = 0;
  else (collection as { clear(): void }).clear();
}

/**
 * What may stand behind a list's face: an array or another array-like, or a twin, whose indexer is
 * `item` and `setItem`. Typed by its element, so a twin's TypeScript reads what the face holds, and
 * the element is `any` where the receiver is untyped, as `mapGet`'s value is.
 */
type Indexed<T> = ArrayLike<T> | { item(index: number): T };

/** What a list's face may be written through: an array's subscript, or a twin's `setItem`. */
type WritablyIndexed<T> = { [index: number]: T } | { setItem(index: number, value: T): unknown };

/**
 * An element read through a list's face (`IList<T>`, `IReadOnlyList<T>`), for whichever list the face
 * holds when the read runs (#586): an array, which stands for a `List<T>` and a `T[]`, by its subscript,
 * and a twin of the app's own by its indexer's getter, `item` (#427). The subscript alone read a
 * property named after the index, which no twin has. Anything else indexed, a typed array a hand-written
 * caller handed over, is read by its subscript as before.
 */
export function item<T = any>(list: Indexed<T>, index: number): T {
  if (Array.isArray(list)) return list[index] as T;
  const twin = list as ArrayLike<T> & { item?: (index: number) => T };
  return typeof twin.item === 'function' ? twin.item(index) : twin[index];
}

/**
 * The write beside {@link item}: an array's subscript, or a twin's indexer setter, `setItem`. It answers
 * the value written, as C#'s assignment does, whatever the setter does with its own copy.
 */
export function setItem<T = any>(list: WritablyIndexed<T>, index: number, value: T): T {
  const target = list as { [index: number]: T } & { setItem?: (index: number, value: T) => unknown };
  if (!Array.isArray(list) && typeof target.setItem === 'function') target.setItem(index, value);
  else target[index] = value;
  return value;
}

/**
 * LINQ's Zip: pairs run out with the SHORTER sequence. A `map` over the receiver instead walks the
 * longer one and hands the selector `undefined` for the missing partner, which for numbers is a
 * silent NaN. Both sources are read once here, so a side-effecting source stays a single read.
 */
export function zip<A, B, R>(
  first: readonly A[],
  second: readonly B[],
  selector: (a: A, b: B) => R,
): R[] {
  const length = Math.min(first.length, second.length);
  const result: R[] = new Array(length);
  for (let i = 0; i < length; i++) result[i] = selector(first[i], second[i]);
  return result;
}

/**
 * `HashSet<T>.Add` — which answers whether the value was NEW, and is the whole reason
 * `if (!set.Add(x)) set.Remove(x)` toggles. A JS `Set.add` returns the set itself, always truthy,
 * so that idiom silently became "add, and never remove".
 */
export function setAdd<T>(set: Set<T>, value: T): boolean {
  if (set instanceof HashSet) return set.tryAdd(value);
  if (set.has(value)) return false;
  set.add(value);
  return true;
}

/**
 * How many a collection holds — `length`, `count`, `size`, or a walk. Same reason as {@link contains}:
 * a C# receiver typed as a collection may be an array or a Set here, and each keeps its count under a
 * different name. The runtime's linked list, queue, stack and sorted set, and a twin of the app's own
 * behind an `ICollection<T>` or an `IReadOnlyList<T>`, keep it as `count`, which a twin that cannot be
 * walked answers alone (#586, #593). It is asked before a `size` or a `length`, which are a twin's own
 * members when it has them: a polyline's `Length` is how long it is, and its `Count` how many points
 * it holds. Null counts as none, so a guarded `xs?.Count` needs no guard at all.
 */
export function count(collection: unknown): number {
  if (collection == null) return 0;
  if (Array.isArray(collection) || typeof collection === 'string') return collection.length;
  const sized = collection as { size?: unknown; length?: unknown; count?: unknown };
  if (typeof sized.count === 'number') return sized.count;
  if (typeof sized.size === 'number') return sized.size;
  if (typeof sized.length === 'number') return sized.length;
  let total = 0;
  if (typeof (collection as Iterable<unknown>)[Symbol.iterator] === 'function')
    for (const _ of collection as Iterable<unknown>) total++;
  return total;
}
