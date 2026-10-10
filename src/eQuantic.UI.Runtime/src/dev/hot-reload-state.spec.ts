import { describe, expect, it } from 'vitest';
import { $eq } from '@equantic/runtime';
import { StatefulComponent } from '../core/component';
import type { Component } from '../core/types';
import { Color, ColorToken, EdgeInsets, Point, Rect } from '../shared/value-types';
import { dateTime, DateTime } from '../utils/datetime';
import { dec, Decimal } from '../utils/decimal';
import { Dictionary } from '../utils/dictionary';
import { sortedDictionary } from '../utils/sorted';
import { capturePageState, restorePageState } from './hot-reload-state';

/**
 * A page's fields cross a hot reload, as a write-once twin declares them: class fields on the page
 * itself. The reload read a `_state` bag no such page has, so `Count: 3` came back `Count: 0` after
 * every save, measured on the dashboard sample under `dotnet run` and under `dotnet watch` (#664).
 * Each case goes through JSON, as the state does through sessionStorage.
 */
class Counter extends StatefulComponent {
  static $typeId = 'App.Counter';
  _count = 0;
  _items: number[] = [];
  _label = 'start';
  _onPick = (): number => this._count;
  build(): Component {
    throw new Error('not built here');
  }
}

/** The same page after an edit: `_label` is gone and `_title` is new. */
class EditedCounter extends StatefulComponent {
  static $typeId = 'App.Counter';
  _count = 0;
  _items: number[] = [];
  _title = 'new';
  build(): Component {
    throw new Error('not built here');
  }
}

/** A record of the app's: its twin has `with` and `equals`, and says it is one, as eqc writes it. */
class Spot {
  constructor(
    public x = 0,
    public y = 0,
  ) {}
  with(patch: Partial<Spot>): Spot {
    return Object.assign(new Spot(this.x, this.y), patch);
  }
  static $record = true;
  equals(other: unknown): boolean {
    return other instanceof Spot && other.x === this.x && other.y === this.y;
  }
}

/** An object with a life of its own, whose JSON is not it. */
class Controller {
  readonly seen = new Map<string, number>();
  readonly listeners: Array<() => void> = [];
}

const throughJson = <T>(value: T): T => JSON.parse(JSON.stringify(value)) as T;

describe('a hot reload carries a page across in the fields its C# declares', () => {
  it('captures the page fields and none of the runtime fields beside them', () => {
    const page = new Counter();
    page._count = 3;
    page._items.push(1, 2);

    const state = capturePageState(page);

    expect(state.fields).toEqual({ _count: 3, _items: [1, 2], _label: 'start' });
    expect(state.specs).toEqual({});
  });

  it('restores them into the reloaded page before it builds', () => {
    const before = new Counter();
    before._count = 3;
    before._items.push(1, 2);
    before._label = 'kept';

    const after = new Counter();
    restorePageState(after, throughJson(capturePageState(before)));

    expect(after._count).toBe(3);
    expect(after._items).toEqual([1, 2]);
    expect(after._label).toBe('kept');
  });

  it('leaves behind a field the edit removed, and a field the edit added keeps its initializer', () => {
    const before = new Counter();
    before._count = 5;
    before._label = 'gone';

    const after = new EditedCounter();
    restorePageState(after, throughJson(capturePageState(before)));

    expect(after._count).toBe(5);
    expect(after._title).toBe('new');
    expect(Object.prototype.hasOwnProperty.call(after, '_label')).toBe(false);
  });

  it('carries a record and a long, and leaves a controller with its initializer', () => {
    // A controller rebuilt from its JSON came back with a map member no map method accepts.
    class Drawing extends StatefulComponent {
      static $typeId = 'App.Drawing';
      _at = new Spot();
      _total = 0n;
      _controller = new Controller();
      build(): Component {
        throw new Error('not built here');
      }
    }

    const before = new Drawing();
    before._at = new Spot(3, 4);
    before._total = 5n;
    before._controller.seen.set('a', 1);
    const state = throughJson(capturePageState(before));

    const after = new Drawing();
    const fresh = after._controller;
    restorePageState(after, state);

    expect('_controller' in state.fields).toBe(false);
    expect(after._controller).toBe(fresh);
    expect(after._at).toBeInstanceOf(Spot);
    expect(after._at.equals(new Spot(3, 4))).toBe(true);
    expect(after._total).toBe(5n);
  });

  it('leaves behind alone a field JSON cannot carry', () => {
    const page = new Counter() as Counter & { _loop?: unknown };
    const loop: Record<string, unknown> = {};
    loop.self = loop;
    page._loop = loop;
    page._count = 2;

    const state = capturePageState(page);

    expect(state.fields._count).toBe(2);
    expect('_loop' in state.fields).toBe(false);
  });
});

