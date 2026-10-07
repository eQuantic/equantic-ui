import { describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import type { Component } from '../core/types';
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

const throughJson = (state: Record<string, unknown>): Record<string, unknown> =>
  JSON.parse(JSON.stringify(state)) as Record<string, unknown>;

describe('a hot reload carries a page across in the fields its C# declares', () => {
  it('captures the page fields and none of the runtime fields beside them', () => {
    const page = new Counter();
    page._count = 3;
    page._items.push(1, 2);

    const state = capturePageState(page);

    expect(state).toEqual({ _count: 3, _items: [1, 2], _label: 'start' });
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

  it('leaves behind alone a field JSON cannot carry', () => {
    const page = new Counter() as Counter & { _loop?: unknown };
    const loop: Record<string, unknown> = {};
    loop.self = loop;
    page._loop = loop;
    page._count = 2;

    const state = capturePageState(page);

    expect(state._count).toBe(2);
    expect('_loop' in state).toBe(false);
  });
});
