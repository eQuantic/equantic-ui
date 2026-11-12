import { describe, expect, it } from 'vitest';
import { boundDelegate, combineDelegate, delegateOver, removeDelegate } from './delegates';

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
