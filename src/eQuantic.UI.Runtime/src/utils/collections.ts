/**
 * .NET-compat `Queue<T>` and `Stack<T>` — FIFO / LIFO collections backed by a JS array, with the
 * .NET API and quirks: `Dequeue`/`Peek`/`Pop` throw on an empty collection, and `Stack.ToArray()`
 * returns items top-first (LIFO order), while `Queue.ToArray()` returns them front-first.
 *
 * The transpiler emits `$eq.collections.queue(...)` / `$eq.collections.stack(...)` for
 * `new Queue<T>(...)` / `new Stack<T>(...)` and maps the instance methods to camelCase. The factories
 * are the constructors' twins, with their refusals (#726); the classes' own constructors are what the
 * hydration builds with, from an array it already holds.
 */

import {
  collectionModified,
  collectionModifiedAfterInstantiation,
  Dictionary as RuntimeDictionary,
} from './dictionary';
import { equals } from './equals';
import { exception } from './exceptions';
import { HashSet } from './hash-set';
import { sameBy, sameItem, type KeyEquality } from './key-equality';
import { collectionCount } from './slots';
import { SortedMap } from './sorted';

export { pairComparer, sameItem } from './key-equality';

/** `ArgumentNullException`'s words for a parameter. */
function argumentNull(parameter: string): Error {
  return exception('System.ArgumentNullException', `Value cannot be null. (Parameter '${parameter}')`);
}

/** `ArgumentOutOfRangeException.ThrowIfNegative`'s words. */
function negative(parameter: string, value: number): Error {
  return exception(
    'System.ArgumentOutOfRangeException',
    `${parameter} ('${value}') must be a non-negative value. (Parameter '${parameter}')\nActual value was ${value}.`,
  );
}

const INDEX_LESS_OR_EQUAL =
  'Index was out of range. Must be non-negative and less than or equal to the size of the collection.';
const INVALID_OFF_LEN =
  'Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.';

/**
 * `Queue<T>.CopyTo` and `Stack<T>.CopyTo`: `items`, in the order the collection enumerates, written into
 * `array` from `arrayIndex`, after .NET's checks in .NET's order. A queue names no actual value when it
 * refuses the index, and a stack names it, as each does on .NET 10 (measured).
 */
function copyInto<T>(items: readonly T[], array: T[] | null, arrayIndex: number, namesTheValue: boolean): void {
  if (array == null) throw argumentNull('array');
  if (arrayIndex < 0 || arrayIndex > array.length) {
    const actual = namesTheValue ? `\nActual value was ${arrayIndex}.` : '';
    throw exception(
      'System.ArgumentOutOfRangeException',
      `${INDEX_LESS_OR_EQUAL} (Parameter 'arrayIndex')${actual}`,
    );
  }
  if (array.length - arrayIndex < items.length) throw exception('System.ArgumentException', INVALID_OFF_LEN);
  items.forEach((item, at) => (array[arrayIndex + at] = item));
}

/** A sequence as C# enumerates it: a string by its chars (UTF-16 code units), anything else as it is. */
function elements<T>(source: Iterable<T> | string): Iterable<T> {
  return typeof source === 'string' ? (source.split('') as unknown as T[]) : source;
}

/**
 * What a constructor's argument hands it: nothing for no argument, a capacity, which a negative value
 * is refused as, or a collection, a null one refused by its parameter's name, as `Queue<T>`,
 * `Stack<T>` and `LinkedList<T>` refuse it (#726), where the factories read a null as no collection.
 */
function constructorArgument<T>(
  argument: [] | [Iterable<T> | string | number | null | undefined],
  takesACapacity: boolean,
): { items: T[]; capacity: number } {
  if (argument.length === 0) return { items: [], capacity: 0 };
  const [source] = argument;
  if (typeof source === 'number' && takesACapacity) {
    if (source < 0) throw negative('capacity', source);
    return { items: [], capacity: source };
  }
  if (source == null) throw argumentNull('collection');
  const items = Array.from(elements(source as Iterable<T> | string));
  return { items, capacity: sequenceCapacity(source, items.length) };
}

/** `Array.MaxLength`, past which no capacity grows. */
const MAX_LENGTH = 0x7fffffc7;

