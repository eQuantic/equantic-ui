import { describe, it, expect, afterEach, beforeEach } from 'vitest';
import {
  adoptServerStateFor,
  nextComponentKey,
  resetComponentKeys,
  runComponentWalk,
} from './component';
import { lowerVisualNode } from '../shared/lowering';
import type { VisualNodeValue } from '../shared/nodes';

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
    static $hydration = { _downloads: 'declared', _packages: 'declared' } as const;
    _downloads = 627000;
    _packages = 23;
  }

  it('adopts the fields under the ROOT key, which is the first component expanded', () => {
    const page = new HomePage();
    win.__INITIAL_STATE__ = { 'HomePage#0': { _downloads: 675617, _packages: 24 } };

    runComponentWalk(page, true, () => undefined);

    expect(page._downloads).toBe(675617);
    expect(page._packages).toBe(24);
  });

  it('adopts a CLEARED field, because null is a value the prefetch loaded', () => {
    // The wire snapshot used to drop nulls, so a prefetch that CLEARS a non-null default wrote
    // nothing the client could read: the server drew the cleared value, the payload said nothing,
    // and hydration left the default the constructor set — the page reverting a moment after it
    // appeared, for the one value that is indistinguishable from absence. The server sends it now
    // and this is the side that has to write it.
    class Banner {
      static $hydration = { _message: 'declared' } as const;
      _message: string | null = 'still here';
    }
    const banner = new Banner();
    win.__INITIAL_STATE__ = { 'Banner#0': { _message: null } };

    expect(adoptServerStateFor(banner, nextComponentKey('Banner'))).toBe(true);
    expect(banner._message).toBeNull();
  });

  it('does NOT consume the payload, because other components still have to read theirs', () => {
    // The flat payload was a single-render handoff to ONE owner and was deleted once read. A page
    // composing three prefetchers would have had the first one swallow the other two's entries.
    const page = new HomePage();
    win.__INITIAL_STATE__ = {
      'HomePage#0': { _downloads: 1 },
      'StatsHeader#0': { _count: 2 },
    };

    runComponentWalk(page, true, () => undefined);

    expect(win.__INITIAL_STATE__?.['StatsHeader#0']).toEqual({ _count: 2 });
  });

  it('gives each component its own entry, and two of a type are told apart by the ordinal', () => {
    class StatsHeader {
      static $hydration = { _count: 'declared' } as const;
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
      static $hydration = { _count: 'declared' } as const;
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
      static $hydration = { _downloads: 'declared' } as const;
      _downloads = 627000;
    }
    const page = new Downloads();
    win.__INITIAL_STATE__ = { 'Downloads#0': { _downloads: '675617' } };

    runComponentWalk(page, true, () => undefined);

    expect(page._downloads).toBe(675617);
  });

  it('hydrates by the class $hydration map when the compiler emitted one — the typed boundary', () => {
    class Todo {
      static $hydration = { id: 'long' as const };
      id = 0n;
      title = '';
    }
    class WalletPage {
      static $hydration = {
        _total: 'decimal' as const,
        _count: 'long' as const,
        _todos: [Todo] as const,
        _label: 'declared' as const,
      };
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

    runComponentWalk(page, true, () => undefined);

    expect(String(page._total)).toBe('10.50');
    expect(page._count).toBe(9007199254740993n);
    expect((page._todos as Todo[])[0]).toBeInstanceOf(Todo);
    expect((page._todos as Todo[])[0].id).toBe(1n);
    expect(page._label).toBe('x'); // 'declared': the witness path, its default a string
  });

  it('ignores keys the class map does not list', () => {
    class Page {
      static $hydration = { _downloads: 'declared' } as const;
      _downloads = 1;
    }
    const page = new Page() as Page & Record<string, unknown>;
    win.__INITIAL_STATE__ = { 'Page#0': { _downloads: 2, _stale: 'from another page' } };

    runComponentWalk(page, true, () => undefined);

    expect(page._downloads).toBe(2);
    expect('_stale' in page).toBe(false);
  });

  it('does not take a key every object INHERITS for a field the component declares', () => {
    class Page {
      static $hydration = { _downloads: 'declared' } as const;
      _downloads = 1;
    }
    const page = new Page();
    // Parsed, as a page parses it: JSON keeps `__proto__` as an ordinary member.
    win.__INITIAL_STATE__ = {
      'Page#0': JSON.parse(
        '{"_downloads":2,"__proto__":{"planted":true},"constructor":"x"}',
      ) as Record<string, unknown>,
    };

    runComponentWalk(page, true, () => undefined);

    expect(page._downloads).toBe(2);
    expect(Object.getPrototypeOf(page)).toBe(Page.prototype);
    expect(page.constructor).toBe(Page);
  });

  it('is a no-op without a payload', () => {
    const page = new HomePage();
    runComponentWalk(page, true, () => undefined);
    expect(page._downloads).toBe(627000);
  });

  it('restarts the count on EVERY root render, not only the first', () => {
    // The count lives across renders. A root that re-rendered without restarting it handed the
    // components below Type#1, Type#2, … — keys the payload does not name — so a composed
    // component silently reverted to its defaults on the second pass. Only stateful children are
    // retained between renders; a composed stateless one is rebuilt, so it asks for a key again.
    class StatsHeader {
      static $hydration = { _count: 'declared' } as const;
      _count = 0;
    }
    win.__INITIAL_STATE__ = {
      'HomePage#0': { _downloads: 1 },
      'StatsHeader#0': { _count: 42 },
    };

    const page = new HomePage();

    // first render: the root claims #0 and the header gets StatsHeader#0
    runComponentWalk(page, true, () => undefined);
    const first = new StatsHeader();
    adoptServerStateFor(first, nextComponentKey('StatsHeader'));
    expect(first._count).toBe(42);

    // second render: the root is mounted and does NOT adopt, but must still consume #0 or
    // everything under it shifts by one.
    runComponentWalk(page, false, () => undefined);
    const second = new StatsHeader();
    adoptServerStateFor(second, nextComponentKey('StatsHeader'));
    expect(second._count).toBe(42);
  });

  it('consumes the root key even when it is not adopting, so the walk below does not shift', () => {
    class HomePage2 {
      _x = 0;
    }
    runComponentWalk(new HomePage2(), false, () => undefined);
    expect(nextComponentKey('HomePage2')).toBe('HomePage2#1');
  });

  it('a BUILD ROOT takes the NEXT key, because the realizer entered it too', () => {
    // Both root render methods call `component.render()` on what `build()` returned, and that
    // render() is this same entry point. It used to restart the count there and claim `#0`, so a
    // component that builds another of its OWN type handed the child the ROOT's entry.
    //
    // The child is NAMED though, and that took measuring rather than reasoning: `Build => new
    // StatsHeader()` with no container makes the server emit `StatsHeader#0`, because the realizer
    // enters every UiComponent it expands and a build root is one. The C# case that says so is
    // `AComponentABuildReturnsDirectly_IsNamedLikeAnyOther`. An earlier version of this case read
    // the Core page — whose Render returns markup, not a component — and concluded the opposite.
    class TreeNode {
      static $hydration = { _label: 'declared' } as const;
      _label = 'default';
    }
    win.__INITIAL_STATE__ = {
      'TreeNode#0': { _label: 'the root' },
      'TreeNode#1': { _label: 'the child' },
    };

    const outer = new TreeNode();
    const inner = new TreeNode();

    runComponentWalk(outer, true, () => runComponentWalk(inner, true, () => undefined));

    expect(outer._label).toBe('the root');
    expect(inner._label).toBe('the child');
  });

  it('adopts ONCE per instance, so a retained component keeps what it has done since', () => {
    // The lowering resolves a stateful child against the instance store and gets the SAME object
    // back on every pass, while the payload is deliberately kept for the life of the document. So
    // every re-render of the parent wrote the server's original fields over whatever the reader had
    // changed: a filter reset itself each time anything above it redrew. The root already adopts
    // only while unmounted; this is the same rule for everything below it.
    class Filter {
      static $hydration = { _query: 'declared' } as const;
      _query = '';
    }
    win.__INITIAL_STATE__ = { 'Filter#0': { _query: 'server' } };

    const retained = new Filter();
    expect(adoptServerStateFor(retained, nextComponentKey('Filter'))).toBe(true);
    expect(retained._query).toBe('server');

    retained._query = 'what the reader typed';

    resetComponentKeys();
    expect(adoptServerStateFor(retained, nextComponentKey('Filter'))).toBe(false);
    expect(retained._query).toBe('what the reader typed');
  });

  it('NAMES a component the lowering renders, because the server named it too', () => {
    // A write-once StatelessComponent twin carries NO `nodeKind` — only StatefulComponent declares
    // one — so the lowering reaches it through the mixing seam and calls its render(). The C#
    // realizer meanwhile ENTERS it like any other UiComponent and consumes an ordinal for it. The
    // seam has to consume the same one: the lowering names what it renders, exactly as it does for
    // a `component` node, and the render inside is then a step in the walk rather than the start of
    // one.
    class StatsHeader {
      static $hydration = { _count: 'declared' } as const;
      _count = 0;
      render() {
        // what a real StatelessComponent.render() does around its build
        return runComponentWalk(this, true, () => ({
          tag: 'div',
          attributes: {},
          events: {},
          children: [],
        }));
      }
    }
    class Footer {
      static $hydration = { _note: 'declared' } as const;
      _note = '';
    }
    win.__INITIAL_STATE__ = {
      'HomePage#0': { _downloads: 1 },
      'StatsHeader#0': { _count: 10 },
      'Footer#0': { _note: 'mine' },
    };

    const page = new HomePage();
    const header = new StatsHeader();
    const footer = new Footer();

    runComponentWalk(page, true, () => {
      lowerVisualNode(header as unknown as VisualNodeValue, {
        textPrimary: {
          light: { r: 0, g: 0, b: 0, a: 255 },
          dark: { r: 255, g: 255, b: 255, a: 255 },
        },
      });
      adoptServerStateFor(footer, nextComponentKey('Footer'));
    });

    expect(header._count).toBe(10);
    // ONE ordinal, not two: the seam names it and the render inside does not name it again.
    expect(footer._note).toBe('mine');
  });

  it('names a component the seam reaches with NOTHING in progress either', () => {
    // The same rule as the case below, at the other end of the range: whether a walk is running is
    // a fact about the caller, and the component's key is a fact about the component. An earlier
    // version read the first as the second — it suppressed every render the seam reached — and a
    // component with nothing above it then adopted nothing at all.
    class Widget {
      static $hydration = { _text: 'declared' } as const;
      _text = 'default';
      render() {
        return runComponentWalk(this, true, () => ({
          tag: 'aside',
          attributes: {},
          events: {},
          children: [],
        }));
      }
    }
    win.__INITIAL_STATE__ = { 'Widget#0': { _text: 'from the server' } };

    const widget = new Widget();
    lowerVisualNode(widget as unknown as VisualNodeValue, {
      textPrimary: {
        light: { r: 0, g: 0, b: 0, a: 255 },
        dark: { r: 255, g: 255, b: 255, a: 255 },
      },
    });

    expect(widget._text).toBe('from the server');
  });

  it('restarts the count for an OUTERMOST bridge render too, not only a page root', () => {
    // A Core page composes a write-once subtree through a VisualNodeComponent, whose render() goes
    // straight to the lowering — no `runComponentWalk` of its own. When that bridge render is the
    // outermost thing happening, nothing restarted the count, so the SAME child was named Type#0 on
    // one render and Type#1 on the next: the payload names the first, and every render after the
    // first reverted the component to its defaults.
    class Widget {
      static $hydration = { _text: 'declared' } as const;
      _text = 'default';
      render() {
        return runComponentWalk(this, true, () => ({
          tag: 'aside',
          attributes: {},
          events: {},
          children: [],
        }));
      }
    }
    win.__INITIAL_STATE__ = { 'Widget#0': { _text: 'from the server' } };

    const context = {
      textPrimary: {
        light: { r: 0, g: 0, b: 0, a: 255 },
        dark: { r: 255, g: 255, b: 255, a: 255 },
      },
    };

    const first = new Widget();
    lowerVisualNode(first as unknown as VisualNodeValue, context);
    expect(first._text).toBe('from the server');

    const second = new Widget();
    lowerVisualNode(second as unknown as VisualNodeValue, context);
    expect(second._text).toBe('from the server');
  });

  it('a NESTED render joins the walk instead of restarting it', () => {
    // A web component composed in an abstract tree carries no nodeKind and renders itself — and
    // that render() is the same method a page root calls. Left alone it reset the count mid-walk,
    // so with two same-type prefetching children both claimed Type#0 and the second was handed the
    // FIRST one's data. A wrong value, not a missing one.
    class StatsHeader {
      static $hydration = { _count: 'declared' } as const;
      _count = 0;
    }
    win.__INITIAL_STATE__ = {
      'HomePage#0': { _downloads: 1 },
      'StatsHeader#0': { _count: 10 },
      'StatsHeader#1': { _count: 20 },
    };

    const page = new HomePage();
    const first = new StatsHeader();
    const second = new StatsHeader();

    runComponentWalk(page, true, () => {
      adoptServerStateFor(first, nextComponentKey('StatsHeader'));

      // the foreign-render seam: something nested calls the root's own entry point
      runComponentWalk(new HomePage(), false, () => undefined);

      adoptServerStateFor(second, nextComponentKey('StatsHeader'));
    });

    expect(first._count).toBe(10);
    expect(second._count).toBe(20);
  });
});