/**
 * What crosses is what comes back as it was, at every depth, rebuilt by the hydration spec of its
 * runtime type; anything else keeps its initializer. Each case was a finding of Copilot's first round
 * on #672, and fails on the commit before the fix.
 */
describe('a hot reload gives back each value as it was, or leaves its initializer', () => {
  it('leaves a number JSON writes as null with its initializer, alone, in a list or in a record', () => {
    class Gauge extends StatefulComponent {
      static $typeId = 'App.Gauge';
      _ratio = 0.5;
      _limit = 1;
      _readings: number[] = [];
      _at = new Spot();
      _count = 0;
      build(): Component {
        throw new Error('not built here');
      }
    }

    const before = new Gauge();
    before._ratio = NaN;
    before._limit = Infinity;
    before._readings = [1, -Infinity];
    before._at = new Spot(NaN, 2);
    before._count = 4;

    const after = new Gauge();
    const readings = after._readings;
    restorePageState(after, throughJson(capturePageState(before)));

    expect(after._ratio).toBe(0.5);
    expect(after._limit).toBe(1);
    expect(after._readings).toBe(readings);
    expect(after._at.equals(new Spot())).toBe(true);
    expect(after._count).toBe(4);
  });

  it('leaves a dictionary holding a controller, and a sorted dictionary, with its initializer', () => {
    class Registry extends StatefulComponent {
      static $typeId = 'App.Registry';
      _byName = new Dictionary<string, Controller>();
      _ranks = sortedDictionary<string, number>(null, 'text');
      _count = 0;
      build(): Component {
        throw new Error('not built here');
      }
    }

    const before = new Registry();
    before._byName.set('a', new Controller());
    before._ranks.set('b', 2);
    before._ranks.set('a', 1);
    before._count = 1;

    const after = new Registry();
    const byName = after._byName;
    const ranks = after._ranks;
    restorePageState(after, throughJson(capturePageState(before)));

    expect(after._byName).toBe(byName);
    expect(after._ranks).toBe(ranks);
    expect(after._count).toBe(1);
  });

  it('carries the vocabulary value types as themselves, alone, in a list the page declares and in a record', () => {
    class Frame {
      constructor(
        public box = new Rect(),
        public label = '',
      ) {}
      with(patch: Partial<Frame>): Frame {
        return Object.assign(new Frame(this.box, this.label), patch);
      }
      static $record = true;
      equals(other: unknown): boolean {
        return other instanceof Frame && other.label === this.label;
      }
    }
    class Canvas extends StatefulComponent {
      static $typeId = 'App.Canvas';
      // An empty list types nothing, so a list of Points crosses where the page declares it, as eqc
      // declares `List<Point>` for a member its manifest carries.
      static get $hydration() {
        return { _path: [{ of: Point, members: { x: 'single', y: 'single' } }] };
      }
      _at = new Point();
      _pad = new EdgeInsets();
      _ink = new ColorToken(Color.black);
      _path: Point[] = [];
      _frame = new Frame();
      build(): Component {
        throw new Error('not built here');
      }
    }

    const before = new Canvas();
    before._at = new Point(3, 4);
    before._pad = EdgeInsets.symmetric(16, 8);
    before._ink = new ColorToken(Color.fromRgb(1, 2, 3), Color.white);
    before._path = [new Point(1, 2), new Point(5, 6)];
    before._frame = new Frame(new Rect(1, 2, 30, 40), 'f');

    const after = new Canvas();
    restorePageState(after, throughJson(capturePageState(before)));

    expect(after._at).toBeInstanceOf(Point);
    expect(after._at.dot(new Point(1, 1))).toBe(7);
    expect(after._pad).toBeInstanceOf(EdgeInsets);
    expect([after._pad.start, after._pad.top, after._pad.end, after._pad.bottom]).toEqual([
      16, 8, 16, 8,
    ]);
    expect(after._ink).toBeInstanceOf(ColorToken);
    expect(after._ink.light).toEqual({ r: 1, g: 2, b: 3, a: 255 });
    expect(after._ink.dark).toEqual(Color.white);
    expect(after._path.map((point) => point instanceof Point)).toEqual([true, true]);
    expect(after._path[1].dot(new Point(1, 0))).toBe(5);
    expect(after._frame).toBeInstanceOf(Frame);
    expect(after._frame.label).toBe('f');
    expect(after._frame.box).toBeInstanceOf(Rect);
    expect(after._frame.box.right).toBe(31);
  });

  it('carries each value as its type at every depth, into the empty and null fields the page declares', () => {
    class Ledger extends StatefulComponent {
      static $typeId = 'App.Ledger';
      // As eqc declares these members for a page whose members cross: an empty or a null initializer
      // types nothing, and the declaration says what each one holds.
      static get $hydration() {
        return {
          _ids: ['long'],
          _totals: { dict: 'long', key: 'number' },
          _prices: { dict: null, key: 'decimal', byValue: true },
          _meta: 'declared',
          _maybe: 'long',
          _due: 'dateTime',
        };
      }
      _ids: bigint[] = [];
      _totals = new Dictionary<number, bigint>();
      _prices = new Dictionary<Decimal, string>(null, true);
      _meta = { tags: [] as string[], last: { at: 0n } };
      _maybe: bigint | null = null;
      _due: DateTime | null = null;
      build(): Component {
        throw new Error('not built here');
      }
    }

    const before = new Ledger();
    before._ids = [5n, 9007199254740993n];
    before._totals.set(1, 5n);
    before._totals.set(2, 7n);
    before._prices.set(dec('1.50'), 'a');
    before._meta = { tags: ['x'], last: { at: 3n } };
    before._maybe = 6n;
    before._due = dateTime(2026, 10, 8);

    const after = new Ledger();
    restorePageState(after, throughJson(capturePageState(before)));

    expect(after._ids).toEqual([5n, 9007199254740993n]);
    expect(after._ids[0] - 1n).toBe(4n);
    expect(after._totals).toBeInstanceOf(Dictionary);
    expect(after._totals.keys()).toEqual([1, 2]);
    expect(after._totals.get(1)).toBe(5n);
    expect(after._totals.get(2)! - 1n).toBe(6n);
    expect(after._prices.get(dec('1.50'))).toBe('a');
    expect(after._prices.equality).toBe(true);
    expect(after._meta.tags).toEqual(['x']);
    expect(after._meta.last.at).toBe(3n);
    expect(after._maybe).toBe(6n);
    expect(after._due).toBeInstanceOf(DateTime);
    expect(after._due!.equals(dateTime(2026, 10, 8))).toBe(true);
  });

  it("leaves a list of the app's records with its initializer, since only an initializer names their class", () => {
    class Route extends StatefulComponent {
      static $typeId = 'App.Route';
      _stops: Spot[] = [];
      _count = 0;
      build(): Component {
        throw new Error('not built here');
      }
    }

    const before = new Route();
    before._stops = [new Spot(1, 2)];
    before._count = 2;

    const after = new Route();
    const stops = after._stops;
    restorePageState(after, throughJson(capturePageState(before)));

    expect(after._stops).toBe(stops);
    expect(after._count).toBe(2);
  });

  it('leaves a field whose type the edit changed with the initializer the edit gave it', () => {
    class Sized extends StatefulComponent {
      static $typeId = 'App.Sized';
      _size: number | string = 3;
      _name = 'a';
      build(): Component {
        throw new Error('not built here');
      }
    }
    class Resized extends StatefulComponent {
      static $typeId = 'App.Sized';
      _size: number | string = 'large';
      _name = 'b';
      build(): Component {
        throw new Error('not built here');
      }
    }

    const before = new Sized();
    before._size = 5;
    before._name = 'z';

    const after = new Resized();
    restorePageState(after, throughJson(capturePageState(before)));

    expect(after._size).toBe('large');
    expect(after._name).toBe('z');
  });
});

