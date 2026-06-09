/**
 * .NET-compat `Queue<T>` and `Stack<T>` — FIFO / LIFO collections backed by a JS array, with the
 * .NET API and quirks: `Dequeue`/`Peek`/`Pop` throw on an empty collection, and `Stack.ToArray()`
 * returns items top-first (LIFO order), while `Queue.ToArray()` returns them front-first.
 *
 * The transpiler emits `$eq.collections.queue(...)` / `$eq.collections.stack(...)` for
 * `new Queue<T>(...)` / `new Stack<T>(...)` and maps the instance methods to camelCase.
 */

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
