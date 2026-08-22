import { describe, it, expect } from 'vitest';
import {
  Queue,
  queue,
  Stack,
  stack,
  ValueMap,
  valueMap,
  LinkedList,
  linkedList,
} from './collections';

describe('Queue<T> — FIFO', () => {
  it('enqueues and dequeues in order', () => {
    const q = queue<number>();
    q.enqueue(1);
    q.enqueue(2);
    q.enqueue(3);
    expect(q.dequeue()).toBe(1);
    expect(q.dequeue()).toBe(2);
    expect(q.count).toBe(1);
  });

  it('peeks the front without removing', () => {
    const q = queue<number>();
    q.enqueue(5);
    expect(q.peek()).toBe(5);
    expect(q.count).toBe(1);
  });

  it('toArray is front-first', () => {
    const q = queue<number>();
    q.enqueue(10);
    q.enqueue(20);
    q.enqueue(30);
    expect(q.toArray()).toEqual([10, 20, 30]);
  });

  it('throws on empty dequeue/peek', () => {
    expect(() => queue<number>().dequeue()).toThrow();
    expect(() => queue<number>().peek()).toThrow();
  });

  it('seeds from an iterable', () => {
    expect(new Queue([1, 2, 3]).dequeue()).toBe(1);
  });
});

describe('Stack<T> — LIFO', () => {
  it('pushes and pops in reverse', () => {
    const s = stack<number>();
    s.push(1);
    s.push(2);
    s.push(3);
    expect(s.pop()).toBe(3);
    expect(s.pop()).toBe(2);
    expect(s.count).toBe(1);
  });

  it('peeks the top', () => {
    const s = stack<number>();
    s.push(10);
    s.push(20);
    expect(s.peek()).toBe(20);
    expect(s.count).toBe(2);
  });

  it('toArray is top-first (LIFO), matching .NET', () => {
    const s = stack<number>();
    s.push(10);
    s.push(20);
    s.push(30);
    expect(s.toArray()).toEqual([30, 20, 10]);
  });

  it('contains', () => {
    const s = stack<string>();
    s.push('a');
    s.push('b');
    expect(s.contains('a')).toBe(true);
    expect(s.contains('z')).toBe(false);
  });

  it('throws on empty pop/peek', () => {
    expect(() => stack<number>().pop()).toThrow();
    expect(() => new Stack<number>().peek()).toThrow();
  });
});

describe('ValueMap<K, V> — structurally-keyed dictionary', () => {
  // A record-like key: distinct object identity, equal by value.
  const pt = (x: number, y: number) => ({ x, y });

  it('keys by structural equality, not reference', () => {
    const m = valueMap<{ x: number; y: number }, string>();
    m.set(pt(1, 2), 'a');
    expect(m.get(pt(1, 2))).toBe('a'); // different object, same value
    expect(m.has(pt(1, 2))).toBe(true);
    expect(m.has(pt(9, 9))).toBe(false);
  });

  it('overwrites an existing equal key instead of duplicating', () => {
    const m = valueMap<{ x: number; y: number }, number>();
    m.set(pt(1, 2), 10);
    m.set(pt(1, 2), 20);
    expect(m.size).toBe(1);
    expect(m.get(pt(1, 2))).toBe(20);
  });

  it('get on an absent key is undefined (non-throwing)', () => {
    expect(valueMap<{ x: number }, number>().get({ x: 5 })).toBeUndefined();
  });

  it('delete removes a structurally-equal key', () => {
    const m = valueMap<{ x: number; y: number }, number>();
    m.set(pt(1, 2), 1);
    expect(m.delete(pt(1, 2))).toBe(true);
    expect(m.delete(pt(1, 2))).toBe(false);
    expect(m.size).toBe(0);
  });

  it('keys/values preserve insertion order', () => {
    const m = valueMap<{ x: number; y: number }, number>();
    m.set(pt(1, 1), 10);
    m.set(pt(2, 2), 20);
    expect(m.keys()).toEqual([pt(1, 1), pt(2, 2)]);
    expect(m.values()).toEqual([10, 20]);
  });

  it('tuple (array) keys compare element-wise', () => {
    const m = valueMap<[number, number], string>();
    m.set([1, 2], 'a');
    expect(m.get([1, 2])).toBe('a');
    expect(m.get([2, 1])).toBeUndefined();
  });

  it('iterates KeyValuePair-shaped { key, value } entries', () => {
    const m = valueMap<{ x: number }, number>();
    m.set({ x: 1 }, 100);
    m.set({ x: 2 }, 200);
    const seen = [...m].map((kvp) => kvp.key.x + kvp.value);
    expect(seen).toEqual([101, 202]);
  });

  it('clear empties the map', () => {
    const m = valueMap<{ x: number }, number>();
    m.set({ x: 1 }, 1);
    m.clear();
    expect(m.size).toBe(0);
  });

  it('seeds from an iterable of [key, value] pairs', () => {
    const m = new ValueMap<{ x: number }, number>([
      [{ x: 1 }, 10],
      [{ x: 2 }, 20],
    ]);
    expect(m.size).toBe(2);
    expect(m.get({ x: 2 })).toBe(20);
  });
});

describe('LinkedList<T> — doubly-linked', () => {
  it('addLast / addFirst order and count', () => {
    const l = linkedList<number>();
    l.addLast(2);
    l.addLast(3);
    l.addFirst(1);
    expect(l.toArray()).toEqual([1, 2, 3]);
    expect(l.count).toBe(3);
  });

  it('first/last nodes expose value and links', () => {
    const l = new LinkedList<number>([10, 20, 30]);
    expect(l.first!.value).toBe(10);
    expect(l.last!.value).toBe(30);
    expect(l.first!.next!.value).toBe(20);
    expect(l.last!.previous!.value).toBe(20);
    expect(l.first!.previous).toBeNull();
    expect(l.last!.next).toBeNull();
  });

  it('removeFirst / removeLast', () => {
    const l = new LinkedList<number>([1, 2, 3]);
    l.removeFirst();
    l.removeLast();
    expect(l.toArray()).toEqual([2]);
  });

  it('remove(value) by structural equality, returns found', () => {
    const l = new LinkedList<number>([1, 2, 3]);
    expect(l.remove(2)).toBe(true);
    expect(l.remove(9)).toBe(false);
    expect(l.toArray()).toEqual([1, 3]);
  });

  it('contains / clear', () => {
    const l = new LinkedList<string>(['a', 'b']);
    expect(l.contains('a')).toBe(true);
    expect(l.contains('z')).toBe(false);
    l.clear();
    expect(l.count).toBe(0);
    expect(l.first).toBeNull();
  });

  it('throws on remove from empty', () => {
    expect(() => linkedList<number>().removeFirst()).toThrow();
    expect(() => linkedList<number>().removeLast()).toThrow();
  });
});