/**
 * eqc's output for the two records `PropertyStoreConformanceTests` runs on both sides, from
 * `ComponentCompiler.CompileSource`, laid out by the formatter and its `patch: any` typed. `FRec`'s
 * setter doubles what it is given, `GRec`'s getter adds one to what it keeps, and both keep the value
 * in the store `$x`, which their `equals` and `getHashCode` read and their `toJSON` reads through the
 * getter.
 *
 *     public record FRec { public int X { get; set => field = value * 2; } }
 *     public record GRec { public int X { get => field + 1; set => field = value; } }
 */
class FRec {
  declare $x: number;
  constructor() {
    this.$x = 0;
  }
  equals(o: unknown) {
    return o instanceof FRec && o.constructor === this.constructor && $eq.equals(this.$x, o.$x);
  }
  with(patch: Partial<this>): FRec {
    return $eq.withPatch(this, patch);
  }
  static $record = true;
  getHashCode(): number {
    return $eq.hash.combine(this.$x);
  }
  toJSON(): Record<string, unknown> {
    return $eq.json(this);
  }
  set x(value: number) {
    this.$x = value * 2;
  }
  get x() {
    return this.$x;
  }
  toString() {
    return `FRec { X = ${this.x} }`;
  }
}
class GRec {
  declare $x: number;
  constructor() {
    this.$x = 0;
  }
  equals(o: unknown) {
    return o instanceof GRec && o.constructor === this.constructor && $eq.equals(this.$x, o.$x);
  }
  with(patch: Partial<this>): GRec {
    return $eq.withPatch(this, patch);
  }
  static $record = true;
  getHashCode(): number {
    return $eq.hash.combine(this.$x);
  }
  toJSON(): Record<string, unknown> {
    return $eq.json(this);
  }
  get x() {
    return this.$x + 1;
  }
  set x(value: number) {
    this.$x = value;
  }
  toString() {
    return `GRec { X = ${this.x} }`;
  }
}

