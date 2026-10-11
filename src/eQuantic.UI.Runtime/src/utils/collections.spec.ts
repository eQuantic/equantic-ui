import { describe, it, expect } from 'vitest';
import { sortedDictionary, sortedList, sortedSet } from './sorted';
import { dictionary, pair } from './dictionary';
import { hashSetOf } from './hash-set';
import {
  Queue,
  queue,
  Stack,
  stack,
  LinkedList,
  linkedList,
  remove,
  add,
  clear,
  count,
  item,
  setItem,
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

// ICollection<KeyValuePair<K, V>>.Remove, which reaches the runtime's remove with a dictionary. A C#
// case needs a KeyValuePair built by hand, which does not cross yet (#433), so the dictionary shapes
// are held here, on the pairs a dictionary itself yields (found in review, #421).
describe('remove over a dictionary, as ICollection<KeyValuePair<K, V>> removes', () => {
  it('removes the pair only when its key is there with an equal value', () => {
    const map = new Map<number, string>([[1, 'a']]);
    expect(remove(map as never, { key: 1, value: 'b' } as never)).toBe(false);
    expect(remove(map as never, { key: 2, value: 'a' } as never)).toBe(false);
    expect(map.size).toBe(1);
    expect(remove(map as never, { key: 1, value: 'a' } as never)).toBe(true);
    expect(map.size).toBe(0);
  });

  it("does the same on the runtime's dictionary, a pair it enumerates included", () => {
    const dict = dictionary<string, number>([
      ['a', 1],
      ['b', 2],
    ]);
    expect(remove(dict as never, { key: 'b', value: 3 } as never)).toBe(false);
    expect(remove(dict as never, { key: 'c', value: 2 } as never)).toBe(false);
    expect(remove(dict as never, [...dict][1] as never)).toBe(true);
    expect(dict.keys()).toEqual(['a']);
  });

  it('does the same on the sorted dictionary', () => {
    const sorted = sortedDictionary<number, string>();
    sorted.set(1, 'a');
    sorted.set(2, 'b');
    expect(remove(sorted as never, { key: 2, value: 'x' } as never)).toBe(false);
    expect(remove(sorted as never, { key: 2, value: 'b' } as never)).toBe(true);
    expect(sorted.size).toBe(1);
    expect(sorted.has(2)).toBe(false);
  });
});

// ICollection<T>'s Add and Clear, which reach the runtime with whichever collection the interface holds
// when the call runs (#593): an array's push and splice were all they had.
describe("ICollection<T>'s Add and Clear, as the collection behind the interface answers them", () => {
  it('adds as each collection adds its own: appended, a value a set holds ignored, a linked list last', () => {
    const list = [1];
    add(list, 2);
    expect(list).toEqual([1, 2]);
    const set = hashSetOf([1]);
    add(set, 2);
    add(set, 1);
    expect([...set]).toEqual([1, 2]);
    const linked = linkedList([1]);
    add(linked, 2);
    expect(linked.toArray()).toEqual([1, 2]);
    const sorted = sortedSet([3]);
    add(sorted, 1);
    add(sorted, 3);
    expect(sorted.toArray()).toEqual([1, 3]);
  });

  it("adds a dictionary's pair, and refuses a key already there in each dictionary's words", () => {
    const dict = dictionary<string, number>();
    add(dict, pair('a', 1));
    expect(dict.get('a')).toBe(1);
    expect(() => add(dict, pair('a', 2))).toThrow('An item with the same key has already been added. Key: a');
    const sorted = sortedDictionary<number, string>();
    add(sorted, pair(2, 'b'));
    add(sorted, pair(1, 'a'));
    expect(sorted.keys()).toEqual([1, 2]);
    expect(() => add(sortedList<number, string>([[2, 'b']]), pair(2, 'c'))).toThrow("Key: 2 (Parameter 'key')");
  });

  it('clears each in place, and a twin through its own members', () => {
    const list = [1, 2];
    clear(list);
    expect(list).toEqual([]);
    const set = hashSetOf([1]);
    clear(set);
    expect(set.size).toBe(0);
    const linked = linkedList([1, 2]);
    clear(linked);
    expect(linked.count).toBe(0);
    const held: number[] = [];
    const twin = {
      add: (item: number) => held.push(item),
      clear: () => (held.length = 0),
      get count() {
        return held.length;
      },
    };
    add(twin, 7);
    expect(count(twin)).toBe(1);
    clear(twin);
    expect(count(twin)).toBe(0);
  });
});

// A list face's indexer (IList<T>, IReadOnlyList<T>), which reaches the runtime with whichever list the
// face holds when it runs (#586): a subscript was all it had, which a twin does not answer.
describe("a list face's indexer, as the list behind the face answers it", () => {
  it('reads and writes an array or an array-like by subscript, a twin by item and setItem', () => {
    const array = [1, 2, 3];
    expect(item(array, 1)).toBe(2);
    expect(setItem(array, 1, 5)).toBe(5);
    expect(array).toEqual([1, 5, 3]);
    const held = [7, 8, 9];
    const twin = {
      item: (index: number) => held[index],
      setItem: (index: number, value: number) => {
        held[index] = Math.min(value, 10);
      },
      get count() {
        return held.length;
      },
    };
    expect(item(twin, 2)).toBe(9);
    expect(setItem(twin, 0, 42)).toBe(42);
    expect(held[0]).toBe(10);
    expect(count(twin)).toBe(3);
    const typed = new Float64Array([1, 2]);
    expect(item(typed, 1)).toBe(2);
    expect(setItem(typed, 0, 3)).toBe(3);
    expect(typed[0]).toBe(3);
  });

  it('counts a twin by its own count, never by a length or a size beside it', () => {
    const polyline = {
      item: (index: number) => [3, 4][index],
      get length() {
        return 12.5;
      },
      get size() {
        return 7;
      },
      get count() {
        return 2;
      },
    };
    expect(count(polyline)).toBe(2);
    expect(count(new Float64Array([1, 2, 3]))).toBe(3);
    expect(count(new Set([1, 2]))).toBe(2);
  });

  // A read or a call through null throws .NET's NullReferenceException in its words: the count of null
  // was none, 0 where `xs.Count` throws, and the others threw JavaScript's own TypeError. A
  // null-conditional and a pattern test the receiver before they reach any of them.
  it('refuses a null as .NET does, for a count, a read, a write, an Add and a Clear', () => {
    const message = 'Object reference not set to an instance of an object.';
    expect(() => count(null)).toThrow(message);
    expect(() => count(undefined)).toThrow(message);
    expect(() => item(null as unknown as number[], 0)).toThrow(message);
    expect(() => setItem(null as unknown as number[], 0, 1)).toThrow(message);
    expect(() => add(null, 1)).toThrow(message);
    expect(() => clear(null)).toThrow(message);
  });
});