/**
 * The capacity .NET's `EnumerableHelpers.ToArray` gives a queue or a stack built from a sequence: the
 * count of an `ICollection<T>`, and for anything else, a queue, a stack and a string among them, the
 * array it doubles from four as it reads. A sequence with nothing in it has none (#866).
 */
function sequenceCapacity(source: unknown, length: number): number {
  if (length === 0) return 0;
  if (collectionCount(source) !== null) return length;
  let capacity = 4;
  while (capacity < length) capacity = Math.min(capacity * 2, MAX_LENGTH);
  return capacity;
}

/** `ArgumentOutOfRangeException.ThrowIfLessThan(capacity, count)`'s words. */
function lessThanCount(capacity: number, count: number): Error {
  return exception(
    'System.ArgumentOutOfRangeException',
    `capacity ('${capacity}') must be greater than or equal to '${count}'. (Parameter 'capacity')\nActual value was ${capacity}.`,
  );
}

/** `TrimExcess()`'s threshold: a capacity is trimmed while what it holds is under nine tenths of it. */
function trimThreshold(capacity: number): number {
  return Math.trunc(capacity * 0.9);
}

export class Queue<T> {
  private readonly items: T[];
  /** Bumped by every change, as .NET's `_version` is: a walk that sees it move refuses to go on. */
  private version = 0;
  /** The length of the array .NET would hold the elements in, which decides `Capacity`, what
   *  `EnsureCapacity` answers and when `TrimExcess` reallocates (#866). */
  private _capacity: number;

  constructor(initial?: Iterable<T>, capacity?: number) {
    this.items = initial ? Array.from(initial) : [];
    this._capacity = capacity ?? sequenceCapacity(initial, this.items.length);
  }

  get count(): number {
    return this.items.length;
  }

  get capacity(): number {
    return this._capacity;
  }

  enqueue(item: T): void {
    if (this.items.length === this._capacity) this.grow(this.items.length + 1);
    this.items.push(item);
    this.version++;
  }

  dequeue(): T {
    if (this.items.length === 0) throw exception('System.InvalidOperationException', 'Queue empty.');
    this.version++;
    return this.items.shift() as T;
  }

  /** `TryDequeue`: the element at the front, taken, or undefined where .NET answers false. */
  tryDequeue(): T | undefined {
    return this.items.length === 0 ? undefined : this.dequeue();
  }

  peek(): T {
    if (this.items.length === 0) throw exception('System.InvalidOperationException', 'Queue empty.');
    return this.items[0];
  }

  /** `TryPeek`: the element at the front, or undefined where .NET answers false. */
  tryPeek(): T | undefined {
    return this.items.length === 0 ? undefined : this.items[0];
  }

  /** By `EqualityComparer<T>.Default`, as .NET's does: a decimal, a date or a record by its value. */
  contains(item: T): boolean {
    return this.items.some((held) => equals(held, item));
  }

  clear(): void {
    this.items.length = 0;
    this.version++;
  }

  /** `EnsureCapacity`: grown, by the rule an `Enqueue` grows by, to hold `capacity` at least. */
  ensureCapacity(capacity: number): number {
    if (capacity < 0) throw negative('capacity', capacity);
    if (this._capacity < capacity) this.grow(capacity);
    return this._capacity;
  }

  /** `TrimExcess()`, under nine tenths of the capacity, and `TrimExcess(capacity)`, to that capacity;
   *  each reallocation a change, as .NET's `SetCapacity` is. */
  trimExcess(...capacity: [] | [number]): void {
    if (capacity.length === 0) {
      if (this.items.length < trimThreshold(this._capacity)) this.setCapacity(this.items.length);
      return;
    }
    const [asked] = capacity;
    if (asked < 0) throw negative('capacity', asked);
    if (asked < this.items.length) throw lessThanCount(asked, this.items.length);
    if (asked !== this._capacity) this.setCapacity(asked);
  }

  /** .NET's `Queue<T>.Grow`: twice the array, four more at least, and `capacity` at least. */
  private grow(capacity: number): void {
    let next = Math.min(2 * this._capacity, MAX_LENGTH);
    next = Math.max(next, this._capacity + 4);
    this.setCapacity(Math.max(next, capacity));
  }

  private setCapacity(capacity: number): void {
    this._capacity = capacity;
    this.version++;
  }