/**
 * What crosses comes back exactly, and only into a field the reloaded page still types the same way.
 * Each case was a finding of Copilot's second round on #672, or one of its kind, and fails on the
 * commit before the fix.
 */
describe('a hot reload gives back a value exactly, into the type the reloaded page gives it', () => {
  it('leaves a negative zero with its initializer, alone, in a list, in a record and as a key', () => {
    class Signed extends StatefulComponent {
      static $typeId = 'App.Signed';
      _zero = 1;
      _list: number[] = [];
      _at = new Spot();
      _byZero = new Dictionary<number, string>();
      _byName = new Dictionary<string, string | undefined>();
      _count = 0;
      build(): Component {
        throw new Error('not built here');
      }
    }

    const before = new Signed();
    before._zero = -0;
    before._list = [1, -0];
    before._at = new Spot(-0, 2);
    before._byZero.set(-0, 'z');
    // An object drops a member that holds undefined, so the entry would not come back at all.
    before._byName.set('a', undefined);
    before._count = 3;

    const after = new Signed();
    const list = after._list;
    const byZero = after._byZero;
    const byName = after._byName;
    restorePageState(after, throughJson(capturePageState(before)));

    expect(Object.is(after._zero, 1)).toBe(true);
    expect(after._list).toBe(list);
    expect(after._at.equals(new Spot())).toBe(true);
    expect(after._byZero).toBe(byZero);
    expect(after._byName).toBe(byName);
    expect(after._count).toBe(3);
  });

  it('gives back a record that keeps a store as the store held it, through none of its accessors', () => {
    class Stores extends StatefulComponent {
      static $typeId = 'App.Stores';
      _f = new FRec();
      _g = new GRec();
      build(): Component {
        throw new Error('not built here');
      }
    }

    const before = new Stores();
    before._f = new FRec();
    before._f.x = 5;
    before._g = new GRec();
    before._g.x = 3;

    const after = new Stores();
    restorePageState(after, throughJson(capturePageState(before)));

    expect(after._f).toBeInstanceOf(FRec);
    expect(after._f.x).toBe(10);
    expect(after._f.equals(before._f)).toBe(true);
    expect(after._g).toBeInstanceOf(GRec);
    expect(after._g.x).toBe(4);
    expect(after._g.equals(before._g)).toBe(true);
  });

  it('leaves a record whose member the edit made a property with its initializer, running no setter', () => {
    const ran: string[] = [];
    // The same record before and after an edit that gave `x` accessors over a store.
    const before = (() => {
      class Mark {
        constructor(public x = 0) {}
        with(patch: Partial<Mark>): Mark {
          return Object.assign(new Mark(this.x), patch);
        }
        static $record = true;
        equals(other: unknown): boolean {
          return other instanceof Mark && other.x === this.x;
        }
      }
      return Mark;
    })();
    const after = (() => {
      class Mark {
        declare $x: number;
        constructor() {
          this.$x = 0;
        }
        get x(): number {
          return this.$x;
        }
        set x(value: number) {
          ran.push('set x');
          this.$x = value * 2;
        }
        with(patch: Record<string, unknown>): Mark {
          return Object.assign(new Mark(), this, patch);
        }
        static $record = true;
        equals(other: unknown): boolean {
          return other instanceof Mark && other.$x === this.$x;
        }
      }
      return Mark;
    })();
    class Marked extends StatefulComponent {
      static $typeId = 'App.Marked';
      _mark: object = new before();
      build(): Component {
        throw new Error('not built here');
      }
    }
    class Remarked extends StatefulComponent {
      static $typeId = 'App.Marked';
      _mark: object = new after();
      build(): Component {
        throw new Error('not built here');
      }
    }

    const page = new Marked();
    page._mark = new before(5);
    const reloaded = new Remarked();
    const mark = reloaded._mark;
    restorePageState(reloaded, throughJson(capturePageState(page)));

    expect(reloaded._mark).toBe(mark);
    expect(ran).toEqual([]);
  });

  it('leaves a list, a dictionary or a null field whose type the edit changed with its initializer', () => {
    class Ledger extends StatefulComponent {
      static $typeId = 'App.Retyped';
      _ids: bigint[] = [];
      _codes: bigint[] = [0n];
      _byId = new Dictionary<number, bigint>();
      _maybe: bigint | null = null;
      _names: string[] = [];
      build(): Component {
        throw new Error('not built here');
      }
    }
    /** The same page after an edit that made each of them text. */
    class Retyped extends StatefulComponent {
      static $typeId = 'App.Retyped';
      _ids: string[] = [];
      _codes: string[] = [''];
      _byId = new Dictionary<string, string>();
      _maybe: string | null = null;
      _names: string[] = [];
      build(): Component {
        throw new Error('not built here');
      }
    }

    const before = new Ledger();
    before._ids = [5n];
    before._codes = [7n];
    before._byId.set(1, 5n);
    before._maybe = 6n;
    before._names = ['a'];

    const after = new Retyped();
    const ids = after._ids;
    const codes = after._codes;
    const byId = after._byId;
    restorePageState(after, throughJson(capturePageState(before)));

    expect(after._ids).toBe(ids);
    expect(after._codes).toBe(codes);
    expect(after._byId).toBe(byId);
    expect(after._maybe).toBeNull();
    // A list of text into a list of text, which nothing on the page contradicts, still crosses.
    expect(after._names).toEqual(['a']);
  });

  it('decides by the type the reloaded page declares, for an empty or a null initializer too', () => {
    class Declared extends StatefulComponent {
      static $typeId = 'App.Declared';
      static get $hydration() {
        return {
          _ids: ['long'],
          _due: 'dateTime',
          _byId: { dict: 'long', key: 'number' },
          _names: 'declared',
        };
      }
      _ids: bigint[] | null = null;
      _due: DateTime | null = null;
      _byId = new Dictionary<number, bigint>();
      _names: string[] = [];
      build(): Component {
        throw new Error('not built here');
      }
    }
    /** The same page after an edit that changed each type, as eqc declares the new ones. */
    class Redeclared extends StatefulComponent {
      static $typeId = 'App.Declared';
      static get $hydration() {
        return { _ids: 'declared', _due: 'declared', _byId: { dict: null }, _names: ['long'] };
      }
      _ids: string[] | null = null;
      _due: string | null = null;
      _byId = new Dictionary<string, string>();
      _names: bigint[] = [];
      build(): Component {
        throw new Error('not built here');
      }
    }

    const before = new Declared();
    before._ids = [5n];
    before._due = dateTime(2026, 10, 8);
    before._byId.set(1, 5n);
    before._names = ['a'];
    const state = throughJson(capturePageState(before));

    const same = new Declared();
    restorePageState(same, state);
    expect(same._ids).toEqual([5n]);
    expect(same._due).toBeInstanceOf(DateTime);
    expect(same._byId.get(1)).toBe(5n);
    expect(same._names).toEqual(['a']);

    const changed = new Redeclared();
    const byId = changed._byId;
    const names = changed._names;
    restorePageState(changed, state);
    expect(changed._ids).toBeNull();
    expect(changed._due).toBeNull();
    expect(changed._byId).toBe(byId);
    expect(changed._names).toBe(names);
  });
});

