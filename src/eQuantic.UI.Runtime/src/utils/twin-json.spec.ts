import { describe, expect, it } from 'vitest';

import { TypeScriptLanguage } from '../shared/components/TypeScriptLanguage';
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

  it('is what a twin eqc writes with a store answers, its stores under their properties', () => {
    // The code engine's languages keep their overridable rules in stores: `Rules` is virtual in
    // CurlyBraceLanguage's C#, and each language overrides it.
    const json = JSON.parse(JSON.stringify(new TypeScriptLanguage())) as Record<string, unknown>;
    expect(Object.keys(json)).toContain('rules');
    expect(Object.keys(json).filter((key) => key.startsWith('$'))).toEqual([]);
    expect((json.rules as Record<string, unknown>).lineComment).toBe('//');
  });

  it('writes a nested twin through its own toJSON', () => {
    expect(JSON.parse(JSON.stringify({ items: [new Base()] }))).toEqual({ items: [{ count: 2, kind: 'base' }] });
  });

  /**
   * A field that moved a case apart from a member (`value$` beside the accessors of `value`, #396) is
   * storage the serializer never writes; the property it gave its name up to is written, read through
   * its getter, as System.Text.Json reads it. A method of that name writes nothing.
   */
  class Moved {
    declare value$: number;
    declare size$: number;
    constructor() {
      this.value$ = 6;
      this.size$ = 3;
    }
    get value(): number {
      return this.value$;
    }
    set value(given: number) {
      this.value$ = given * 2;
    }
    size(): number {
      return this.size$ * 2;
    }
    toJSON(): Record<string, unknown> {
      return twinJson(this);
    }
  }

  it('writes a moved field as the property it gave its name to, and never the field', () => {
    expect(JSON.parse(JSON.stringify(new Moved()))).toEqual({ value: 6 });
  });

  /**
   * A moved field and a property's store can stand for one property: `int value;` beside a virtual or
   * a `field`-backed `Value` holds `value$` and `$value`. System.Text.Json reads the getter once, and
   * both keys read it, so a getter with an effect ran twice and the second answer was written
   * (Copilot's second review of #696). Each JSON name is written once.
   */
  let reads = 0;
  class Stored {
    declare value$: number;
    declare $value: number;
    constructor() {
      this.value$ = 1;
      this.$value = 10;
    }
    get value(): number {
      reads += 1;
      return this.$value + reads;
    }
    toJSON(): Record<string, unknown> {
      return twinJson(this);
    }
  }

  it('reads a property a moved field and its store both stand for once, and writes it once', () => {
    reads = 0;
    expect(JSON.parse(JSON.stringify(new Stored()))).toEqual({ value: 11 });
    expect(reads).toBe(1);
  });

  /**
   * A field moves a `$` more when an ancestor holds the slot it would take (`value$$` beside an
   * inherited `value$`): every `$` after a name is the field's, and the name before them is the one
   * the serializer reads.
   */
  class Shelf {
    declare value$: number;
    constructor() {
      this.value$ = 1;
    }
    get value(): number {
      return this.value$ * 10;
    }
    toJSON(): Record<string, unknown> {
      return twinJson(this);
    }
  }
  class Shop extends Shelf {
    declare value$$: number;
    constructor() {
      super();
      this.value$$ = 4;
    }
  }

  it('writes a field moved past an inherited slot as the property, and neither field', () => {
    expect(JSON.parse(JSON.stringify(new Shop()))).toEqual({ value: 10 });
  });
});