  /** Front-to-back order (FIFO), matching .NET. */
  toArray(): T[] {
    return this.items.slice();
  }

  /** `CopyTo(array, arrayIndex)`: front to back, from `arrayIndex`. */
  copyTo(array: T[] | null, arrayIndex: number): void {
    copyInto(this.items, array, arrayIndex, false);
  }

  /** Enumerates front to back, as .NET's does, for a `foreach` and LINQ alike. A change while it is
   *  walked ends the walk at its next step, the one after the last element included, with .NET's
   *  InvalidOperationException: it walked a copy, and walked on (#765). */
  *[Symbol.iterator](): Generator<T, undefined, unknown> {
    const version = this.version;
    for (let at = 0; at < this.items.length; at++) {
      yield this.items[at];
      if (this.version !== version) throw collectionModified();
    }
    return undefined;
  }

  /**
   * The array System.Text.Json writes for a queue, front to back: with no `toJSON` a Server Action
   * argument was written as the class's fields, `{"items":[...]}`, which the server reads as no queue
   * (#597).
   */
  toJSON(): T[] {
    return this.toArray();
  }
}

export class Stack<T> {
  private readonly items: T[];
  /** Bumped by every change, as .NET's `_version` is: a walk that sees it move refuses to go on. */
  private version = 0;
  /** The length of the array .NET would hold the elements in (#866). A stack's capacity changes
   *  without a change of version, as .NET's `Array.Resize` of it does. */
  private _capacity: number;

  constructor(initial?: Iterable<T>, capacity?: number) {
    this.items = initial ? Array.from(initial) : [];
    this._capacity = capacity ?? sequenceCapacity(initial, this.items.length);
  }

  get count(): number {
    return this.items.length;
  }

  get capacity(): number {
    return this._capacity;
  }

  push(item: T): void {
    if (this.items.length === this._capacity) this.grow(this.items.length + 1);
    this.items.push(item);
    this.version++;
  }

  pop(): T {
    if (this.items.length === 0) throw exception('System.InvalidOperationException', 'Stack empty.');
    this.version++;
    return this.items.pop() as T;
  }

  /** `TryPop`: the element on top, taken, or undefined where .NET answers false. */
  tryPop(): T | undefined {
    return this.items.length === 0 ? undefined : this.pop();
  }

  peek(): T {
    if (this.items.length === 0) throw exception('System.InvalidOperationException', 'Stack empty.');
    return this.items[this.items.length - 1];
  }

  /** `TryPeek`: the element on top, or undefined where .NET answers false. */
  tryPeek(): T | undefined {
    return this.items.length === 0 ? undefined : this.items[this.items.length - 1];
  }

  /** By `EqualityComparer<T>.Default`, as .NET's does: a decimal, a date or a record by its value. */
  contains(item: T): boolean {
    return this.items.some((held) => equals(held, item));
  }

  clear(): void {
    this.items.length = 0;
    this.version++;
  }

  /** `EnsureCapacity`: grown, by the rule a `Push` grows by, to hold `capacity` at least. */
  ensureCapacity(capacity: number): number {
    if (capacity < 0) throw negative('capacity', capacity);
    if (this._capacity < capacity) this.grow(capacity);
    return this._capacity;
  }

  /** `TrimExcess()`, under nine tenths of the capacity, and `TrimExcess(capacity)`, to that capacity. */
  trimExcess(...capacity: [] | [number]): void {
    if (capacity.length === 0) {
      if (this.items.length < trimThreshold(this._capacity)) this._capacity = this.items.length;
      return;
    }
    const [asked] = capacity;
    if (asked < 0) throw negative('capacity', asked);
    if (asked < this.items.length) throw lessThanCount(asked, this.items.length);
    this._capacity = asked;
  }

  /** .NET's `Stack<T>.Grow`: four from empty, twice the array otherwise, and `capacity` at least. */
  private grow(capacity: number): void {
    const next = this._capacity === 0 ? 4 : Math.min(2 * this._capacity, MAX_LENGTH);
    this._capacity = Math.max(next, capacity);
  }

  /** Top-to-bottom order (LIFO), matching .NET `Stack<T>.ToArray()`. */
  toArray(): T[] {
    return this.items.slice().reverse();
  }

