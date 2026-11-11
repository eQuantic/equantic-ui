import { describe, expect, it } from 'vitest';
import { addToEvent, combineDelegate, removeDelegate, removeFromEvent } from './delegates';

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
