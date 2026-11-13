/**
 * A grouped Select in the browser (#677, REAL eqc output): the options of every group are one list.
 * Driven as a person drives it: a click opens the field, keydown moves the highlight, Enter chooses.
 * The C# twin is KeyboardTests.ArrowsCrossAGroup_AndEnterReportsTheIndexAcrossTheGroups.
 */

import { afterEach, describe, expect, it } from 'vitest';
import { StatefulComponent } from '../core/component';
import { resetShortcuts } from '../dom/shortcuts';
import { photonTheme } from './design-system.generated';
import { setPhotonTheme } from './photon-context';
import { Column } from './vocabulary';
import { lowerVisualNode } from './lowering';
import type { VisualNodeValue } from './nodes';
import { Select } from './components/Select';
import { OptionGroup } from './components/OptionGroup';

setPhotonTheme(photonTheme);

const nextFrame = () =>
  new Promise<void>((resolve) =>
    requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
  );

const chosen: number[] = [];

class GroupedHost extends StatefulComponent {
  build(): Column {
    const column = new Column(0);
    column.add(
      new Select(
        null,
        1,
        (index: number) => chosen.push(index),
        null,
        [new OptionGroup('Open', ['Lisboa', 'Porto']), new OptionGroup('Next', ['Braga'])],
      ) as never,
    );
    return column as never;
  }
}

describe('a grouped Select, driven in the browser', () => {
  afterEach(() => {
    resetShortcuts();
    document.body.innerHTML = '';
    chosen.length = 0;
  });

  it('ArrowDown crosses from the last option of a group to the first of the next, and Enter reports it', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    new GroupedHost().mount(container);

    const combobox = () => container.querySelector('[role="combobox"]') as HTMLElement;
    combobox().click();
    await nextFrame();

    const before = container.querySelector(`#${combobox().getAttribute('aria-activedescendant')}`)!;
    expect(before.textContent).toBe('Porto');

    window.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }),
    );
    await nextFrame();

    const active = container.querySelector(`#${combobox().getAttribute('aria-activedescendant')}`)!;
    expect(active.getAttribute('role')).toBe('option');
    expect(active.textContent).toBe('Braga');
    const group = active.parentElement!.closest('[role="group"]')!;
    expect(group.getAttribute('aria-label')).toBe('Next');
    expect(group.contains(active)).toBe(true);

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
    await nextFrame();

    expect(chosen).toEqual([2]);
    expect(container.querySelector('[role="listbox"]')).toBeNull();
  });
});

describe('a nameless Group (C# twin: GroupSemanticsTests)', () => {
  const text = (content: string, extra: Record<string, unknown> = {}) => ({
    nodeKind: 'text',
    content,
    role: 'bodyM',
    ...extra,
  });
  const flexible = () => ({ nodeKind: 'flexible', child: text('Delivery') });
  const group = (child: unknown, extra: Record<string, unknown> = {}) => ({
    nodeKind: 'group',
    label: '',
    child,
    ...extra,
  });
  const column = (...children: unknown[]) =>
    ({ nodeKind: 'column', gap: 0, children, height: { kind: 'fixed', value: 200 } }) as unknown as VisualNodeValue;
  const lower = (node: unknown) => lowerVisualNode(node as VisualNodeValue, { textPrimary: photonTheme.textPrimary });

  it('lowers to its child, on the parent axis', () => {
    expect(JSON.stringify(lower(column(group(flexible()))))).toBe(JSON.stringify(lower(column(flexible()))));
  });

  it('the outermost key wins, so keyed siblings stay distinct', () => {
    const lowered = lower(
      column(
        group(text('A', { key: 'row' }), { key: 'a' }),
        group(text('B', { key: 'row' }), { key: 'b' }),
        group(text('C'), { key: 'c' }),
      ),
    ) as { children: { key?: string }[] };
    expect(lowered.children.map((child) => child.key)).toEqual(['a', 'b', 'c']);
  });

  it('a bookmark lands, and a second id on the same element is refused', () => {
    expect((lower(group(text('Next'), { bookmark: 'next' })) as { attributes: Record<string, string> }).attributes['id']).toBe('next');
    expect(() => lower(group(text('Next', { bookmark: 'inner' }), { bookmark: 'outer' }))).toThrow(/'outer'.*'inner'/);
  });

  it('a stack sees a positioned through it', () => {
    const corner = () => ({ nodeKind: 'positioned', child: text('x'), top: 0, end: 0 });
    const stack = (layer: unknown) => ({
      nodeKind: 'stack',
      align: 'topStart',
      children: [{ nodeKind: 'box', style: { width: { kind: 'fixed', value: 100 } } }, layer],
    });
    expect(JSON.stringify(lower(stack(group(corner()))))).toBe(JSON.stringify(lower(stack(corner()))));
  });

  it('blank space is no name', () => {
    expect((lower(group(text('Delivery'), { label: '  ' })) as { attributes: Record<string, string> }).attributes['role']).toBeUndefined();
  });
});