/**
 * WHAT THE CLASS'S MAP LISTS is what the server sends: the compiler writes `static $hydration` from
 * the hydration manifest the server writes its payload from. A captured primary-constructor
 * parameter the router did not pass is never assigned, so the instance has no such property, and a
 * rule that adopted only members the instance already held refused the server's value for it.
 */
describe('server-state adoption by the hydration manifest', () => {
  beforeEach(() => {
    resetComponentKeys();
  });

  afterEach(() => {
    delete win.__INITIAL_STATE__;
    resetComponentKeys();
  });

  it('adopts a listed value into a member the instance has not assigned yet', () => {
    class Quote {
      declare symbol: string | undefined;
      static get $hydration() {
        return { symbol: 'declared' as const };
      }
    }
    const quote = new Quote();
    expect(Object.prototype.hasOwnProperty.call(quote, 'symbol')).toBe(false);
    win.__INITIAL_STATE__ = { 'Quote#0': { symbol: 'ACME' } };

    runComponentWalk(quote, true, () => undefined);

    expect(quote.symbol).toBe('ACME');
  });

  it('coerces a listed value by its wire spec', () => {
    class Stats {
      declare downloads: bigint | undefined;
      static get $hydration() {
        return { downloads: 'long' as const };
      }
    }
    const stats = new Stats();
    win.__INITIAL_STATE__ = { 'Stats#0': { downloads: '9007199254740993' } };

    runComponentWalk(stats, true, () => undefined);

    expect(stats.downloads).toBe(9007199254740993n);
  });

  it('leaves out a key the map does not list, even one the instance holds', () => {
    // The map, not the instance, says what the server carries here. A payload key it does not list
    // came from a build that described this class differently, and taking it would overwrite a
    // member with a value nothing in this build sent.
    class Draft {
      _count = 1;
      _text = 'typed here';
      static get $hydration() {
        return { _count: 'declared' as const };
      }
    }
    const draft = new Draft();
    win.__INITIAL_STATE__ = { 'Draft#0': { _count: 2, _text: 'from an older build' } };

    runComponentWalk(draft, true, () => undefined);

    expect(draft._count).toBe(2);
    expect(draft._text).toBe('typed here');
  });

  it('adopts a projection as plain data, never rebuilt into the twin of its class', () => {
    // A server value crosses as only the members the browser reads. The class it was on the server has
    // a twin whose getters compute from members that never crossed, so the projection stays plain.
    class SiteOptions {
      title = '';
      maxUpload = 0n;
      get display() {
        return this.title.toUpperCase();
      }
      static get $hydration() {
        return { maxUpload: 'long' as const };
      }
    }
    class AboutPage {
      _options: unknown = undefined;
      static get $hydration() {
        return { _options: { members: { maxUpload: 'long' as const } } };
      }
    }
    const page = new AboutPage();
    win.__INITIAL_STATE__ = { 'AboutPage#0': { _options: { title: 'Docs', maxUpload: '9007199254740993' } } };

    runComponentWalk(page, true, () => undefined);

    expect(page._options).not.toBeInstanceOf(SiteOptions);
    expect(Object.getPrototypeOf(page._options)).toBe(Object.prototype);
    expect(page._options).toEqual({ title: 'Docs', maxUpload: 9007199254740993n });
  });

  it('adopts a null server value as null', () => {
    class LoginPage {
      declare identity: unknown;
      static get $hydration() {
        return { identity: { members: {} } };
      }
    }
    const page = new LoginPage();
    win.__INITIAL_STATE__ = { 'LoginPage#0': { identity: null } };

    runComponentWalk(page, true, () => undefined);

    expect(page.identity).toBeNull();
  });

  it('adopts nothing into a class without a map, which the build described no state for', () => {
    class Undescribed {
      _count = 1;
    }
    const undescribed = new Undescribed();
    win.__INITIAL_STATE__ = { 'Undescribed#0': { _count: 2 } };

    expect(adoptServerStateFor(undescribed, nextComponentKey('Undescribed'))).toBe(false);
    expect(undescribed._count).toBe(1);
  });
});
