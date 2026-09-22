import { describe, it, expect, afterEach, beforeEach } from 'vitest';
import { adoptServerState, adoptServerStateFor, nextComponentKey, resetComponentKeys } from './component';

interface Payload {
  __INITIAL_STATE__?: Record<string, Record<string, unknown>>;
}

const win = window as unknown as Payload;

/**
 * SERVER DATA (the C# `IServerPrefetch` twin): the fields the server's prefetch filled land before
 * the first render, so the client starts from what the server already wrote as HTML instead of
 * flashing the field defaults.
 *
 * KEYED BY COMPONENT, which is the change these cases carry. Only the root of a route used to
 * prefetch and the payload was one flat field map; now any component the page composes may declare
 * server data, so the payload says WHICH component each map belongs to — `Type#ordinal` in
 * depth-first expansion order, counted identically by the C# realizer and by the lowering walk here.
 *
 * The type half of the key is the safety: if the two sides ever expand different trees, an ordinal
 * alone would hand a component whatever sat at that number.
 */
describe('server-state adoption (C# IServerPrefetch twin)', () => {
  beforeEach(() => {
    resetComponentKeys();
  });

  afterEach(() => {
    delete win.__INITIAL_STATE__;
    resetComponentKeys();
  });

  class HomePage {
    _downloads = 627000;
    _packages = 23;
  }

  it('adopts the fields under the ROOT key, which is the first component expanded', () => {
    const page = new HomePage();
    win.__INITIAL_STATE__ = { 'HomePage#0': { _downloads: 675617, _packages: 24 } };

    adoptServerState(page);

    expect(page._downloads).toBe(675617);
    expect(page._packages).toBe(24);
  });

  it('does NOT consume the payload, because other components still have to read theirs', () => {
    // The flat payload was a single-render handoff to ONE owner and was deleted once read. A page
    // composing three prefetchers would have had the first one swallow the other two's entries.
    const page = new HomePage();
    win.__INITIAL_STATE__ = {
      'HomePage#0': { _downloads: 1 },
      'StatsHeader#0': { _count: 2 },
    };

    adoptServerState(page);

    expect(win.__INITIAL_STATE__?.['StatsHeader#0']).toEqual({ _count: 2 });
  });

  it('gives each component its own entry, and two of a type are told apart by the ordinal', () => {
    class StatsHeader {
      _count = 0;
    }
    win.__INITIAL_STATE__ = {
      'StatsHeader#0': { _count: 10 },
      'StatsHeader#1': { _count: 20 },
    };

    const first = new StatsHeader();
    const second = new StatsHeader();
    adoptServerStateFor(first, nextComponentKey('StatsHeader'));
    adoptServerStateFor(second, nextComponentKey('StatsHeader'));

    expect(first._count).toBe(10);
    expect(second._count).toBe(20);
  });

  it('leaves a component alone when the key names a different type — a drift is a missing value', () => {
    class StatsHeader {
      _count = 7;
    }
    win.__INITIAL_STATE__ = { 'SomethingElse#0': { _count: 99 } };

    const header = new StatsHeader();
    const applied = adoptServerStateFor(header, nextComponentKey('StatsHeader'));

    expect(applied).toBe(false);
    expect(header._count).toBe(7);
  });

  it('coerces a long that crossed as a string back to the field type', () => {
    // EqJson writes Int64 as a string so values beyond 2^53 survive the wire.
    class Downloads {
      _downloads = 627000;
    }
    const page = new Downloads();
    win.__INITIAL_STATE__ = { 'Downloads#0': { _downloads: '675617' } };

    adoptServerState(page);

    expect(page._downloads).toBe(675617);
  });

  it('hydrates by the class $hydration map when the compiler emitted one — the typed boundary', () => {
    class Todo {
      static $hydration = { id: 'long' as const };
      id = 0n;
      title = '';
    }
    class WalletPage {
      static $hydration = { _total: 'decimal' as const, _count: 'long' as const, _todos: [Todo] as const };
      _total: unknown = null; // even a null default hydrates — the spec, not the witness, types it
      _count: unknown = 0n;
      _todos: unknown = [];
      _label = '';
    }
    const page = new WalletPage();
    win.__INITIAL_STATE__ = {
      'WalletPage#0': {
        _total: '10.50',
        _count: '9007199254740993',
        _todos: [{ id: '1', title: 'a' }],
        _label: 'x',
      },
    };

    adoptServerState(page);

    expect(String(page._total)).toBe('10.50');
    expect(page._count).toBe(9007199254740993n);
    expect((page._todos as Todo[])[0]).toBeInstanceOf(Todo);
    expect((page._todos as Todo[])[0].id).toBe(1n);
    expect(page._label).toBe('x'); // no spec entry — assigned verbatim
  });

  it('ignores keys the component does not declare', () => {
    class Page {
      _downloads = 1;
    }
    const page = new Page() as Page & Record<string, unknown>;
    win.__INITIAL_STATE__ = { 'Page#0': { _downloads: 2, _stale: 'from another page' } };

    adoptServerState(page);

    expect(page._downloads).toBe(2);
    expect('_stale' in page).toBe(false);
  });

  it('is a no-op without a payload', () => {
    const page = new HomePage();
    adoptServerState(page);
    expect(page._downloads).toBe(627000);
  });
});