  /** `CopyTo(array, arrayIndex)`: from the top, from `arrayIndex`, as the stack enumerates. */
  copyTo(array: T[] | null, arrayIndex: number): void {
    copyInto(this.toArray(), array, arrayIndex, true);
  }

  /** Enumerates from the top, as .NET's does, for a `foreach` and LINQ alike. A change while it is
   *  walked ends the walk as a queue's ends, in System.Collections' words (#765). */
  *[Symbol.iterator](): Generator<T, undefined, unknown> {
    const version = this.version;
    for (let at = this.items.length - 1; at >= 0; at--) {
      yield this.items[at];
      if (this.version !== version) throw collectionModifiedAfterInstantiation();
    }
    return undefined;
  }

  /** The array System.Text.Json writes for a stack: its enumeration, from the top (#597). */
  toJSON(): T[] {
    return this.toArray();
  }
}

/** `new Queue<T>()`, `new Queue<T>(capacity)` and `new Queue<T>(collection)`. */
export function queue<T>(...argument: [] | [Iterable<T> | string | number | null]): Queue<T> {
  const { items, capacity } = constructorArgument(argument, true);
  return new Queue<T>(items, capacity);
}

/** `new Stack<T>()`, `new Stack<T>(capacity)` and `new Stack<T>(collection)`. */
export function stack<T>(...argument: [] | [Iterable<T> | string | number | null]): Stack<T> {
  const { items, capacity } = constructorArgument(argument, true);
  return new Stack<T>(items, capacity);
}

/** `InvalidOperationException`'s words for a node of another list, or of none. */
function foreignNode(): Error {
  return exception('System.InvalidOperationException', 'The LinkedList node does not belong to current LinkedList.');
}

/** `InvalidOperationException`'s words for a new node that is already in a list. */
function attachedNode(): Error {
  return exception('System.InvalidOperationException', 'The LinkedList node already belongs to a LinkedList.');
}

/**
 * A node of {@link LinkedList} — mirrors .NET `LinkedListNode<T>`: a value, the list it belongs to and
 * the nodes beside it, `null` at the ends. A node built on its own, or removed from its list, belongs
 * to none and links to nothing, as .NET's `Invalidate` leaves it (#866). Only the list links it.
 */
export class LinkedListNode<T> {
  value: T;
  /** @internal The list's to write. */
  _list: LinkedList<T> | null = null;
  /** @internal */
  _next: LinkedListNode<T> | null = null;
  /** @internal */
  _previous: LinkedListNode<T> | null = null;

  constructor(value: T) {
    this.value = value;
  }

  get list(): LinkedList<T> | null {
    return this._list;
  }

  get next(): LinkedListNode<T> | null {
    return this._next;
  }

  get previous(): LinkedListNode<T> | null {
    return this._previous;
  }

  /** @internal What .NET's `Invalidate` does to a node its list lets go of. */
  _invalidate(): void {
    this._list = this._next = this._previous = null;
  }
}

/**
 * .NET-compat `LinkedList<T>` — a doubly-linked list with the .NET API: every `Add` of a value or of a
 * node of its own, `Remove` of a value or of a node, `RemoveFirst`/`RemoveLast`, `Find`/`FindLast`,
 * `Contains`, `CopyTo`, `Clear`, `Count`, and the `First`/`Last` node accessors. Real nodes (not an
 * array) so `First.Next` traversal is faithful. Value lookup (`Contains`/`Remove`/`Find`) uses
 * `$eq.equals`, matching `EqualityComparer<T>.Default` for primitives and value types. The transpiler
 * emits `$eq.collections.linkedList(...)` and maps each member to its twin here; an overload that takes
 * a node is the twin named for the node (`addNodeAfter`, `removeNode`).
 */
