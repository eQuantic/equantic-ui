import { describe, expect, it } from 'vitest';
import {
  addToEvent,
  boundDelegate,
  combineDelegate,
  delegateOver,
  removeDelegate,
  removeFromEvent,
} from './delegates';

describe('a delegate of a method', () => {
  class Box {
    n = 1;
    m(): number {
      return this.n;
    }
    other(): number {
      return 2;
    }
  }

  it('is one function for a target and a method', () => {
    const box = new Box();
    expect(boundDelegate(box, box.m)).toBe(boundDelegate(box, box.m));
    expect(boundDelegate(box, box.m)).not.toBe(boundDelegate(box, box.other));
    expect(boundDelegate(box, box.m)).not.toBe(boundDelegate(new Box(), box.m));
  });

  it('runs over its target', () => {
    const box = new Box();
    const read = boundDelegate(box, box.m);
    box.n = 5;
    expect(read()).toBe(5);
  });

  it('is found by a removal when it is made again', () => {
    const box = new Box();
    const added = combineDelegate(null, boundDelegate(box, box.m));
    expect(removeDelegate(added, boundDelegate(box, box.m))).toBeNull();
  });
});

describe('a delegate built from another', () => {
  it('is one function for a source, and never the source', () => {
    const source = (n: number) => n;
    const make = (held: typeof source) => (n: number) => held(n);
    expect(delegateOver(source, make)).toBe(delegateOver(source, make));
    expect(delegateOver(source, make)).not.toBe(source);
  });
});

describe('an event whose implementation the emitter cannot see (#746)', () => {
  it('calls the add and remove accessors the twin wrote', () => {
    const seen: string[] = [];
    const target = {
      $add$rung(handler: () => void) {
        seen.push('add');
        void handler;
      },
      $remove$rung(handler: () => void) {
        seen.push('remove');
        void handler;
      },
    };
    const handler = () => {};

    addToEvent(target, 'rung', handler);
    removeFromEvent(target, 'rung', handler);

    expect(seen).toEqual(['add', 'remove']);
    expect('rung' in target).toBe(false);
  });

  it('composes the invocation list of a field-like event, which has no accessors', () => {
    const target: { rung?: (() => void) | null } = {};
    let count = 0;
    const one = () => (count += 1);
    const ten = () => (count += 10);

    addToEvent(target, 'rung', one);
    addToEvent(target, 'rung', ten);
    target.rung?.();
    removeFromEvent(target, 'rung', one);
    target.rung?.();

    expect(count).toBe(21);
  });
});

describe('a multicast delegate', () => {
  it('runs its handlers in the order they were added, and removes the last occurrence', () => {
    const calls: number[] = [];
    const a = () => calls.push(1);
    const b = () => calls.push(2);

    const composed = removeDelegate(combineDelegate(combineDelegate(combineDelegate(null, a), b), a), a);
    composed?.();

    expect(calls).toEqual([1, 2]);
  });
});
