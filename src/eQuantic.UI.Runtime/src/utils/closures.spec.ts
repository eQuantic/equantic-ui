import { describe, expect, it } from 'vitest';
import { statics } from './closures';

/** A static generic class's twin, as eqc writes one: statics reached through `this` (#1855). */
class Cache {
  static size = 4;
  static n = 0;
  static $slots: { items: number[] } | null = null;
  static $failure: unknown = null;
  static inits = 0;

  static $init(): { items: number[] } {
    if (this.$slots === null) {
      Cache.inits++;
      this.$slots = { items: [] };
    }
    return this.$slots;
  }

  static get items(): number[] {
    return this.$init().items;
  }

  static bump(): number {
    return ++this.n;
  }
}

describe('statics', () => {
  it('is one object per closed type, made once', () => {
    expect(statics(Cache, 'int')).toBe(statics(Cache, 'int'));
    expect(statics(Cache, 'int')).not.toBe(statics(Cache, 'string'));
  });

  it('keeps each closed type its own type initializer and slots', () => {
    const before = Cache.inits;
    statics(Cache, 'a').items.push(1, 2);
    statics(Cache, 'b').items.push(3);
    statics(Cache, 'a').items.push(4);
    expect(statics(Cache, 'a').items).toEqual([1, 2, 4]);
    expect(statics(Cache, 'b').items).toEqual([3]);
    expect(Cache.inits - before).toBe(2);
  });

  it('reads a constant through to the class, and keeps what a closed type writes its own', () => {
    expect(statics(Cache, 'c').size).toBe(4);
    statics(Cache, 'c').bump();
    statics(Cache, 'c').bump();
    statics(Cache, 'd').bump();
    expect(statics(Cache, 'c').n).toBe(2);
    expect(statics(Cache, 'd').n).toBe(1);
    expect(Cache.n).toBe(0);
  });
});