export class LinkedList<T> implements Iterable<T> {
  private head: LinkedListNode<T> | null = null;
  private tail: LinkedListNode<T> | null = null;
  private _count = 0;
  /** Bumped by every node added or removed and by `Clear`, as .NET's `version` is; a value written into
   *  a node is no change. */
  private version = 0;

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
    this.insertBefore(this.head, node);
    return node;
  }

  addLast(value: T): LinkedListNode<T> {
    const node = new LinkedListNode(value);
    this.insertBefore(null, node);
    return node;
  }

  /** `AddFirst(node)`: a node of no list, put at the front. */
  addNodeFirst(node: LinkedListNode<T> | null): void {
    this.insertBefore(this.head, this.unattached(node));
  }

  /** `AddLast(node)`: a node of no list, put at the back. */
  addNodeLast(node: LinkedListNode<T> | null): void {
    this.insertBefore(null, this.unattached(node));
  }

  /** `AddAfter(node, value)`: a new node after a node of this list, answered. */
  addAfter(node: LinkedListNode<T> | null, value: T): LinkedListNode<T> {
    const after = this.own(node);
    const added = new LinkedListNode(value);
    this.insertBefore(after._next, added);
    return added;
  }

  /** `AddAfter(node, newNode)`: a node of no list after a node of this list. */
  addNodeAfter(node: LinkedListNode<T> | null, newNode: LinkedListNode<T> | null): void {
    const after = this.own(node);
    this.insertBefore(after._next, this.unattached(newNode));
  }

  /** `AddBefore(node, value)`: a new node before a node of this list, answered. */
  addBefore(node: LinkedListNode<T> | null, value: T): LinkedListNode<T> {
    const before = this.own(node);
    const added = new LinkedListNode(value);
    this.insertBefore(before, added);
    return added;
  }

  /** `AddBefore(node, newNode)`: a node of no list before a node of this list. */
  addNodeBefore(node: LinkedListNode<T> | null, newNode: LinkedListNode<T> | null): void {
    const before = this.own(node);
    this.insertBefore(before, this.unattached(newNode));
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
    const node = this.find(value);
    if (node === null) return false;
    this.unlink(node);
    return true;
  }

  /** `Remove(node)`: a node of this list, removed. It looked for the node among the values, and
   *  removed nothing (#866). */
  removeNode(node: LinkedListNode<T> | null): void {
    this.unlink(this.own(node));
  }

  /** `Find`: the first node whose value equals `value`, or null. */
  find(value: T): LinkedListNode<T> | null {
    for (let n = this.head; n !== null; n = n._next) {
      if (equals(n.value, value)) return n;
    }
    return null;
  }

  /** `FindLast`: the last node whose value equals `value`, or null. */
  findLast(value: T): LinkedListNode<T> | null {
    for (let n = this.tail; n !== null; n = n._previous) {
      if (equals(n.value, value)) return n;
    }
    return null;
  }

  contains(value: T): boolean {
    return this.find(value) !== null;
  }

  /** `Clear`: every node let go of, as .NET invalidates each. */
  clear(): void {
    for (let n = this.head; n !== null; ) {
      const next = n._next;
      n._invalidate();
      n = next;
    }
    this.head = this.tail = null;
    this._count = 0;
    this.version++;
  }

  /** `CopyTo(array, index)`: front to back, after .NET's checks in .NET's order. */
  copyTo(array: T[] | null, index: number): void {
    if (array == null) throw argumentNull('array');
    if (index < 0) throw negative('index', index);
    if (index > array.length) {
      throw exception(
        'System.ArgumentOutOfRangeException',
        `Must be less than or equal to the size of the collection. (Parameter 'index')\nActual value was ${index}.`,
      );
    }
    if (array.length - index < this._count) {
      throw exception('System.ArgumentException', 'Insufficient space in the target location to copy the information.');
    }
    let at = index;
    for (let n = this.head; n !== null; n = n._next) array[at++] = n.value;
  }

  /** .NET's `ValidateNode`: a node of this list. */
  private own(node: LinkedListNode<T> | null): LinkedListNode<T> {
    if (node == null) throw argumentNull('node');
    if (node._list !== this) throw foreignNode();
    return node;
  }

  /** .NET's `ValidateNewNode`: a node of no list, refused by the name `node` whichever parameter it is. */
  private unattached(node: LinkedListNode<T> | null): LinkedListNode<T> {
    if (node == null) throw argumentNull('node');
    if (node._list !== null) throw attachedNode();
    return node;
  }

  /** `node` linked in before `before`, or at the back for null. */
  private insertBefore(before: LinkedListNode<T> | null, node: LinkedListNode<T>): void {
    const after = before === null ? this.tail : before._previous;
    node._list = this;
    node._previous = after;
    node._next = before;
    if (after === null) this.head = node;
    else after._next = node;
    if (before === null) this.tail = node;
    else before._previous = node;
    this._count++;
    this.version++;
  }

  private unlink(node: LinkedListNode<T>): void {
    if (node._previous !== null) node._previous._next = node._next;
    else this.head = node._next;
    if (node._next !== null) node._next._previous = node._previous;
    else this.tail = node._previous;
    node._invalidate();
    this._count--;
    this.version++;
  }

  /** Front to back. A node added or removed while it is walked ends the walk as a stack's ends, where
   *  it walked on, through every node added at the end (#765). */
  *[Symbol.iterator](): Generator<T, undefined, unknown> {
    const version = this.version;
    for (let n = this.head; n !== null; n = n._next) {
      yield n.value;
      if (this.version !== version) throw collectionModifiedAfterInstantiation();
    }
    return undefined;
  }

  /** Front-to-back order, matching .NET enumeration. */
  toArray(): T[] {
    return [...this];
  }

  /** The array System.Text.Json writes for a linked list, front to back: with no `toJSON` it was
   * written as its node graph from the head (#597). */
  toJSON(): T[] {
    return this.toArray();
  }
}

