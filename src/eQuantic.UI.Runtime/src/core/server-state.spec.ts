import { describe, it, expect, afterEach } from 'vitest';
import { adoptServerState } from './component';

interface Payload {
  __INITIAL_STATE__?: Record<string, unknown>;
}

const win = window as unknown as Payload;

/**
 * SERVER DATA (the C# `IServerPrefetch` twin): the fields the server's prefetch filled cross by
 * FIELD NAME and land before the page's first render, so the client starts from what the server
 * already wrote as HTML instead of flashing the field defaults.
 */
describe('server-state adoption (C# IServerPrefetch twin)', () => {
  afterEach(() => {
    delete win.__INITIAL_STATE__;
  });

  it('adopts declared fields and consumes the payload', () => {
    const page = { _downloads: 627000, _packages: 23 };
    win.__INITIAL_STATE__ = { _downloads: 675617, _packages: 24 };

    adoptServerState(page);

    expect(page._downloads).toBe(675617);
    expect(page._packages).toBe(24);
    expect(win.__INITIAL_STATE__).toBeUndefined();
  });

  it('coerces a long that crossed as a string back to the field type', () => {
    // EqJson writes Int64 as a string so values beyond 2^53 survive the wire.
    const page = { _downloads: 627000 };
    win.__INITIAL_STATE__ = { _downloads: '675617' };

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
      _total: '10.50',
      _count: '9007199254740993',
      _todos: [{ id: '1', title: 'a' }],
      _label: 'x',
    };

    adoptServerState(page);

    expect(String(page._total)).toBe('10.50');
    expect(page._count).toBe(9007199254740993n);
    expect((page._todos as Todo[])[0]).toBeInstanceOf(Todo);
    expect((page._todos as Todo[])[0].id).toBe(1n);
    expect(page._label).toBe('x'); // no spec entry — assigned verbatim
  });

  it('ignores keys the component does not declare', () => {
    const page = { _downloads: 1 } as Record<string, unknown>;
    win.__INITIAL_STATE__ = { _downloads: 2, _stale: 'from another page' };

    adoptServerState(page);

    expect(page._downloads).toBe(2);
    expect('_stale' in page).toBe(false);
  });

  it('LEAVES a payload that matched nothing — its real owner has not rendered yet', () => {
    // A Core page reads the payload from its own state object; a shared component rendering first
    // must not swallow it.
    const unrelated = { _other: 0 };
    win.__INITIAL_STATE__ = { _downloads: 675617 };

    adoptServerState(unrelated);

    expect(win.__INITIAL_STATE__).toEqual({ _downloads: 675617 });
  });

  it('is a no-op without a payload', () => {
    const page = { _downloads: 627000 };
    adoptServerState(page);
    expect(page._downloads).toBe(627000);
  });
});
