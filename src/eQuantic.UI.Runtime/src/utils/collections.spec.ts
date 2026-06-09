import { describe, it, expect } from 'vitest';
import { Queue, queue, Stack, stack } from './collections';

describe('Queue<T> — FIFO', () => {
  it('enqueues and dequeues in order', () => {
    const q = queue<number>();
    q.enqueue(1); q.enqueue(2); q.enqueue(3);
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
    q.enqueue(10); q.enqueue(20); q.enqueue(30);
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
    s.push(1); s.push(2); s.push(3);
    expect(s.pop()).toBe(3);
    expect(s.pop()).toBe(2);
    expect(s.count).toBe(1);
  });

  it('peeks the top', () => {
    const s = stack<number>();
    s.push(10); s.push(20);
    expect(s.peek()).toBe(20);
    expect(s.count).toBe(2);
  });

  it('toArray is top-first (LIFO), matching .NET', () => {
    const s = stack<number>();
    s.push(10); s.push(20); s.push(30);
    expect(s.toArray()).toEqual([30, 20, 10]);
  });

  it('contains', () => {
    const s = stack<string>();
    s.push('a'); s.push('b');
    expect(s.contains('a')).toBe(true);
    expect(s.contains('z')).toBe(false);
  });

  it('throws on empty pop/peek', () => {
    expect(() => stack<number>().pop()).toThrow();
    expect(() => new Stack<number>().peek()).toThrow();
  });
});