/** `new LinkedList<T>()` and `new LinkedList<T>(collection)`. */
export function linkedList<T>(...argument: [] | [Iterable<T> | string | null]): LinkedList<T> {
  return new LinkedList<T>(constructorArgument(argument, false).items);
}

/** `new LinkedListNode<T>(value)`: a node of no list. It was built as a linked list of the value,
 *  which a number is not (#866). */
export function linkedListNode<T>(value: T): LinkedListNode<T> {
  return new LinkedListNode(value);
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

/** What .NET throws for a member read or a call through null, in its words. */
function nullReference(): Error {
  return exception('System.NullReferenceException', 'Object reference not set to an instance of an object.');
}

/**
 * `ICollection<T>.Add`, for the collection the interface holds when the call runs, each adding as its
 * own `ICollection<T>.Add` does in .NET (#593): an array (a `List<T>`) appends; a set adds a value it
 * does not hold and ignores one it does, a `HashSet` and a `SortedSet` alike; a linked list adds LAST;
 * a dictionary (`ICollection<KeyValuePair<K, V>>`) adds the pair's key and value and refuses a key
 * already there in its own words, the runtime's `Dictionary` and a sorted map alike; and a twin calls
 * its own `add`. It was an array's `push`, which none of the others has. An array stands for a `T[]`
 * as well as a `List<T>`, so a fixed-size array behind the face grows here where .NET refuses the call
 * (NotSupportedException): the value carries nothing that tells the two apart (#711). A null throws the
 * NullReferenceException a call through null throws in .NET.
 */
export function add<T>(collection: unknown, item: T): void {
  if (collection == null) throw nullReference();
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
 * `clear` (#593). It was an array's `splice`, which a set, a linked list and a dictionary lack. A null
 * throws as {@link add} does.
 */
export function clear(collection: unknown): void {
  if (collection == null) throw nullReference();
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
 * caller handed over, is read by its subscript as before. A null throws as {@link add} does, where a
 * read of a member through it threw JavaScript's own TypeError, in its own words.
 */
export function item<T = any>(list: Indexed<T>, index: number): T {
  if (list == null) throw nullReference();
  if (Array.isArray(list)) return list[index] as T;
  const twin = list as ArrayLike<T> & { item?: (index: number) => T };
  return typeof twin.item === 'function' ? twin.item(index) : twin[index];
}

/**
 * The write beside {@link item}: an array's subscript, or a twin's indexer setter, `setItem`. It answers
 * the value written, as C#'s assignment does, whatever the setter does with its own copy. A null throws
 * as {@link add} does.
 */
export function setItem<T = any>(list: WritablyIndexed<T>, index: number, value: T): T {
  if (list == null) throw nullReference();
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
 * it holds. A null throws the NullReferenceException `xs.Count` throws in .NET: a null-conditional
 * `xs?.Count` and a property pattern test the receiver before they count, so only a read through null
 * reaches here, and counting it as none answered 0 where .NET throws.
 */
export function count(collection: unknown): number {
  if (collection == null) throw nullReference();
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
