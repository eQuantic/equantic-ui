import { describe, expect, it } from 'vitest';

import { twinJson } from './twin-json';

/**
 * A twin's JSON is what a server action receives, and System.Text.Json binds it by the C# names. A
 * property that keeps a store (`$name` under accessors on the prototype, #591) was sent as `$name`,
 * which the server dropped, and the property itself, an accessor, was not sent at all.
 */
describe('twinJson', () => {
  class Base {
    declare $kind: string;
    declare count: number;
    constructor() {
      this.count = 2;
      this.$kind = 'base';
    }
    get kind(): string {
      return this.$kind;
    }
    set kind(value: string) {
      this.$kind = value;
    }
    toJSON(): Record<string, unknown> {
      return twinJson(this);
    }
  }

  class Derived extends Base {
    override get kind(): string {
      return 'derived';
    }
    override set kind(value: string) {
      super.kind = value;
    }
  }

  it('writes a store under its property, read through the property, and every other member as it is', () => {
    expect(JSON.parse(JSON.stringify(new Base()))).toEqual({ count: 2, kind: 'base' });
  });

  it('reads the property through the most derived accessor, as the serializer calls the getter', () => {
    expect(JSON.parse(JSON.stringify(new Derived()))).toEqual({ count: 2, kind: 'derived' });
  });

  it('copies a $ key that names no member of the value as it is', () => {
    expect(twinJson({ $loose: 1, other: 2 })).toEqual({ $loose: 1, other: 2 });
  });

  it('keeps a member called __proto__ a member', () => {
    const value = JSON.parse('{"__proto__":{"polluted":true}}') as object;
    const json = twinJson(value);
    expect(Object.prototype.hasOwnProperty.call(json, '__proto__')).toBe(true);
    expect(Object.getPrototypeOf(json)).toBe(Object.prototype);
  });

  it('writes a nested twin through its own toJSON', () => {
    expect(JSON.parse(JSON.stringify({ items: [new Base()] }))).toEqual({ items: [{ count: 2, kind: 'base' }] });
  });
});