/**
 * eqc's output for an app's ordinary class that overrides `Equals` and declares a `With` of its own
 * (ComponentCompiler.CompileSource, laid out by the formatter and its `any`s typed): its twin has
 * `equals` and `with` as a record's does, and is no record.
 *
 *     public class Tally
 *     {
 *         public int Count;
 *         public string Name = "";
 *         public override bool Equals(object? obj) => obj is Tally other && other.Count == Count && other.Name == Name;
 *         public override int GetHashCode() => Count;
 *         public Tally With(int count) => new Tally { Count = count, Name = Name };
 *     }
 */
class Tally {
  constructor() {
    this.count = 0;
    this.name = '';
  }
  count!: number;
  name!: string;
  equals(obj: unknown) {
    let other: Tally;
    return (
      obj instanceof Tally &&
      ((other = obj), true) &&
      other.count === this.count &&
      other.name === this.name
    );
  }
  getHashCode() {
    return this.count;
  }
  with(count: number) {
    let $n0: Tally;
    return (($n0 = new Tally()), ($n0.count = count), ($n0.name = this.name), $n0);
  }
}

/** Copilot's third round on #672: each case fails on the commit before the fix. */
describe('a hot reload carries a record by what eqc says it is', () => {
  it("leaves an app's class with its initializer, though it declares equals and with", () => {
    class Tallied extends StatefulComponent {
      static $typeId = 'App.Tallied';
      _tally = new Tally();
      build(): Component {
        throw new Error('not built here');
      }
    }

    const before = new Tallied();
    before._tally.count = 7;
    before._tally.name = 'seven';
    const state = throughJson(capturePageState(before));

    const after = new Tallied();
    const tally = after._tally;
    restorePageState(after, state);

    expect('_tally' in state.fields).toBe(false);
    expect(after._tally).toBe(tally);
  });
});
